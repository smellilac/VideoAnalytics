using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using VideoAnalytics.Api;
using VideoAnalytics.Api.Endpoints;
using VideoAnalytics.Api.Infrastructure;
using VideoAnalytics.Application;
using VideoAnalytics.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) =>
        config.ReadFrom.Configuration(context.Configuration)
              .ReadFrom.Services(services));

    builder.Services.AddOpenApi();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    
    // Explicit despite being the .NET 8+ default — a faulted BackgroundService (e.g. PipelineEventConsumer
    // on a fatal Kafka client error) must bring down the whole host so Kubernetes restarts the pod.
    // Do NOT change to Ignore: a silently-dead Kafka consumer with a healthy HTTP API
    // is a worse failure mode than a visible crash loop.
    builder.Services.Configure<HostOptions>(options =>
    {
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
    });

    var endpointTypes = Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false }
                    && t.IsAssignableTo(typeof(IEndpointGroup)));

    foreach (var type in endpointTypes)
        builder.Services.AddSingleton(typeof(IEndpointGroup), type);
    
    builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    app.UseExceptionHandler();

    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });

    app.MapEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
