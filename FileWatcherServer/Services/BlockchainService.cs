using FileWatcherServer.Data;
using FileWatcherServer.Models;
using Microsoft.EntityFrameworkCore;

namespace FileWatcherServer.Services;

public class BlockchainService : IHostedService, IDisposable
{
    private readonly IServiceProvider _sp;
    private readonly List<FileEvent> _queue = new();
    private readonly object _lock = new();
    private Timer? _timer;
    private const int IntervalSec = 15;

    public BlockchainService(IServiceProvider sp) { _sp = sp; }

    public Task StartAsync(CancellationToken _)
    {
        _timer = new Timer(DoWork, null,
            TimeSpan.FromSeconds(IntervalSec),
            TimeSpan.FromSeconds(IntervalSec));
        return Task.CompletedTask;
    }

    public void AddEventToQueue(FileEvent ev)
    {
        lock (_lock) _queue.Add(ev);
    }

    private async void DoWork(object? _)
    {
        List<FileEvent> batch;
        lock (_lock)
        {
            if (_queue.Count == 0) return;
            batch = new List<FileEvent>(_queue);
            _queue.Clear();
        }

        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var last = await db.EventBlocks.OrderByDescending(b => b.Id).FirstOrDefaultAsync();
            var prevHash = last?.Hash ?? "0";

            var data = System.Text.Json.JsonSerializer.Serialize(batch);
            var block = new EventBlock(DateTime.UtcNow, prevHash, data);

            await db.EventBlocks.AddAsync(block);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Blockchain] New block {block.Id} with {batch.Count} events");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Blockchain] Error: {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken _) { _timer?.Change(Timeout.Infinite, 0); return Task.CompletedTask; }
    public void Dispose() => _timer?.Dispose();
}
