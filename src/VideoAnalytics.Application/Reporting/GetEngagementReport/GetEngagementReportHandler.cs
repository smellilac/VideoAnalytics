namespace VideoAnalytics.Application.Reporting.GetEngagementReport;

using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Datasets.Common;
using VideoAnalytics.Application.Interfaces;

public sealed class GetEngagementReportHandler(
    IDatasetRepository datasetRepository,
    IReportRepository reportRepository,
    ICacheService cacheService,
    ILogger<GetEngagementReportHandler> logger)
    : IQueryHandler<GetEngagementReportQuery, ErrorOr<GetEngagementReportResponse>>
{
    private const string RelevantName = "engagement_metrics";

    public async ValueTask<ErrorOr<GetEngagementReportResponse>> Handle(
        GetEngagementReportQuery query,
        CancellationToken cancellationToken)
    {
        var issues = await datasetRepository.CheckRangeReadinessAsync(
            RelevantName, query.DateFrom, query.DateTo, cancellationToken);

        if (issues.Count > 0)
        {
            logger.LogWarning(
                "Engagement report for platform {Platform} [{DateFrom}–{DateTo}] blocked: {IssueCount} unready dataset(s)",
                query.Platform, query.DateFrom, query.DateTo, issues.Count);
            return DatasetErrors.DataNotReady(issues);
        }

        var cacheKey = $"report:engagement:{query.Platform}:{query.DateFrom:yyyy-MM-dd}:{query.DateTo:yyyy-MM-dd}:{query.Limit}";

        var cached = await cacheService.GetAsync<GetEngagementReportResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            logger.LogInformation(
                "Engagement report cache hit for platform {Platform} [{DateFrom}–{DateTo}]",
                query.Platform, query.DateFrom, query.DateTo);
            return cached;
        }

        var metrics = await reportRepository.GetEngagementMetricsAsync(
            query.Platform, query.DateFrom, query.DateTo, query.Limit, cancellationToken);

        var response = new GetEngagementReportResponse(
            query.Platform,
            query.DateFrom,
            query.DateTo,
            metrics,
            metrics.Count);

        await cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5), cancellationToken);

        logger.LogInformation(
            "Engagement report served from ClickHouse for platform {Platform} [{DateFrom}–{DateTo}]: {Count} records",
            query.Platform, query.DateFrom, query.DateTo, metrics.Count);

        return response;
    }
}
