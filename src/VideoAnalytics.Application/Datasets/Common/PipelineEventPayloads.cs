namespace VideoAnalytics.Application.Datasets.Common;

using System.Text.Json;
using System.Text.Json.Serialization;
using VideoAnalytics.Domain.Datasets;

public sealed record PipelineEventEnvelope(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    JsonElement Data);

// → RegisterDatasetCommand
public sealed record DatasetRegisteredData(
    string Name,
    string Version,
    string PipelineRunId,
    JsonDocument? Metadata);

// → UpdateDatasetStatusCommand
// Name is NOT "StatusChangedPayload" — that name is taken by the outgoing OutboxPayloads
public sealed record DatasetStatusUpdateData(
    Guid DatasetId,
    DatasetStatus NewStatus,
    string? Message,
    JsonDocument? Metadata);

// → RegisterArtifactCommand
public sealed record DatasetArtifactRegisteredData(
    Guid DatasetId,
    [property: JsonPropertyName("s3_key")] string S3Key,
    string ArtifactType,
    long SizeBytes,
    long RowCount);

// → AddDependencyCommand
public sealed record DatasetDependencyAddedData(
    Guid DatasetId,
    Guid DependsOnDatasetId);
