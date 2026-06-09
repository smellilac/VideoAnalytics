namespace VideoAnalytics.Api.Endpoints.DatasetLifecycle;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using VideoAnalytics.Api.Infrastructure;
using Application.Datasets.RegisterArtifact;

public sealed class RegisterArtifactEndpoint : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/datasets/{id:guid}/artifacts", HandleAsync)
           .WithName("RegisterArtifact")
           .WithTags("Datasets")
           .Produces<RegisterArtifactResponse>(StatusCodes.Status201Created)
           .Produces<RegisterArtifactResponse>(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status404NotFound)
           .Produces(StatusCodes.Status422UnprocessableEntity)
           .ProducesValidationProblem()
           .WithOpenApi();
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        [FromBody] RegisterArtifactRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new RegisterArtifactCommand(id, request.S3Key, request.ArtifactType, request.SizeBytes, request.RowCount);
        var result = await mediator.Send(command, cancellationToken);
        return result.MatchFirst<IResult>(
            value => TypedResults.Created($"/api/datasets/{value.DatasetId}/artifacts/{value.ArtifactId}", value),
            error => error.ToHttpResult());
    }
}

public sealed record RegisterArtifactRequest(
    string S3Key,
    string ArtifactType,
    long SizeBytes,
    long RowCount);
