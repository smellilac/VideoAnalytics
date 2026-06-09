namespace VideoAnalytics.Domain.Outbox;

public static class OutboxMessageTypes
{
    public const string DatasetStatusChanged = "dataset.status.changes";
    public const string DatasetReady = "dataset.ready";
}
