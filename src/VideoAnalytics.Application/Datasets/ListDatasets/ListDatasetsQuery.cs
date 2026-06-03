namespace VideoAnalytics.Application.Datasets.ListDatasets;

using ErrorOr;
using Mediator;
using VideoAnalytics.Domain.Datasets;

public sealed record ListDatasetsQuery(
    DatasetStatus? Status,
    int Skip,
    int Take) : IQuery<ErrorOr<ListDatasetsResponse>>;

public sealed record ListDatasetsResponse(IReadOnlyList<DatasetSummary> Items, int Total);

public sealed record DatasetSummary(
    Guid Id,
    string Name,
    string Version,
    string PipelineRunId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
