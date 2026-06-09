namespace VideoAnalytics.Api.Endpoints.DatasetLifecycle;

using Mediator;
using VideoAnalytics.Api.Infrastructure;
using VideoAnalytics.Application.Datasets.ResetDataset;

public sealed class ResetDatasetEndpoint : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/datasets/{id:guid}/reset", HandleAsync)
           .WithName("ResetDataset")
           .WithTags("Datasets")
           .Produces(StatusCodes.Status204NoContent)
           .Produces(StatusCodes.Status404NotFound)
           .Produces(StatusCodes.Status422UnprocessableEntity)
           .WithOpenApi();
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new ResetDatasetCommand(id);
        var result = await mediator.Send(command, cancellationToken);
        return result.MatchFirst<IResult>(
            _ => TypedResults.NoContent(),
            error => error.ToHttpResult());
    }
}
