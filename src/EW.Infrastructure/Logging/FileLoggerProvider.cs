using System.Text;
using Microsoft.Extensions.Logging;

namespace EW.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public FileLoggerProvider(string directory, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        Directory.CreateDirectory(directory);
        _writer = new StreamWriter(Path.Combine(directory, fileName), append: true, new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    internal void Write(LogLevel level, string categoryName, string message)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {categoryName}: {message}";
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Dispose();
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);
            var message = formatter(state, exception);
            if (exception is not null)
            {
                message = $"{message}{Environment.NewLine}{exception}";
            }

            provider.Write(logLevel, categoryName, message);
        }
    }
}
