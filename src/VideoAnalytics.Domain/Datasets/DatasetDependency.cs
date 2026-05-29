namespace VideoAnalytics.Domain.Datasets;

public sealed record DatasetDependency(
    Guid DatasetId,
    Guid DependsOnDatasetId);
