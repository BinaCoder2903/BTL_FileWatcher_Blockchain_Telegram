using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace FileWatcherServer.Models;

public class EventBlock
{
    [Key] public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string PreviousHash { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;     // JSON list<FileEvent>
    public string Hash { get; set; } = string.Empty;

    public EventBlock() { }
    public EventBlock(DateTime timestamp, string previousHash, string data)
    {
        Timestamp = timestamp;
        PreviousHash = previousHash;
        Data = data;
        Hash = CalculateHash();
    }

    public string CalculateHash()
    {
        using var sha256 = SHA256.Create();
        var raw = $"{Timestamp:o}-{PreviousHash}-{Data}";
        return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(raw)));
    }
}
