#nullable enable

using System;

using Microsoft.Extensions.DependencyInjection;

using test.backlog.EndToEndTests;

using Xunit;

namespace test.backlog;

public class TestProgram(ITestOutputHelper testOutputHelper)
    : BaseEndToEndTests(
        testOutputHelper) // This isn't an end to end test but it does touch static state shared with the end to end tests
{
    protected override void Dispose(bool disposing)
    {
        //Set IS_TEST to true so that the main dispose can clean up any state changes
        Environment.SetEnvironmentVariable("IS_TEST", "true");
        base.Dispose(disposing);
    }

    [Fact]
    public void ConfigureDependencyInjection_RegistersSystemTimeProvider()
    {
        // Ensure that we're not running with test dependency injections
        Environment.SetEnvironmentVariable("IS_TEST", null);

        var services = new ServiceCollection();

        Backlog.Src.Program.ConfigureDependencyInjection(services);

        using var serviceProvider = services.BuildServiceProvider();
        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, timeProvider);
    }
}
