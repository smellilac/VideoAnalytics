namespace VideoAnalytics.Infrastructure.Storage;

using System.ComponentModel.DataAnnotations;

public sealed class MinioSettings
{
    public string Endpoint { get; init; } = "localhost:9000";

    [Required]
    public string AccessKey { get; init; } = string.Empty;

    [Required]
    public string SecretKey { get; init; } = string.Empty;
    public bool UseSSL { get; init; } = false;
    public string BucketName { get; init; } = "datasets";
    public int CircuitBreakerMinimumThroughput { get; init; } = 3;
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;
    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 60;
}
