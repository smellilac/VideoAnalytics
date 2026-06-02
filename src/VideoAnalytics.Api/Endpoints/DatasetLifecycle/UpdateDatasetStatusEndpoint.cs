namespace VideoAnalytics.Api.Endpoints.DatasetLifecycle;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using VideoAnalytics.Api.Infrastructure;
using VideoAnalytics.Application.Datasets.UpdateStatus;
using VideoAnalytics.Domain.Datasets;

public sealed class UpdateDatasetStatusEndpoint : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/datasets/{id:guid}/status", HandleAsync)
           .WithName("UpdateDatasetStatus")
           .WithTags("Datasets")
           .Produces(StatusCodes.Status204NoContent)
           .Produces(StatusCodes.Status404NotFound)
           .Produces(StatusCodes.Status409Conflict)
           .Produces(StatusCodes.Status422UnprocessableEntity)
           .ProducesValidationProblem()
           .WithOpenApi();
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        [FromBody] UpdateDatasetStatusRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDatasetStatusCommand(id, request.NewStatus, request.Message);
        var result = await mediator.Send(command, cancellationToken);
        return result.MatchFirst<IResult>(
            _ => TypedResults.NoContent(),
            error => error.ToHttpResult());
    }
}

public sealed record UpdateDatasetStatusRequest(DatasetStatus NewStatus, string? Message = null);
