#nullable enable

using System;

using Backlog;
using Backlog.Options;

using Microsoft.Extensions.DependencyInjection;

using test.backlog.EndToEndTests;

using Xunit;

namespace test.backlog;

public class TestProgram : BaseEndToEndTests // This isn't an end to end test but it does touch static state shared with the end to end tests
{
    public TestProgram(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        // Ensure that we're not running with test dependency injections
        Environment.SetEnvironmentVariable("IS_TEST", null);
    }

    protected override void Dispose(bool disposing)
    {
        //Set IS_TEST to true so that the main dispose can clean up any state changes
        Environment.SetEnvironmentVariable("IS_TEST", "true");
        base.Dispose(disposing);
    }

    [Fact]
    public void ConfigureDependencyInjection_RegistersSystemTimeProvider()
    {
        var services = new ServiceCollection();

        Backlog.Program.ConfigureDependencyInjection(services);

        using var serviceProvider = services.BuildServiceProvider();
        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, timeProvider);
    }

    [Fact]
    public void ConfigureDependencyInjection_WhenIsDryRunTrue_RegistersDryRunBucket()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<BacklogParserOptions>(o => o.IsDryRun = true);

        Backlog.Program.ConfigureDependencyInjection(services, isDryRun: true);

        using var provider = services.BuildServiceProvider();
        var bucket = provider.GetRequiredService<IBucket>();

        Assert.IsType<DryRunBucket>(bucket);
    }

    [Fact]
    public void ConfigureDependencyInjection_WhenIsDryRunFalse_RegistersBucket()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<BacklogParserOptions>(o => o.IsDryRun = false);

        Backlog.Program.ConfigureDependencyInjection(services, isDryRun: false);

        using var provider = services.BuildServiceProvider();
        var bucket = provider.GetRequiredService<IBucket>();

        Assert.IsType<Bucket>(bucket);
    }
}
