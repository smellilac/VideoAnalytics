namespace VideoAnalytics.Application.Datasets.CheckReadiness;

using ErrorOr;
using Mediator;

public sealed record CheckReadinessQuery(Guid DatasetId) : IQuery<ErrorOr<CheckReadinessResponse>>;

public sealed record CheckReadinessResponse(bool IsReady, string? Reason);
