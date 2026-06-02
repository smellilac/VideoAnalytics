namespace VideoAnalytics.Api.Endpoints.DatasetLifecycle;

using Mediator;
using VideoAnalytics.Api.Infrastructure;
using VideoAnalytics.Application.Datasets.CheckReadiness;

public sealed class GetReadinessEndpoint : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/datasets/{id:guid}/readiness", HandleAsync)
           .WithName("GetDatasetReadiness")
           .WithTags("Datasets")
           .Produces<CheckReadinessResponse>(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status404NotFound)
           .WithOpenApi();
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new CheckReadinessQuery(id);
        var result = await mediator.Send(query, cancellationToken);
        return result.MatchFirst<IResult>(
            value => TypedResults.Ok(value),
            error => error.ToHttpResult());
    }
}
