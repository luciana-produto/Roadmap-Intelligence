using Microsoft.Extensions.Logging;

namespace ProductHub.Application.Tests.Common;

public sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _logs = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Logs => _logs.AsReadOnly();

    public bool HasWarning(string contains) =>
        _logs.Any(l => l.Level == LogLevel.Warning && l.Message.Contains(contains));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _logs.Add((logLevel, formatter(state, exception)));
}
