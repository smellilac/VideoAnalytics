namespace VideoAnalytics.Api.Endpoints;

using Microsoft.Extensions.DependencyInjection;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = app.ServiceProvider.GetServices<IEndpointGroup>();

        foreach (var group in groups)
            group.MapEndpoints(app);

        return app;
    }
}
