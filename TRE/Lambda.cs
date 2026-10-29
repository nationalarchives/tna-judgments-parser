using System;
using System.IO;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TRE.FindCaseLaw;
using TRE.FindCaseLaw.Inputs;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Api = UK.Gov.NationalArchives.Judgments.Api;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace UK.Gov.NationalArchives.CaseLaw.TRE;

public class Lambda
{
    private readonly IHost appHost;

    public Lambda()
    {
        appHost = CreateAppHost();

        // This logging setup is bad because we are altering a static field in a non-static context.
        // Ideally we would be delegating all logger creation to the apphost's dependency injection framework however a
        // lot of the parser runs in a static context and does not use DI so this is not yet possible.
        // This hack must remain until all parts of the FCL parser no longer use static logger creation.
        Logging.Factory = appHost.Services.GetRequiredService<ILoggerFactory>();
    }

    public Stream FunctionHandler(Stream input)
    {
        using var scope = appHost.Services.CreateScope();

        var worker = scope.ServiceProvider.GetRequiredService<Worker>();

        return worker.Process(input);
    }

    private static IHost CreateAppHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddScoped<IValidator, Validator>();
        builder.Services.AddScoped<Api.IParser, Api.Parser>();
        builder.Services.AddScoped<Worker>();
        builder.Services.AddScoped<IInputHelper, InputHelper>();
        builder.Services.AddScoped<IExternalResourceRetriever, ExternalResourceRetriever>();

        builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
        builder.Services.AddSingleton(TimeProvider.System);

        // The CustomLoggerProvider is a singleton that needs to be registered under multiple interfaces so instantiate
        // it here and register the object directly under the required interfaces
        var customLoggerProvider = new CustomLoggerProvider();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider>(customLoggerProvider));
        builder.Services.AddSingleton<ICustomLoggerProvider>(customLoggerProvider);

        return builder.Build();
    }
}
