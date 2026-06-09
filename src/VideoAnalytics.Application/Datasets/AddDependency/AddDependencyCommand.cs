namespace VideoAnalytics.Application.Datasets.AddDependency;

using ErrorOr;
using Mediator;

public sealed record AddDependencyCommand(
    Guid DatasetId,
    Guid DependsOnDatasetId) : ICommand<ErrorOr<Success>>;
