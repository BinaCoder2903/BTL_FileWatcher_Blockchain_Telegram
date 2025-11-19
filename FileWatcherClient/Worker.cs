using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private FileSystemWatcher? _watcher;
    private HubConnection? _hubConnection;

    // === CONFIG TESTER ===
    private const string WATCH_PATH = @"C:\temp\watch-test"; // doi theo nhu cau
    private const string FILE_FILTER = "*";                   // theo doi TAT CA
    private const string SERVER_URL = "http://localhost:5000/notifyHub";
    private const string CLIENT_ID = "CLIENT_LAPTOP_DEV";
    // gioi han khong hash file sieu lon (vd 512MB)
    private const long MaxHashSizeBytes = 512L * 1024 * 1024;
    // === END CONFIG ===

    private IgnoreMatcher? _ignore;
    private readonly Dictionary<string, DateTime> _debounce = new(); // key = fullPath
    private const int DebounceMs = 1200; // gom CHANGED trong ~1.2s

    public Worker(ILogger<Worker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File Watcher Client dang khoi dong...");

        if (!Directory.Exists(WATCH_PATH))
        {
            _logger.LogWarning("Thu muc '{path}' khong ton tai. Dang tao...", WATCH_PATH);
            Directory.CreateDirectory(WATCH_PATH);
        }

        // load ignore pattern tu file .watchignore (neu co) + mac dinh
        _ignore = IgnoreMatcher.LoadFrom(WATCH_PATH);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(SERVER_URL)
            .WithAutomaticReconnect()
            .Build();

        await ConnectToHubAsync(stoppingToken);

        _watcher = new FileSystemWatcher(WATCH_PATH)
        {
            Filter = FILE_FILTER, // tat ca
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _watcher.Created += async (s, e) => await OnFileEvent(e, "CREATED");
        _watcher.Deleted += async (s, e) => await OnFileEvent(e, "DELETED");
        _watcher.Changed += async (s, e) => await OnFileEvent(e, "CHANGED");
        _watcher.Renamed += async (s, e) => await OnFileRenamed(e);

        _logger.LogInformation("San sang nhan su kien tai: {p}", WATCH_PATH);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ConnectToHubAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Dang ket noi den hub: {u}", SERVER_URL);
                await _hubConnection!.StartAsync(token);
                _logger.LogInformation("Da ket noi, ConnectionId = {id}", _hubConnection.ConnectionId);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loi ket noi SignalR. Thu lai sau 5s.");
                await Task.Delay(5000, token);
            }
        }
    }

    private static bool IsDirectoryPath(string path)
    {
        try { return Directory.Exists(path); }
        catch { return false; }
    }

    private async Task OnFileEvent(FileSystemEventArgs e, string eventType)
    {
        // bo file tam office ~$
        if (!string.IsNullOrEmpty(e.Name) && e.Name.StartsWith("~")) return;
        // ignore theo glob
        if (_ignore != null && _ignore.IsIgnored(e.FullPath)) return;

        // thu muc: ghi nhan nhung khong hash
        if (IsDirectoryPath(e.FullPath) && eventType != "DELETED")
        {
            var dirEvent = new FileEvent(CLIENT_ID, eventType, e.FullPath, null, 0, DateTime.UtcNow, "DIR");
            await SendEventToServer(dirEvent);
            _logger.LogInformation("[{t}] (DIR) {p}", eventType, e.FullPath);
            return;
        }

        // debounce cho CHANGED (file)
        if (eventType == "CHANGED")
        {
            var now = DateTime.UtcNow;
            if (_debounce.TryGetValue(e.FullPath, out var last) &&
                (now - last).TotalMilliseconds < DebounceMs)
                return;
            _debounce[e.FullPath] = now;
        }

        _logger.LogInformation("[{t}] {p}", eventType, e.FullPath);

        string hash = "N/A";
        long size = 0;

        if (eventType != "DELETED")
        {
            (hash, size) = await GetFileMetadataWithRetry(e.FullPath);
        }

        var fileEvent = new FileEvent(
            CLIENT_ID,
            eventType,
            e.FullPath,
            null,
            size,
            DateTime.UtcNow,
            hash
        );

        await SendEventToServer(fileEvent);
    }

    private async Task OnFileRenamed(RenamedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Name) && e.Name.StartsWith("~")) return;
        if (_ignore != null && _ignore.IsIgnored(e.FullPath)) return;

        // renaming folder
        if (IsDirectoryPath(e.FullPath))
        {
            var dirEvent = new FileEvent(CLIENT_ID, "RENAMED", e.FullPath, e.OldFullPath, 0, DateTime.UtcNow, "DIR");
            await SendEventToServer(dirEvent);
            _logger.LogInformation("[RENAMED] (DIR) {old} -> {cur}", e.OldFullPath, e.FullPath);
            return;
        }

        _logger.LogInformation("[RENAMED] {old} -> {cur}", e.OldFullPath, e.FullPath);

        var (hash, size) = await GetFileMetadataWithRetry(e.FullPath);

        var fileEvent = new FileEvent(
            CLIENT_ID,
            "RENAMED",
            e.FullPath,
            e.OldFullPath,
            size,
            DateTime.UtcNow,
            hash
        );

        await SendEventToServer(fileEvent);
    }

    private async Task SendEventToServer(FileEvent fileEvent)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.InvokeAsync("SendEvent", fileEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loi khi gui su kien den server.");
            }
        }
        else
        {
            _logger.LogWarning("Hub chua ket noi. Bo qua su kien.");
        }
    }

    /// <summary>
    /// Doc file + tinh hash SHA-256, retry neu bi khoa.
    /// Tra ve (Hash, Size). Neu file qua lon -> Hash = "TOO-LARGE"
    /// </summary>
    private async Task<(string Hash, long Size)> GetFileMetadataWithRetry(string filePath, int maxRetries = 5, int delayMs = 120)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long size = fs.Length;

                if (size > MaxHashSizeBytes)
                    return ("TOO-LARGE", size);

                using var sha256 = SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(fs);
                string hash = Convert.ToHexString(hashBytes);
                return (hash, size);
            }
            catch (IOException)
            {
                await Task.Delay(delayMs * (i + 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loi khi lay metadata file: {p}", filePath);
                return ("ERROR", 0);
            }
        }
        _logger.LogWarning("Khong the truy cap file sau {n} lan: {p}", maxRetries, filePath);
        return ("LOCKED", 0);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("File Watcher Client dang dung...");
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync(cancellationToken);
            await _hubConnection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}

// Model (giu dong dinh dang voi server)
internal record FileEvent(
    string ClientId,
    string EventType,
    string FilePath,
    string? OldFilePath,
    long Size,
    DateTime Timestamp,
    string FileHash
);
