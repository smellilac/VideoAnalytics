namespace VideoAnalytics.Infrastructure.Persistence;

public sealed class ClickHouseSettings
{
    public string ConnectionString { get; init; } = string.Empty;
}