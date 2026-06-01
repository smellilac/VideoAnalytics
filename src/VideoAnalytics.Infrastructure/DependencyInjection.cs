namespace VideoAnalytics.Infrastructure;

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
        
        // TODO Check scoped dependencies (maybe would appear later)
        services.AddScoped<IEventPublisher, NullEventPublisher>();
        services.AddSingleton<ICacheService, NullCacheService>();
        services.AddSingleton<IArtifactStorage, NullArtifactStorage>();

        return services;
    }
}
