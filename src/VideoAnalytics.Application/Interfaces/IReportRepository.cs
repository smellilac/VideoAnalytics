namespace VideoAnalytics.Application.Interfaces;

using VideoAnalytics.Application.Reporting.GetEngagementReport;

public interface IReportRepository
{
    Task<IReadOnlyList<EngagementMetricDto>> GetEngagementMetricsAsync(
        string platform,
        DateOnly dateFrom,
        DateOnly dateTo,
        int limit,
        CancellationToken cancellationToken);
}
