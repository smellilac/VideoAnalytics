namespace VideoAnalytics.Domain.Datasets;

public sealed record ReadinessResult(bool IsReady, string? Reason = null)
{
    public static ReadinessResult Ready() => new(true);
    public static ReadinessResult NotReady(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(false, reason);
    }
}
