namespace VideoAnalytics.Domain.Outbox;

public sealed class OutboxMessage
{
    // Required by EF Core
    private OutboxMessage() { }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    public static OutboxMessage Create(string type, string payload, DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.CreateVersion7(createdAt),
            Type = type,
            Payload = payload,
            CreatedAt = createdAt
        };

    public void MarkProcessed(DateTimeOffset processedAt) => ProcessedAt = processedAt;

    public void MarkFailed(DateTimeOffset now, string error)
    {
        // TODO Check do I need Interlocked to increment here
        RetryCount++;
        Error = error;
    }
}
