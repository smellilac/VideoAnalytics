namespace VideoAnalytics.Application.Datasets.CheckReadiness;

using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Datasets.Common;
using VideoAnalytics.Application.Interfaces;

public sealed class CheckReadinessHandler(
    IDatasetRepository repository,
    ILogger<CheckReadinessHandler> logger)
    : IQueryHandler<CheckReadinessQuery, ErrorOr<CheckReadinessResponse>>
{
    public async ValueTask<ErrorOr<CheckReadinessResponse>> Handle(
        CheckReadinessQuery query,
        CancellationToken cancellationToken)
    {
        var dataset = await repository.GetByIdAsync(query.DatasetId, cancellationToken);
        if (dataset is null)
            return DatasetErrors.NotFound(query.DatasetId);

        var result = await repository.CheckReadinessAsync(query.DatasetId, cancellationToken);

        logger.LogInformation(
            "Readiness check for dataset {DatasetId}: IsReady={IsReady}",
            query.DatasetId, result.IsReady);

        return new CheckReadinessResponse(result.IsReady, result.Reason);
    }
}
