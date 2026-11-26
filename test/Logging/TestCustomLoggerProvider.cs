#nullable enable

using System;

using Microsoft.Extensions.Logging;

using Shouldly;

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace test.Logging;

public class TestCustomLoggerProvider
{
    [Fact]
    public void Log_WithException_RecordsExceptionDetails()
    {
        var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");
        var exception = new InvalidOperationException("something went wrong");

        logger.LogError(exception, "parse failed");

        var messages = provider.Reset();
        messages.Count.ShouldBe(1);
        messages[0].Level.ShouldBe(LogLevel.Error);
        messages[0].Message.ShouldBe("parse failed");
        messages[0].Exception.ShouldBe(exception.ToString());
        messages[0].Exception.ShouldContain("InvalidOperationException");
        messages[0].Exception.ShouldContain("something went wrong");
    }

    [Fact]
    public void Log_WithoutException_LeavesExceptionNull()
    {
        var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");

        logger.LogError("parse failed");

        var messages = provider.Reset();
        messages.Count.ShouldBe(1);
        messages[0].Exception.ShouldBeNull();
    }

    [Fact]
    public void Log_WithInformation_RecordsMessageAtInformationLevelWithoutException()
    {
        var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");

        logger.LogInformation("parsing {Filename}", "judgment.docx");

        var messages = provider.Reset();
        messages.Count.ShouldBe(1);
        messages[0].Level.ShouldBe(LogLevel.Information);
        messages[0].Category.ShouldBe("TestCategory");
        messages[0].Message.ShouldBe("parsing judgment.docx");
        messages[0].Exception.ShouldBeNull();
    }

    [Fact]
    public void Log_WithCriticalAndException_RecordsExceptionDetailsAtCriticalLevel()
    {
        var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");
        var exception = new InvalidOperationException("unrecoverable failure");

        logger.LogCritical(exception, "parser crashed");

        var messages = provider.Reset();
        messages.Count.ShouldBe(1);
        messages[0].Level.ShouldBe(LogLevel.Critical);
        messages[0].Message.ShouldBe("parser crashed");
        messages[0].Exception.ShouldBe(exception.ToString());
    }

    [Fact]
    public void Log_WithInformationThenCriticalAndException_RecordsBothInOrderWithOnlyCriticalHavingException()
    {
        var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");
        var exception = new InvalidOperationException("unrecoverable failure");

        logger.LogInformation("[STAGE] Parsing document");
        logger.LogCritical(exception, "parser crashed");

        var messages = provider.Reset();
        messages.Count.ShouldBe(2);
        messages[0].Level.ShouldBe(LogLevel.Information);
        messages[0].Message.ShouldBe("[STAGE] Parsing document");
        messages[0].Exception.ShouldBeNull();
        messages[1].Level.ShouldBe(LogLevel.Critical);
        messages[1].Message.ShouldBe("parser crashed");
        messages[1].Exception.ShouldBe(exception.ToString());
    }

    [Fact]
    public void ToJson_WithInformationAndCritical_SerializesLevelsAsCamelCaseStrings()
    {
        var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");

        logger.LogInformation("[STAGE] Parsing document");
        logger.LogCritical("parser crashed");

        var json = CustomLoggerProvider.ToJson(provider.Reset());
        json.ShouldContain("\"level\":\"information\"");
        json.ShouldContain("\"level\":\"critical\"");
    }

    [Fact]
    public void ToJson_WithException_IncludesExceptionProperty()
    {
        var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");

        logger.LogError(new InvalidOperationException("something went wrong"), "parse failed");

        var json = CustomLoggerProvider.ToJson(provider.Reset());
        json.ShouldContain("\"exception\":");
        json.ShouldContain("something went wrong");
    }

    [Fact]
    public void ToJson_WithoutException_OmitsExceptionProperty()
    {
        var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");

        logger.LogError("parse failed");

        var json = CustomLoggerProvider.ToJson(provider.Reset());
        json.ShouldNotContain("exception");
    }
}
