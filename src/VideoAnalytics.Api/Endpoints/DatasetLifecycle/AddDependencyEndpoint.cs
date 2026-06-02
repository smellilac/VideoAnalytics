namespace VideoAnalytics.Api.Endpoints.DatasetLifecycle;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using VideoAnalytics.Api.Infrastructure;
using Application.Datasets.AddDependency;

public sealed class AddDependencyEndpoint : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/datasets/{id:guid}/dependencies", HandleAsync)
           .WithName("AddDependency")
           .WithTags("Datasets")
           .Produces(StatusCodes.Status204NoContent)
           .Produces(StatusCodes.Status404NotFound)
           .Produces(StatusCodes.Status409Conflict)
           .ProducesValidationProblem()
           .WithOpenApi();
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        [FromBody] AddDependencyRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AddDependencyCommand(id, request.DependsOnDatasetId);
        var result = await mediator.Send(command, cancellationToken);
        return result.MatchFirst<IResult>(
            _ => TypedResults.NoContent(),
            error => error.ToHttpResult());
    }
}

public sealed record AddDependencyRequest(Guid DependsOnDatasetId);
