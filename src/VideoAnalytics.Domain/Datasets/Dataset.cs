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

    public void TransitionTo(DatasetStatus newStatus, TimeProvider timeProvider, string? message = null)
    {
        if (!DatasetStatusTransitions.IsAllowed(Status, newStatus))
            throw new InvalidOperationException($"Transition from {Status} to {newStatus} is not allowed.");

        if (newStatus == DatasetStatus.Failed)
            ErrorMessage = message;

        if (Status == DatasetStatus.Failed && newStatus == DatasetStatus.Pending)
            ErrorMessage = null;

        if (newStatus == DatasetStatus.Ready)
            CompletedAt = timeProvider.GetUtcNow();

        Status = newStatus;
        UpdatedAt = timeProvider.GetUtcNow();
    }
}
