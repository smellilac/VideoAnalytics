namespace VideoAnalytics.Application.Reporting.GetEngagementReport;

using ErrorOr;
using Mediator;

public sealed record GetEngagementReportQuery(
    string Platform,
    DateOnly DateFrom,
    DateOnly DateTo,
    int Limit = 100) : IQuery<ErrorOr<GetEngagementReportResponse>>;

public sealed record GetEngagementReportResponse(
    string Platform,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<EngagementMetricDto> Metrics,
    int Total);

public sealed record EngagementMetricDto(
    string VideoId,
    string Platform,
    DateTimeOffset RecordedAt,
    long Views,
    long Likes,
    long Comments,
    long Shares,
    double EngagementRate,
    string Category,
    IReadOnlyList<string> Tags);
