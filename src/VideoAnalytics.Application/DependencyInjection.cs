namespace VideoAnalytics.Application;

using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using VideoAnalytics.Application.Common;
using VideoAnalytics.Application.Datasets.RegisterDataset;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator();
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssemblyContaining<RegisterDatasetValidator>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
