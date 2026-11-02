using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments;

public interface ICustomLoggerProvider : ILoggerProvider
{
    IList<LogMessage> Reset();
}

public sealed class CustomLoggerProvider : ICustomLoggerProvider
{
    private readonly List<LogMessage> Messages = new();

    private readonly LogLevel Minimum;

    public CustomLoggerProvider(LogLevel minimum)
    {
        Minimum = minimum;
    }

    public CustomLoggerProvider() : this(LogLevel.Information) { }

    public IList<LogMessage> Reset()
    {
        var copy = new List<LogMessage>(Messages);
        Messages.Clear();
        return copy;
    }

    public ILogger CreateLogger(string category)
    {
        return new CustomLogger(Minimum, category, Messages);
    }

    public void Dispose()
    {
        Messages.Clear();
    }

    public static readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string ToJson(IList<LogMessage> messages)
    {
        return JsonSerializer.Serialize(messages, options);
    }
}
