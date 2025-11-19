// AlertBatcher.cs - server
// Gom nhieu CHANGED cua 1 file trong 30s thanh 1 tin Telegram

using FileWatcherServer.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace FileWatcherServer.Services;

public class AlertBatcher
{
    private readonly IMemoryCache _cache;
    private readonly TelegramService _tg;

    public AlertBatcher(IMemoryCache cache, TelegramService tg)
    {
        _cache = cache;
        _tg = tg;
    }

    public void Enqueue(FileEvent ev, int windowSeconds = 30)
    {
        var key = $"batch::{ev.ClientId}::{ev.FilePath}";
        var list = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(windowSeconds);
            return new List<FileEvent>();
        })!;
        lock (list) { list.Add(ev); }
    }

    public async Task FlushAsync(FileEvent ev, int windowSeconds = 30)
    {
        var key = $"batch::{ev.ClientId}::{ev.FilePath}";
        if (_cache.TryGetValue(key, out List<FileEvent>? list) && list != null)
        {
            List<FileEvent> captured;
            lock (list) { captured = list.ToList(); list.Clear(); }
            _cache.Remove(key);

            if (captured.Count == 0) return;

            var first = captured[0];
            var sb = new StringBuilder();
            sb.AppendLine($"<b>[BATCH {captured.Count} events]</b>");
            sb.AppendLine($"• Client: <code>{first.ClientId}</code>");
            sb.AppendLine($"• Path: <code>{first.FilePath}</code>");
            if (!string.IsNullOrEmpty(first.OldFilePath))
                sb.AppendLine($"• Old: <code>{first.OldFilePath}</code>");
            sb.AppendLine($"• Window: {windowSeconds}s\n");

            foreach (var e in captured.Take(10))
                sb.AppendLine($"• {e.EventType} @ {e.Timestamp:HH:mm:ss}");
            if (captured.Count > 10)
                sb.AppendLine($"• (+{captured.Count - 10} more)");

            sb.AppendLine("\n🖥️ Dashboard: http://localhost:5000/");
            await _tg.SendAsync(sb.ToString());
        }
    }
}
