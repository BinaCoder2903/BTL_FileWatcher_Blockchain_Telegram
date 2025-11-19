// Program.cs - FileWatcherServer (.NET 8)

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using FileWatcherServer.Data;
using FileWatcherServer.Hubs;
using FileWatcherServer.Models;
using FileWatcherServer.Services;
using Microsoft.Extensions.Hosting.WindowsServices; 


var builder = WebApplication.CreateBuilder(args);

// Chạy như Windows Service + cố định content root/cổng
if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}
builder.WebHost.UseUrls("http://localhost:5000;https://localhost:5001");

// DI
builder.Services.AddSignalR();
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite("Data Source=filewatcher.db"));
builder.Services.AddSingleton<BlockchainService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockchainService>()); // <-- dòng đã sửa

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddSingleton<AlertBatcher>();

builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials()));

var app = builder.Build();

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

// Tạo DB nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// API lịch sử
app.MapGet("/api/history", async (AppDbContext db) =>
{
    var all = new List<FileEvent>();
    var blocks = await db.EventBlocks.Where(b => b.Id > 1).OrderByDescending(b => b.Id).ToListAsync();
    foreach (var b in blocks)
    {
        var evs = JsonSerializer.Deserialize<List<FileEvent>>(b.Data);
        if (evs != null) all.AddRange(evs);
    }
    return Results.Ok(all.OrderByDescending(e => e.Timestamp));
});

// Export CSV
app.MapGet("/api/export/csv", async (AppDbContext db) =>
{
    var rows = new List<FileEvent>();
    var blocks = await db.EventBlocks.Where(b => b.Id > 1).OrderBy(b => b.Id).ToListAsync();
    foreach (var b in blocks)
    {
        var evs = JsonSerializer.Deserialize<List<FileEvent>>(b.Data);
        if (evs != null) rows.AddRange(evs);
    }
    var sb = new StringBuilder();
    sb.AppendLine("ClientId,EventType,FilePath,OldFilePath,Size,Timestamp,FileHash");
    foreach (var e in rows.OrderBy(e => e.Timestamp))
        sb.AppendLine($"\"{e.ClientId}\",\"{e.EventType}\",\"{e.FilePath}\",\"{e.OldFilePath ?? ""}\",{e.Size},\"{e.Timestamp:o}\",\"{e.FileHash}\"");
    return Results.Text(sb.ToString(), "text/csv", Encoding.UTF8);
});

// Test telegram
app.MapGet("/api/alert/test", async (TelegramService tg) =>
{
    await tg.SendAsync("✅ Telegram alert test from FileWatcherServer");
    return Results.Ok(new { ok = true });
});

// Health
app.MapGet("/healthz", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }));

// Hub
app.MapHub<NotifyHub>("/notifyHub");

await app.RunAsync();
