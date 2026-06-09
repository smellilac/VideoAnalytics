namespace VideoAnalytics.Infrastructure.Kafka;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Datasets.Common;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Outbox;
using VideoAnalytics.Infrastructure.Persistence;

internal sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;
    private const int MaxRetryCount = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox polling cycle failed");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken); 
        
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // SELECT FOR UPDATE SKIP LOCKED prevents concurrent processors from picking the same rows
        // when multiple instances of the service run (horizontal scaling).
        var messages = await dbContext.OutboxMessages
            .FromSql($"""
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND retry_count < {MaxRetryCount}
                ORDER BY created_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            var now = timeProvider.GetUtcNow();
            try
            {
                await DispatchAsync(message.Type, message.Payload, eventPublisher, cancellationToken);
                message.MarkProcessed(now);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId} of type {Type}", message.Id, message.Type);
                message.MarkFailed(now, ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task DispatchAsync(string type, string payload, IEventPublisher publisher, CancellationToken ct)
    {
        switch (type)
        {
            case OutboxMessageTypes.DatasetStatusChanged:
                var statusChanged = JsonSerializer.Deserialize<StatusChangedPayload>(payload)
                    ?? throw new InvalidOperationException($"Cannot deserialize payload for type '{type}'.");
                await publisher.PublishStatusChangedAsync(
                    statusChanged.DatasetId, statusChanged.FromStatus, statusChanged.ToStatus, ct);
                break;

            case OutboxMessageTypes.DatasetReady:
                var datasetReady = JsonSerializer.Deserialize<DatasetReadyPayload>(payload)
                    ?? throw new InvalidOperationException($"Cannot deserialize payload for type '{type}'.");
                await publisher.PublishDatasetReadyAsync(datasetReady.DatasetId, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown outbox message type '{type}'.");
        }
    }

}
