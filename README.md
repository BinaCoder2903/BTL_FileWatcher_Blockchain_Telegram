# BTL_FileWatcher_Blockchain_Telegram

Giám sát thay đổi file **thời gian thực** trên Windows, hiển thị trên **dashboard web**, lưu vết theo kiểu **hash-chain (blockchain mini)** để hạn chế sửa log, và **gửi cảnh báo Telegram** cho sự kiện quan trọng.

> Demo nhanh: tạo/sửa/đổi tên/xoá file trong thư mục theo dõi → tab **Real-time** hiển thị ngay; ~15s sau vào tab **Lịch sử** để thấy sự kiện đã “đúc block”; Telegram ting khi **RENAMED/DELETED** (và gộp **CHANGED** để tránh spam).

---

## ✨ Tính năng
- Realtime 4 loại sự kiện: `CREATED / CHANGED / RENAMED / DELETED`.
- Theo dõi **mọi loại file** và **thư mục** (hash thư mục = `DIR`).
- Hash **SHA-256** (streaming); file lớn gắn nhãn `TOO-LARGE`, file bị khóa → `LOCKED`.
- **Blockchain mini**: gom sự kiện ~15s/block; `Hash = SHA256(timestamp + previousHash + data)`.
- **Dashboard web** (Tailwind + JS): Real-time, Lịch sử (immutable), **Export CSV**.
- **Telegram alert**: gửi ngay khi rename/delete; batch đối với change.
- Hỗ trợ chạy **Windows Service** (server & client).
- **`.watchignore`** để loại rác/đường dẫn hệ thống.

---

## 🧱 Kiến trúc
```
Client (FileSystemWatcher) --SignalR--> Server (ASP.NET Core)
   |                                        |
Hash SHA-256 + debounce                 EF Core + SQLite
                                        Block creation (hash-chain)
                                        Dashboard (Tailwind/JS)
/api/history, /api/export/csv           Telegram (bot → chat/group)
```

---

## ✅ Yêu cầu
- Windows 10/11
- .NET 8 SDK: https://dotnet.microsoft.com/en-us/download
- (Tuỳ chọn) Telegram bot (token + chat_id) nếu bật cảnh báo.

---

## 🚀 Cách chạy nhanh (dev)

### 1) Server (Dashboard + Blockchain + API)
```bash
cd FileWatcherServer
dotnet restore
dotnet run
```
- Mở **http://localhost:5000** (dashboard).
- Test Telegram (nếu đã cấu hình): **http://localhost:5000/api/alert/test**.

### 2) Client (Watcher)
Mở `FileWatcherClient/Worker.cs` và kiểm tra:
```csharp
const string WATCH_PATH = @"C:	emp\watch-test";            // thư mục theo dõi
const string SERVER_URL  = "http://localhost:5000/notifyHub"; // hub server
```
Chạy:
```bash
cd FileWatcherClient
dotnet restore
dotnet run
```

**Thử nhanh**
- Tạo `C:	emp\watch-test.txt` → Real-time: **CREATED**  
- Đổi tên `a.txt` → `b.txt` → **RENAMED** (Telegram ting nếu bật)  
- Xoá `b.txt` → **DELETED** (Telegram ting)  
- ~15s sau mở tab **Lịch sử** → **Export CSV**.

---

## 🔔 Bật cảnh báo Telegram (tuỳ chọn)
1. Chat **@BotFather** → tạo bot → lấy **token**.  
2. Add bot vào group (nếu gửi nhóm) và nhắn **/start** cho bot.  
3. Lấy **chat_id** (dùng `getUpdates` hoặc bot hiển thị chat id).  
4. Điền token/chat_id trong **TelegramService** (hoặc qua biến môi trường) của **FileWatcherServer**.  
5. Chạy server, gọi **/api/alert/test** để kiểm tra.

> **Đừng commit token** lên repo public.

---

## 🖥️ Chạy nền dạng Windows Service (tuỳ chọn)
Publish:
```bash
dotnet publish .\FileWatcherServer -c Release -r win-x64 --self-contained false
dotnet publish .\FileWatcherClient -c Release -r win-x64 --self-contained false
```
Cài (PowerShell/Command Prompt – Run as Admin):
```powershell
sc create FileWatcherServer binPath= "C:\Apps\FileWatcherServer\FileWatcherServer.exe" start= auto
sc start  FileWatcherServer

sc create FileWatcherClient binPath= "C:\Apps\FileWatcherClient\FileWatcherClient.exe" start= auto
sc start  FileWatcherClient
```
> Nhớ copy **`wwwroot`** cạnh `FileWatcherServer.exe` (server dùng `AppContext.BaseDirectory` làm content root).

---

## 🧹 `.watchignore` (khuyến nghị)
Tạo `C:	emp\watch-test\.watchignore`:
```
**/Windows/**
**/Program Files/**
**/Program Files (x86)/**
**/AppData/**
**/*.tmp
**/~$*
**/bin/**
**/obj/**
**/.git/**
```

---

## 📁 Cấu trúc dự án
```
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
```

---

## 🧪 Troubleshooting
- **Client không kết nối** → kiểm tra `SERVER_URL`, port 5000, firewall.  
- **Dashboard trắng khi chạy service** → đảm bảo `wwwroot` nằm cạnh `.exe`.  
- **Không thấy lịch sử** → đợi chu kỳ đúc block (~15s) hoặc phát sinh thêm sự kiện.  
- **Telegram không ting** → sai token/chat_id hoặc chưa **/start**; test **/api/alert/test**.  
- **Hash = `TOO-LARGE` / `LOCKED`** → file lớn/đang bị khoá; thử lại hoặc điều chỉnh giới hạn trong `Worker.cs`.

---

## 📜 License
MIT License.

---

## 💡 Hướng phát triển
- Auth/role cho dashboard; lọc lịch sử theo thời gian.  
- Phân trang hoặc **virtualize** bảng lịch sử (`/api/history?skip=&take=`).  
- Bổ sung metadata (owner/ACL) & chữ ký số block.  
- Với file text: gửi kèm **diff** cho sự kiện `CHANGED`.
