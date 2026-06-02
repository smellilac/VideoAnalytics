namespace VideoAnalytics.Infrastructure.Kafka;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Infrastructure.Persistence;

internal sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

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
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await DispatchAsync(message.Type, message.Payload, eventPublisher, cancellationToken);
                message.MarkProcessed(timeProvider.GetUtcNow());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId} of type {Type}", message.Id, message.Type);
            }
        }

        if (messages.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task DispatchAsync(string type, string payload, IEventPublisher publisher, CancellationToken ct)
    {
        switch (type)
        {
            case "dataset.status.changes":
                var statusChanged = JsonSerializer.Deserialize<StatusChangedPayload>(payload)
                    ?? throw new InvalidOperationException($"Cannot deserialize payload for type '{type}'.");
                await publisher.PublishStatusChangedAsync(
                    statusChanged.DatasetId, statusChanged.FromStatus, statusChanged.ToStatus, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown outbox message type '{type}'.");
        }
    }

    private sealed record StatusChangedPayload(Guid DatasetId, string FromStatus, string ToStatus);
}
