using System.IO;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments;

public class Logging
{
    public static ILoggerFactory Factory = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
    });

    public static void SetConsoleAndFile(FileInfo file, LogLevel level = LogLevel.Information)
    {
        Factory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .AddFile(file.FullName, level)
                .SetMinimumLevel(level);
        });
    }
}
