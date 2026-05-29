namespace VideoAnalytics.Application.Datasets.RegisterDataset;

using System.Text.Json;
using Mediator;

public sealed record RegisterDatasetCommand(
    string Name,
    string Version,
    string PipelineRunId,
    JsonDocument? Metadata) : ICommand<RegisterDatasetResponse>;

public sealed record RegisterDatasetResponse(
    Guid DatasetId,
    string Name,
    string Version,
    string Status,
    DateTimeOffset CreatedAt);
