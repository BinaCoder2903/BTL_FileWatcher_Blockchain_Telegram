namespace FileWatcherServer.Models;

// alert rule model (bind tu appsettings)
public record AlertRule(
    string id,
    string name,
    string[] eventTypes,
    string pathPattern,
    string? clientId,
    int cooldownSeconds,
    string? chatId,
    bool enabled
);
