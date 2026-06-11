using System.Text.Json.Nodes;

namespace VideoAnalytics.Domain.Datasets;

using System.Text.Json;

public sealed class Dataset
{
    // Required by EF Core
    private Dataset() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string PipelineRunId { get; private set; } = string.Empty;
    public DatasetStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public JsonDocument? Metadata { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static Dataset Create(
        string name,
        string version,
        string pipelineRunId,
        JsonDocument? metadata,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Version = version,
            PipelineRunId = pipelineRunId,
            Metadata = metadata,
            Status = DatasetStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void TransitionTo(DatasetStatus newStatus, DateTimeOffset now, string? message = null)
    {
        if (!DatasetStatusTransitions.IsAllowed(Status, newStatus))
            throw new InvalidOperationException($"Transition from {Status} to {newStatus} is not allowed.");

        if (newStatus == DatasetStatus.Failed)
            ErrorMessage = message;

        if (Status == DatasetStatus.Failed && newStatus == DatasetStatus.Pending)
            ErrorMessage = null;

        if (newStatus == DatasetStatus.Ready)
            CompletedAt = now;

        Status = newStatus;
        UpdatedAt = now;
    }
    
    public void MergeMetadata(JsonDocument? incoming)
    {
        if (incoming is null)
            return;

        JsonObject merged = Metadata is { RootElement.ValueKind: JsonValueKind.Object }
            ? JsonNode.Parse(Metadata.RootElement.GetRawText())!.AsObject()
            : new JsonObject();

        var incomingNode = JsonNode.Parse(incoming.RootElement.GetRawText())!.AsObject();
        foreach (var (key, value) in incomingNode)
            merged[key] = value?.DeepClone();

        var newMetadata = JsonDocument.Parse(merged.ToJsonString());

        Metadata?.Dispose();   
        Metadata = newMetadata;
    }
}
