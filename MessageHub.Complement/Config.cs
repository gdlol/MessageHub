using Microsoft.Extensions.Logging.Console;

namespace MessageHub.Complement;

public class Config
{
    public string ServerName { get; init; } = default!;
    public string SelfUrl { get; init; } = default!;
    public string DataPath { get; init; } = default!;
    public string PrivateNetworkSecret { get; init; } = default!;
    public ConsoleLoggerProvider ConsoleLoggerProvider { get; init; } = default!;
}
