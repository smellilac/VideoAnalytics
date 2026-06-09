namespace VideoAnalytics.Application.Datasets.UpdateStatus;

using ErrorOr;
using Mediator;
using VideoAnalytics.Domain.Datasets;

public sealed record UpdateDatasetStatusCommand(
    Guid DatasetId,
    DatasetStatus NewStatus,
    string? Message = null) : ICommand<ErrorOr<Success>>;
