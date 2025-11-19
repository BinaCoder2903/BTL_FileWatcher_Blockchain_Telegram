// NotifyHub.cs - server
// Gui ngay Telegram voi RENAMED/DELETED; CHANGED thi batch 30s

using Microsoft.AspNetCore.SignalR;
using FileWatcherServer.Services;
using FileWatcherServer.Models;

namespace FileWatcherServer.Hubs;

public class NotifyHub : Hub
{
    private readonly BlockchainService _blockchain;
    private readonly TelegramService _telegram;
    private readonly AlertBatcher _batcher;

    public NotifyHub(BlockchainService blockchain, TelegramService telegram, AlertBatcher batcher)
    {
        _blockchain = blockchain;
        _telegram = telegram;
        _batcher = batcher;
    }

    public async Task SendEvent(FileEvent ev)
    {
        // day vao blockchain
        _blockchain.AddEventToQueue(ev);

        // realtime UI
        await Clients.All.SendAsync("ReceiveNewEvent", ev);

        if (ev.EventType is "RENAMED" or "DELETED")
        {
            var msg =
                $"<b>[ALERT {ev.EventType}]</b>\n" +
                $"• Client: <code>{ev.ClientId}</code>\n" +
                $"• Path: <code>{ev.FilePath}</code>\n" +
                (ev.OldFilePath is null ? "" : $"• Old: <code>{ev.OldFilePath}</code>\n") +
                $"• Time(UTC): <code>{ev.Timestamp:o}</code>\n\n" +
                $"🖥️ Dashboard: http://localhost:5000/";
            _ = _telegram.SendAsync(msg);
        }
        else if (ev.EventType == "CHANGED")
        {
            _batcher.Enqueue(ev, 30);
            _ = Task.Delay(30000).ContinueWith(_ => _batcher.FlushAsync(ev));
        }
        // CREATED: tuy ban muon gui ngay hay khong
    }
}
