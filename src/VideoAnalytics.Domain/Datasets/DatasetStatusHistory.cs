namespace VideoAnalytics.Domain.Datasets;

public sealed class DatasetStatusHistory
{
    // Required by EF Core
    private DatasetStatusHistory() { }

    public Guid Id { get; private set; }
    public Guid DatasetId { get; private set; }
    public DatasetStatus FromStatus { get; private set; }
    public DatasetStatus ToStatus { get; private set; }
    public string? Message { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
