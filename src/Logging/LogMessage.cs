using System;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments;

public struct LogMessage
{
    public DateTime Timestamp { get; internal init; }

    [JsonPropertyName("class")]
    public string Category { get; internal init; }

    public LogLevel Level { get; internal init; }

    public string Message { get; internal init; }
}
