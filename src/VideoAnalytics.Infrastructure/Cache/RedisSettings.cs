namespace VideoAnalytics.Infrastructure.Cache;

using System.ComponentModel.DataAnnotations;

internal sealed class RedisSettings
{
    [Required]
    public string ConnectionString { get; init; } = string.Empty;
    public string InstanceName { get; init; } = "videoanalytics:";
    public int CircuitBreakerMinimumThroughput { get; init; } = 3;
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;
    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 60;
    public int CompressionThresholdBytes { get; init; } = 1024;
}
