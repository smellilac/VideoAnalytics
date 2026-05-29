namespace VideoAnalytics.Api.Endpoints.DatasetLifecycle;

using System.Text.Json;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using VideoAnalytics.Application.Datasets.RegisterDataset;

public sealed class RegisterDatasetEndpoint : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/datasets", HandleAsync)
           .WithName("RegisterDataset")
           .WithTags("Datasets")
           .Produces<RegisterDatasetResponse>(StatusCodes.Status201Created)
           .Produces(StatusCodes.Status409Conflict)
           .ProducesValidationProblem()
           .WithOpenApi();
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] RegisterDatasetRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new RegisterDatasetCommand(
            request.Name,
            request.Version,
            request.PipelineRunId,
            request.Metadata);
        var response = await mediator.Send(command, cancellationToken);
        return TypedResults.Created($"/api/datasets/{response.DatasetId}", response);
    }
}

public sealed record RegisterDatasetRequest(
    string Name,
    string Version,
    string PipelineRunId,
    JsonDocument? Metadata = null);
