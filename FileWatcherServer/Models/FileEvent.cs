namespace FileWatcherServer.Models;

public record FileEvent(
    string ClientId,
    string EventType,      // CREATED, DELETED, CHANGED, RENAMED
    string FilePath,
    string? OldFilePath,
    long Size,
    DateTime Timestamp,
    string FileHash        // SHA256
);

