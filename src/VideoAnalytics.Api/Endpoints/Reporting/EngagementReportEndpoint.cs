namespace VideoAnalytics.Api.Endpoints.Reporting;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using VideoAnalytics.Api.Infrastructure;
using VideoAnalytics.Application.Reporting.GetEngagementReport;

public sealed class EngagementReportEndpoint : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/engagement", HandleAsync)
           .WithName("GetEngagementReport")
           .WithTags("Reporting")
           .Produces<GetEngagementReportResponse>(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status503ServiceUnavailable)
           .ProducesValidationProblem()
           .WithOpenApi();
    }

    private static async Task<IResult> HandleAsync(
        IMediator mediator,
        CancellationToken cancellationToken,
        [FromQuery] string platform,
        [FromQuery] DateOnly dateFrom,
        [FromQuery] DateOnly dateTo,
        [FromQuery] int limit = 100)
    {
        var query = new GetEngagementReportQuery(platform, dateFrom, dateTo, limit);
        var result = await mediator.Send(query, cancellationToken);
        return result.MatchFirst<IResult>(
            response => TypedResults.Ok(response),
            error => error.ToHttpResult());
    }
}
