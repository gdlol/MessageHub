namespace MessageHub.Complement.Logging;

public sealed class PrefixedLoggerProvider : ILoggerProvider
{
    public string Prefix { get; }

    private readonly ILoggerFactory loggerFactory;

    public PrefixedLoggerProvider(ILoggerProvider provider, string prefix)
    {
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(provider);
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        Prefix = prefix;
    }

    public ILogger CreateLogger(string categoryName)
    {
        categoryName = $"{Prefix}{categoryName}";
        return loggerFactory.CreateLogger(categoryName);
    }

    public void Dispose()
    {
        loggerFactory.Dispose();
    }
}
