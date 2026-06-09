namespace VideoAnalytics.Infrastructure;

using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Infrastructure.Cache;
using VideoAnalytics.Infrastructure.Kafka;
using VideoAnalytics.Infrastructure.Persistence;
using VideoAnalytics.Infrastructure.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgreSQL")));

        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IEventPublisher, NullEventPublisher>();
        services.AddSingleton<ICacheService, NullCacheService>();
        services.AddSingleton<IArtifactStorage, NullArtifactStorage>();
        services.AddHostedService<OutboxPublisher>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(name: "postgresql", tags: ["ready"])
            .AddRedis(
                configuration.GetConnectionString("Redis") ?? "localhost:6379",
                name: "redis",
                tags: ["ready"])
            .AddKafka(
                new ProducerConfig
                {
                    BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092"
                },
                name: "kafka",
                tags: ["ready"]);

        return services;
    }
}
