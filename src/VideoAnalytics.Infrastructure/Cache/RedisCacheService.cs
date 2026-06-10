using System.Buffers;

namespace VideoAnalytics.Infrastructure.Cache;

using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;
using VideoAnalytics.Application.Interfaces;

internal sealed class RedisCacheService : ICacheService, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IDatabase _db;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly ResiliencePipeline _pipeline;

    public RedisCacheService(
        IConnectionMultiplexer multiplexer,
        IOptions<RedisSettings> options,
        ILogger<RedisCacheService> logger)
    {
        _multiplexer = multiplexer;
        _db = multiplexer.GetDatabase();
        _settings = options.Value;
        _logger = logger;
        _pipeline = BuildPipeline(_settings, _logger);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var prefixedKey = PrefixKey(key);
        try
        {
            var raw = await _pipeline.ExecuteAsync<RedisValue>(
                async ct => await _db.StringGetAsync(prefixedKey), cancellationToken);

            if (raw.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss for key {Key}", prefixedKey);
                return default;
            }

            _logger.LogDebug("Cache hit for key {Key}", prefixedKey);
            return Deserialize<T>((byte[])raw!);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or BrokenCircuitException)
        {
            _logger.LogWarning(ex, "Redis unavailable on GetAsync for key {Key} — returning null", prefixedKey);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry, CancellationToken cancellationToken)
    {
        var prefixedKey = PrefixKey(key);
        var (rentedArray, length) = Serialize(value);
    
        try
        {
            var redisValue = (RedisValue)rentedArray[..length];
        
            await _pipeline.ExecuteAsync(
                async ct => await _db.StringSetAsync(prefixedKey, redisValue, expiry),
                cancellationToken);

            _logger.LogDebug("Set cache key {Key} TTL {Ttl}", prefixedKey, expiry);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or BrokenCircuitException)
        {
            _logger.LogWarning(ex,
                "Redis unavailable on SetAsync for key {Key} — skipping cache write", prefixedKey);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedArray);
        }
    }

    public async Task InvalidateAsync(Guid datasetId, CancellationToken cancellationToken)
    {
        // Invalidates only report:summary:{datasetId} — engagement/trends expire via TTL
        var pattern = $"{_settings.InstanceName}report:summary:{datasetId}";
        try
        {
            // Single-node Redis only — for Redis Cluster, iterate over all servers
            var server = _multiplexer.GetServers().FirstOrDefault(s => s.IsConnected);
            if (server is null)
            {
                _logger.LogWarning(
                    "No connected Redis server available during InvalidateAsync for dataset {DatasetId}", datasetId);
                return;
            }

            await _pipeline.ExecuteAsync(async ct =>
            {
                await foreach (var redisKey in server.KeysAsync(pattern: pattern).WithCancellation(ct))
                {
                    await _db.KeyDeleteAsync(redisKey);
                    _logger.LogDebug("Invalidated cache key {Key}", (string?)redisKey);
                }
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or BrokenCircuitException)
        {
            _logger.LogWarning(ex,
                "Redis unavailable on InvalidateAsync for dataset {DatasetId} — cache not invalidated", datasetId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _multiplexer.CloseAsync();
        _multiplexer.Dispose();
    }

    private string PrefixKey(string key) =>
        string.IsNullOrEmpty(_settings.InstanceName) ? key : $"{_settings.InstanceName}{key}";

    private (byte[] array, int length) Serialize<T>(T value)
    {
        var writer = new ArrayBufferWriter<byte>(initialCapacity: 512);
    
        using var jsonWriter = new Utf8JsonWriter(writer);
        JsonSerializer.Serialize(jsonWriter, value);
    
        var jsonBytes = writer.WrittenSpan;

        if (jsonBytes.Length <= _settings.CompressionThresholdBytes)
        {
            var rented = ArrayPool<byte>.Shared.Rent(1 + jsonBytes.Length);
            rented[0] = 0x00;
            jsonBytes.CopyTo(rented.AsSpan(1));
            return (rented, 1 + jsonBytes.Length);
        }

        var rentedCompressed = ArrayPool<byte>.Shared.Rent(1 + jsonBytes.Length);
        rentedCompressed[0] = 0x01;
        using var ms = new MemoryStream(rentedCompressed, 1, rentedCompressed.Length - 1);
        using (var gzip = new GZipStream(ms, CompressionLevel.Fastest))
            gzip.Write(jsonBytes);
        return (rentedCompressed, 1 + (int)ms.Position);
    }

    private static T? Deserialize<T>(byte[] data)
    {
        if (data[0] == 0x01)
        {
            using var input = new MemoryStream(data, 1, data.Length - 1);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<T>(gzip);
        }

        // 0x00 prefix = uncompressed JSON
        return JsonSerializer.Deserialize<T>(data.AsSpan(1));
    }

    private static ResiliencePipeline BuildPipeline(
        RedisSettings settings, ILogger<RedisCacheService> logger) =>
        new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakerBreakDurationSeconds),
                SamplingDuration = TimeSpan.FromSeconds(settings.CircuitBreakerSamplingDurationSeconds),
                ShouldHandle = static args => ValueTask.FromResult(
                    args.Outcome.Exception is RedisException or TimeoutException),
                OnOpened = args =>
                {
                    logger.LogWarning(
                        "Redis circuit breaker opened for {Duration}s — cache requests will fast-fail",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogInformation("Redis circuit breaker half-open — testing Redis connectivity");
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Redis circuit breaker closed — cache requests will resume");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
}
