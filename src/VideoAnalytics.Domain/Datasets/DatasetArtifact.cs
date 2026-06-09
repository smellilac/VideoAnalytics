namespace VideoAnalytics.Domain.Datasets;

public sealed class DatasetArtifact
{
    // Required by EF Core
    private DatasetArtifact() { }

    public Guid Id { get; private set; }
    public Guid DatasetId { get; private set; }
    public string S3Key { get; private set; } = string.Empty;
    public string ArtifactType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public long RowCount { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
}
