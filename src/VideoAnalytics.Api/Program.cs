using System.Reflection;
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

    var endpointTypes = Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false }
                    && t.IsAssignableTo(typeof(IEndpointGroup)));

    foreach (var type in endpointTypes)
        builder.Services.AddSingleton(typeof(IEndpointGroup), type);

    builder.Services.AddExceptionHandler<ConflictExceptionHandler>();
    builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    app.UseExceptionHandler();

    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference();

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
