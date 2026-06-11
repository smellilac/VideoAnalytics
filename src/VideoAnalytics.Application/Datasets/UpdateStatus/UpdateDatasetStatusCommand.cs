namespace VideoAnalytics.Application.Datasets.UpdateStatus;

using System.Text.Json;
using ErrorOr;
using Mediator;
using VideoAnalytics.Domain.Datasets;

public sealed record UpdateDatasetStatusCommand(
    Guid DatasetId,
    DatasetStatus NewStatus,
    string? Message = null,
    JsonDocument? Metadata = null) : ICommand<ErrorOr<Success>>;
