#nullable enable

using System;
using System.Linq;

using Microsoft.Extensions.Logging;

using Moq;

namespace test.Mocks;

public class MockLogger<T> : Mock<ILogger<T>>
{
    public string GetLogMessageContaining(string partialMessage)
    {
        return Invocations.Where(i => i.Method.Name == nameof(ILogger.Log))
                          .Where(i => i.Arguments[2].ToString()!.Contains(partialMessage))
                          .Select(i => i.Arguments[2].ToString()!)
                          .Single();
    }

    public MockLogger<T> VerifyLog(string expectedMessage, LogLevel expectedLogLevel, Times? times = null)
    {
        times ??= Times.Once();

        Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == expectedLogLevel),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == expectedMessage),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            (Times)times);

        return this;
    }

    public MockLogger<T> VerifyLog<TException>(string expectedMessage, LogLevel expectedLogLevel, Times? times = null)
        where TException : Exception?
    {
        times ??= Times.Once();

        Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == expectedLogLevel),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<TException>(e => e != null && e.Message == expectedMessage),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            (Times)times);

        return this;
    }
}
