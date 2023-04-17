
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments {

public class Logging {

    public static ILoggerFactory Factory = LoggerFactory.Create(builder => { builder.SetMinimumLevel(LogLevel.Information); });

    public static void SetConsoleAndFile(FileInfo file, LogLevel level = LogLevel.Information) {
        Factory = LoggerFactory.Create(builder => { builder
            .AddConsole()
            .AddFile(file.FullName, level)
            .SetMinimumLevel(level);
        });
    }

    public static void SetConsole(LogLevel level) {
        Factory = LoggerFactory.Create(builder => { builder
            .AddConsole()
            .SetMinimumLevel(level);
        });
    }

    public static void SetFile(FileInfo file, LogLevel level = LogLevel.Information) {
        if (file is null)
            Factory = LoggerFactory.Create(builder => { builder.SetMinimumLevel(LogLevel.None); });
        else
            Factory = LoggerFactory.Create(builder => { builder
                .AddFile(file.FullName)
                .SetMinimumLevel(level);
        });
    }

}

public struct LogMessage {

    public DateTime Timestamp { get; internal init; }

    [JsonPropertyName("class")]
    public string Category { get; internal init; }

    public LogLevel Level { get; internal init; }

    public string Message { get; internal init; }

}

public sealed class CustomLoggerProvider : ILoggerProvider {

    private readonly List<LogMessage> Messages = new List<LogMessage>();

    private readonly LogLevel Minimum;

    public CustomLoggerProvider(LogLevel minimum) {
        Minimum = minimum;
    }
    public CustomLoggerProvider() : this(LogLevel.Information) { }

    public IList<LogMessage> Reset() {
        var copy = new List<LogMessage>(Messages);
        Messages.Clear();
        return copy;
    }

    public ILogger CreateLogger(string category) {
        return new CustomLogger(Minimum, category, Messages);
    }

    public void Dispose() {
        Messages.Clear();
    }

    public static readonly JsonSerializerOptions options = new JsonSerializerOptions() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string ToJson(IList<LogMessage> messages) => JsonSerializer.Serialize(messages, options);

}

internal sealed class CustomLogger : ILogger {

    private readonly LogLevel Minimum;

    private readonly string Category;

    private readonly List<LogMessage> Messages;

    internal CustomLogger(LogLevel minimum, string category, List<LogMessage> messages) {
        Minimum = minimum;
        Category = category;
        Messages = messages;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull {
        return default;
    }

    public bool IsEnabled(LogLevel logLevel) {
        if (logLevel == LogLevel.None)
            return false;
        return logLevel >= Minimum;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
        if (!IsEnabled(logLevel))
            return;
        var text = formatter(state, exception);
        var message = new LogMessage {
            Timestamp = DateTime.UtcNow,
            Category = Category,
            Level = logLevel,
            Message = text
        };
        Messages.Add(message);
    }

}

}
