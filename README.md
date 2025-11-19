# BTL_FileWatcher_Blockchain_Telegram

Giám sát thay đổi file **thời gian thực** trên Windows, hiển thị trên **dashboard web**, lưu vết kiểu **hash-chain (blockchain mini)** để chống sửa log, và **gửi cảnh báo Telegram** cho sự kiện quan trọng.

> Demo: tạo/sửa/đổi tên/xoá file trong thư mục theo dõi → thấy realtime trên web, sau ~15s vào tab **Lịch sử** để xem các sự kiện đã “đúc block”, Telegram sẽ ting khi rename/delete.

---

## ✨ Tính năng chính
- **Realtime**: Client đẩy sự kiện `CREATED / CHANGED / RENAMED / DELETED` qua SignalR.
- **Mọi định dạng + Thư mục**: Theo dõi *mọi file* và cả **folder** (hash = `DIR`).
- **Hash SHA-256** (streaming). File quá lớn → gắn nhãn `TOO-LARGE`, file bị khóa → `LOCKED`.
- **Blockchain mini**: Gom sự kiện định kỳ (~15s) vào block; `Hash = SHA256(timestamp + previousHash + data)`.
- **Dashboard**: Tab **Real-time** (lọc/tìm), tab **Lịch sử** (immutable, Export CSV).
- **Telegram alert**: Ping ngay khi `RENAMED/DELETED` (gộp `CHANGED` để tránh spam).
- **Windows Service (tùy chọn)**: Server/Client có thể chạy nền, tự khởi động cùng Windows.
- **`.watchignore`**: Loại rác/đường dẫn hệ thống.

---

## 🧱 Kiến trúc nhanh

Client (FileSystemWatcher) --SignalR--> Server (ASP.NET Core)
| |
Hash SHA-256 + debounce EF Core + SQLite
Block creation (hash-chain)
Dashboard (Tailwind/JS)
/api/history, /api/export/csv
Telegram (bot → chat / group)

yaml
Sao chép mã

---

## ✅ Yêu cầu
- **Windows 10/11**
- **.NET 8 SDK**: https://dotnet.microsoft.com/en-us/download
- (Tùy chọn) **Telegram bot** (token + chatId) nếu dùng cảnh báo.

---

## 🚀 Cách chạy nhanh (dev)

### 1) Server (Dashboard + Blockchain + API)
```bash
cd FileWatcherServer
dotnet restore
dotnet run
Mở http://localhost:5000 (dashboard)

Test bot: http://localhost:5000/api/alert/test (nếu đã cấu hình Telegram)

2) Client (Watcher)
Mở FileWatcherClient/Worker.cs và kiểm tra:

csharp
Sao chép mã
const string WATCH_PATH = @"C:\temp\watch-test";      // thư mục theo dõi
const string SERVER_URL  = "http://localhost:5000/notifyHub"; // hub server
Chạy:

bash
Sao chép mã
cd FileWatcherClient
dotnet restore
dotnet run
Thử nghiệm:

Tạo C:\temp\watch-test\a.txt → Realtime thấy CREATED

Đổi tên a.txt → b.txt → RENAMED (Telegram ting nếu bật)

Xóa b.txt → DELETED (Telegram ting)

Sau ~15s sang tab Lịch sử để thấy sự kiện trong block → Export CSV.

🔔 (Tùy chọn) Cảnh báo Telegram
Tạo bot với @BotFather → nhận token.

Add bot vào group (nếu gửi vào group) và gửi /start cho bot.

Lấy chat_id (dùng getUpdates hoặc bot hiển thị chat id).

Đặt token/chatId trong TelegramService (hoặc biến môi trường) của FileWatcherServer.

Chạy server và mở /api/alert/test để thử.

Không commit token vào repo public.

🖥️ (Tùy chọn) Chạy nền dạng Windows Service
Publish:

bash
Sao chép mã
dotnet publish .\FileWatcherServer -c Release -r win-x64 --self-contained false
dotnet publish .\FileWatcherClient -c Release -r win-x64 --self-contained false
Cài (PowerShell Admin):

powershell
Sao chép mã
sc create FileWatcherServer binPath= "C:\Apps\FileWatcherServer\FileWatcherServer.exe" start= auto
sc start  FileWatcherServer
sc create FileWatcherClient binPath= "C:\Apps\FileWatcherClient\FileWatcherClient.exe" start= auto
sc start  FileWatcherClient
Đảm bảo wwwroot nằm cạnh FileWatcherServer.exe (server dùng ContentRootPath = AppContext.BaseDirectory).

🧹 .watchignore (khuyến nghị)
Tạo file C:\temp\watch-test\.watchignore:

markdown
Sao chép mã
**/Windows/**
**/Program Files/**
**/Program Files (x86)/**
**/AppData/**
**/*.tmp
**/~$*
**/bin/**
**/obj/**
**/.git/**
📁 Cấu trúc dự án
pgsql
Sao chép mã
FileWatcherServer/
  Program.cs
  Data/AppDbContext.cs
  Models/EventBlock.cs
  Hubs/NotifyHub.cs
  Services/BlockchainService.cs
  Services/TelegramService.cs
  Services/AlertEngine.cs
  wwwroot/index.html   <-- Dashboard UI

FileWatcherClient/
  Program.cs
  Worker.cs            <-- watcher (mọi file + thư mục, hash, debounce)
  IgnoreMatcher.cs     <-- glob ignore
🧪 Troubleshooting
Client không kết nối → Kiểm tra SERVER_URL, port 5000, firewall.

Dashboard trắng khi chạy service → Copy wwwroot cạnh .exe.

Không thấy lịch sử → Đợi qua chu kỳ đúc block (~15s) hoặc tạo thêm thao tác.

Telegram không ting → Sai token/chatId hoặc chưa /start; thử /api/alert/test.

Hash = TOO-LARGE / LOCKED → File quá lớn hoặc đang bị app khác khóa; thử lại hoặc tăng giới hạn trong Worker.cs.
