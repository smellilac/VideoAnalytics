namespace VideoAnalytics.Tests.Fakes;

using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Application.Reporting.GetEngagementReport;

internal sealed class NullReportRepository : IReportRepository
{
    public Task<IReadOnlyList<EngagementMetricDto>> GetEngagementMetricsAsync(
        string platform,
        DateOnly dateFrom,
        DateOnly dateTo,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EngagementMetricDto>>([]);
}
