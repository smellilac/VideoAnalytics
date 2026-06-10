using Polly;
using Polly.CircuitBreaker;

namespace VideoAnalytics.Infrastructure.Storage;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using VideoAnalytics.Application.Interfaces;

internal sealed class MinioArtifactStorage : IArtifactStorage, IAsyncDisposable
{
    private readonly IMinioClient _minioClient;
    private readonly MinioSettings _settings;
    private readonly ILogger<MinioArtifactStorage> _logger;
    private readonly ResiliencePipeline _pipeline;

    public MinioArtifactStorage(
        IMinioClient minioClient,
        IOptions<MinioSettings> options,
        ILogger<MinioArtifactStorage> logger)
    {
        _minioClient = minioClient;
        _settings = options.Value;
        _logger = logger;
        _pipeline = BuildPipeline(_settings, _logger);
    }

    public async Task<bool> ExistsAsync(string s3Key, CancellationToken cancellationToken)
    {
        try
        {
            await _pipeline.ExecuteAsync(async ct =>
            {
                var args = new StatObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(s3Key);

                await _minioClient.StatObjectAsync(args, ct);
            }, cancellationToken);

            return true;
        }
        catch (ObjectNotFoundException)
        {
            _logger.LogDebug("Object {S3Key} not found in bucket {Bucket}", s3Key, _settings.BucketName);
            return false;
        }
        catch (BucketNotFoundException ex)
        {
            _logger.LogError(ex,
                "Bucket {Bucket} does not exist — verify MinIO bucket name in configuration",
                _settings.BucketName);
            throw;
        }
        catch (AuthorizationException ex)
        {
            _logger.LogError(ex,
                "MinIO authorization failed for bucket {Bucket} — verify AccessKey and SecretKey",
                _settings.BucketName);
            throw;
        }
        catch (ConnectionException ex)
        {
            _logger.LogError(ex,
                "MinIO network error checking {S3Key} in bucket {Bucket}",
                s3Key, _settings.BucketName);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_minioClient is IDisposable disposable)
            disposable.Dispose();
    
        return ValueTask.CompletedTask;
    }

    private static ResiliencePipeline BuildPipeline(
        MinioSettings settings, ILogger<MinioArtifactStorage> logger) =>
        new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakerBreakDurationSeconds),
                SamplingDuration = TimeSpan.FromSeconds(settings.CircuitBreakerSamplingDurationSeconds),
                ShouldHandle = static args => ValueTask.FromResult(
                    args.Outcome.Exception is ConnectionException or HttpRequestException),
                OnOpened = args =>
                {
                    logger.LogWarning(
                        "MinIO circuit breaker opened for {Duration}s — storage requests will fast-fail",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogInformation("MinIO circuit breaker half-open — testing storage connectivity");
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("MinIO circuit breaker closed — storage requests will resume");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
}
