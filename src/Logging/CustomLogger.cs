using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments;

internal sealed class CustomLogger : ILogger
{
    private readonly LogLevel Minimum;

    private readonly string Category;

    private readonly List<LogMessage> Messages;

    internal CustomLogger(LogLevel minimum, string category, List<LogMessage> messages)
    {
        Minimum = minimum;
        Category = category;
        Messages = messages;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return default;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
        {
            return false;
        }

        return logLevel >= Minimum;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var text = formatter(state, exception);
        var message = new LogMessage
        {
            Timestamp = DateTime.UtcNow,
            Category = Category,
            Level = logLevel,
            Message = text
        };
        Messages.Add(message);
    }
}
