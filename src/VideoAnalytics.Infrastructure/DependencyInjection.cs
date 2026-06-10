namespace VideoAnalytics.Infrastructure;

using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using StackExchange.Redis;
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

        // Kafka
        services.AddOptions<KafkaSettings>()
            .BindConfiguration("Kafka")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

        // MinIO
        services.AddOptions<MinioSettings>()
            .BindConfiguration("Minio")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddSingleton<IMinioClient>(sp =>
        {
            var s = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
            return new MinioClient()
                .WithEndpoint(s.Endpoint)
                .WithCredentials(s.AccessKey, s.SecretKey)
                .WithSSL(s.UseSSL)
                .Build();
        });
        services.AddSingleton<IArtifactStorage, MinioArtifactStorage>();

        services.AddSingleton<ICacheService, NullCacheService>();
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
