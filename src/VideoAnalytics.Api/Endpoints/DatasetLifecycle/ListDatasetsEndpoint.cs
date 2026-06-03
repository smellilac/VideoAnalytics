using Microsoft.AspNetCore.Mvc;

namespace VideoAnalytics.Api.Endpoints.DatasetLifecycle;

using Mediator;
using VideoAnalytics.Api.Infrastructure;
using VideoAnalytics.Application.Datasets.ListDatasets;
using VideoAnalytics.Domain.Datasets;

public sealed class ListDatasetsEndpoint : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/datasets", HandleAsync)
           .WithName("ListDatasets")
           .WithTags("Datasets")
           .Produces<ListDatasetsResponse>(StatusCodes.Status200OK)
           .ProducesValidationProblem()
           .WithOpenApi();
    }

    private static async Task<IResult> HandleAsync(
        IMediator mediator,
        CancellationToken cancellationToken,
        [FromQuery] DatasetStatus? status = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        var query = new ListDatasetsQuery(status, skip, take);
        var result = await mediator.Send(query, cancellationToken);
        return result.MatchFirst<IResult>(
            value => TypedResults.Ok(value),
            error => error.ToHttpResult());
    }
}
