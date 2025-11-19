// Services/TelegramService.cs
// Ban khong can appsettings. Sua 2 hang so BOT_TOKEN va DEFAULT_CHAT_ID la chay.

using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FileWatcherServer.Services;

public static class TelegramConfig
{
    // SUA 2 HANG SO NAY NEU MUON HARD-CODE
    public const bool ENABLE = true; 
    public const string BOT_TOKEN = "8537068075:AAHF0S-IvH-41CYjWWZY6GwYgCrARiQ53TY";        
    public const string DEFAULT_CHAT_ID = "2094453264";  
}

public class TelegramService
{
    private readonly HttpClient _http = new();
    private readonly ILogger<TelegramService> _log;
    private readonly bool _enable;
    private readonly string _token;
    private readonly string? _defaultChatId;

    public TelegramService(ILogger<TelegramService> log)
    {
        _log = log;

        // Uu tien bien moi truong neu co, khong co thi dung hang so
        _token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                        ?? TelegramConfig.BOT_TOKEN;
        _defaultChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
                        ?? TelegramConfig.DEFAULT_CHAT_ID;
        _enable = TelegramConfig.ENABLE;

        if (!_enable)
            _log.LogWarning("Telegram disabled (ENABLE=false).");
        else
            _log.LogInformation("Telegram enabled. ChatId default: {cid}", _defaultChatId ?? "(none)");
    }

    public async Task SendAsync(string text, string? chatId = null, CancellationToken ct = default)
    {
        if (!_enable) { _log.LogDebug("Skip send: Telegram disabled."); return; }

        var cid = chatId ?? _defaultChatId;
        if (string.IsNullOrWhiteSpace(_token))
        {
            _log.LogError("Telegram token is empty. Set TELEGRAM_BOT_TOKEN env or TelegramConfig.BOT_TOKEN.");
            return;
        }
        if (string.IsNullOrWhiteSpace(cid))
        {
            _log.LogError("Telegram chat id is empty. Set TELEGRAM_CHAT_ID env or TelegramConfig.DEFAULT_CHAT_ID.");
            return;
        }

        try
        {
            var url = $"https://api.telegram.org/bot{_token}/sendMessage";
            var payload = new { chat_id = cid, text, parse_mode = "HTML", disable_web_page_preview = true };
            var json = JsonSerializer.Serialize(payload);

            using var res = await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _log.LogError("Telegram send failed. Status={Status} Body={Body}", (int)res.StatusCode, body);
            }
            else
            {
                _log.LogInformation("Telegram message sent to {cid}", cid);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Telegram send exception");
        }
    }
}
