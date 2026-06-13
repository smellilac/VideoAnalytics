namespace VideoAnalytics.Tests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Infrastructure.Persistence;
using VideoAnalytics.Tests.Fakes;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        await _postgres.StopAsync();
        await _redis.StopAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override app configuration so ValidateOnStart passes and health checks use the container
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = _redis.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
                // Satisfy ValidateOnStart; ClickHouseReportRepository is replaced below with a fake.
                ["ClickHouse:ConnectionString"] = "Host=localhost;Port=8123;Database=default"
            }));

        builder.ConfigureServices(services =>
        {
            // Replace PostgreSQL with the Testcontainers instance
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Replace ICacheService with a no-op fake — Redis is still wired for the health check
            // but cache operations in tests should not depend on cache state
            var cacheDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ICacheService));
            if (cacheDescriptor is not null)
                services.Remove(cacheDescriptor);
            services.AddSingleton<ICacheService, NullCacheService>();

            // Replace IReportRepository with a no-op fake — ClickHouse is not available in tests
            var reportDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IReportRepository));
            if (reportDescriptor is not null)
                services.Remove(reportDescriptor);
            services.AddSingleton<IReportRepository, NullReportRepository>();
        });
    }
}
