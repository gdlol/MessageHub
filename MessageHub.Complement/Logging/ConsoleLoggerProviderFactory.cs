using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace MessageHub.Complement.Logging;

public static class ConsoleLoggerProviderFactory
{
    private class Disposable : IDisposable
    {
        public void Dispose() { }
    }

    private class OptionsMonitor : IOptionsMonitor<ConsoleLoggerOptions>
    {
        private readonly ConsoleLoggerOptions options;

        public OptionsMonitor(ConsoleLoggerOptions options)
        {
            this.options = options;
        }

        public ConsoleLoggerOptions CurrentValue => options;

        public ConsoleLoggerOptions Get(string name) => options;

        public IDisposable OnChange(Action<ConsoleLoggerOptions, string> listener)
        {
            return new Disposable();
        }
    }

    public static ConsoleLoggerProvider Create(ConsoleLoggerOptions? options = null)
    {
        return new ConsoleLoggerProvider(new OptionsMonitor(options ?? new()));
    }
}
