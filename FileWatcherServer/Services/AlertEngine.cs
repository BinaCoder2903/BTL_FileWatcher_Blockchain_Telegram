using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using FileWatcherServer.Models;

namespace FileWatcherServer.Services;

public class AlertEngine
{
    private readonly List<AlertRule> _rules;
    private readonly TelegramService _telegram;
    private readonly IMemoryCache _cache;

    public AlertEngine(IConfiguration cfg, TelegramService telegram, IMemoryCache cache)
    {
        _telegram = telegram;
        _cache = cache;
        _rules = cfg.GetSection("AlertRules").Get<List<AlertRule>>() ?? new();
    }

    // match + send with cooldown
    public async Task TryAlertAsync(FileEvent ev, CancellationToken ct = default)
    {
        foreach (var r in _rules.Where(x => x.enabled))
        {
            if (!r.eventTypes.Contains(ev.EventType)) continue;
            if (!string.IsNullOrEmpty(r.clientId) &&
                !string.Equals(r.clientId, ev.ClientId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!GlobMatch(ev.FilePath, r.pathPattern)) continue;

            // cooldown key theo (rule, file)
            var key = $"cooldown::{r.id}::{ev.FilePath}";
            if (_cache.TryGetValue(key, out _)) continue;

            var msg =
                $"<b>[{r.name}]</b>\n" +
                $"? Type: <code>{ev.EventType}</code>\n" +
                $"? Client: <code>{ev.ClientId}</code>\n" +
                $"? Path: <code>{ev.FilePath}</code>\n" +
                (string.IsNullOrEmpty(ev.OldFilePath) ? "" : $"? Old: <code>{ev.OldFilePath}</code>\n") +
                $"? Size: <code>{ev.Size}</code>\n" +
                $"? Hash: <code>{(string.IsNullOrEmpty(ev.FileHash) ? "N/A" : ev.FileHash[..Math.Min(16, ev.FileHash.Length)] + "бн")}</code>\n" +
                $"? Time(UTC): <code>{ev.Timestamp:o}</code>\n\n" +
                $"??? Dashboard: http://localhost:5000/";

            await _telegram.SendAsync(msg, r.chatId, ct);

            _cache.Set(key, true, TimeSpan.FromSeconds(Math.Max(1, r.cooldownSeconds)));
        }
    }

    // glob matcher: support **, *, ?
    private static bool GlobMatch(string text, string pattern)
    {
        var rx = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^\\/]*")
            .Replace(@"\?", ".")
            + "$";
        return Regex.IsMatch(text, rx, RegexOptions.IgnoreCase);
    }
}
