using Microsoft.EntityFrameworkCore;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;

namespace Sonrisa.EventAlertSystem.Api.Application;

public sealed class NotificationDeliveryRecordingWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingRecoveryAge = TimeSpan.FromMinutes(1);
    private const int MaximumAttempts = 3;
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessPendingDeliveriesAsync(stoppingToken);
        }
    }

    private async Task ProcessPendingDeliveriesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        var now = DateTime.UtcNow;
        await RecoverStaleDeliveriesAsync(context, now, cancellationToken);

        var deliveryIds = await context.NotificationDeliveries
            .Where(delivery => delivery.Status == NotificationDeliveryStatus.Pending
                && (delivery.NextAttemptAtUtc == null || delivery.NextAttemptAtUtc <= now))
            .OrderBy(delivery => delivery.Id)
            .Take(BatchSize)
            .Select(delivery => delivery.Id)
            .ToListAsync(cancellationToken);

        foreach (var deliveryId in deliveryIds)
        {
            await ProcessDeliveryAsync(context, scope.ServiceProvider, deliveryId, cancellationToken);
        }
    }

    private static async Task RecoverStaleDeliveriesAsync(
        EventAlertDbContext context,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var staleBefore = now - ProcessingRecoveryAge;
        await context.NotificationDeliveries
            .Where(delivery => delivery.Status == NotificationDeliveryStatus.Processing
                && delivery.UpdatedAtUtc <= staleBefore
                && delivery.AttemptCount >= MaximumAttempts)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(delivery => delivery.Status, NotificationDeliveryStatus.Failed)
                .SetProperty(delivery => delivery.NextAttemptAtUtc, (DateTime?)null)
                .SetProperty(delivery => delivery.LastError, "Processing was interrupted after the maximum attempts.")
                .SetProperty(delivery => delivery.UpdatedAtUtc, now), cancellationToken);

        await context.NotificationDeliveries
            .Where(delivery => delivery.Status == NotificationDeliveryStatus.Processing
                && delivery.UpdatedAtUtc <= staleBefore
                && delivery.AttemptCount < MaximumAttempts)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(delivery => delivery.Status, NotificationDeliveryStatus.Pending)
                .SetProperty(delivery => delivery.NextAttemptAtUtc, (DateTime?)null)
                .SetProperty(delivery => delivery.UpdatedAtUtc, now), cancellationToken);
    }

    private static async Task ProcessDeliveryAsync(
        EventAlertDbContext context,
        IServiceProvider serviceProvider,
        int deliveryId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var claimed = await context.NotificationDeliveries
            .Where(delivery => delivery.Id == deliveryId
                && delivery.Status == NotificationDeliveryStatus.Pending
                && (delivery.NextAttemptAtUtc == null || delivery.NextAttemptAtUtc <= now))
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(delivery => delivery.Status, NotificationDeliveryStatus.Processing)
                .SetProperty(delivery => delivery.AttemptCount, delivery => delivery.AttemptCount + 1)
                .SetProperty(delivery => delivery.UpdatedAtUtc, now), cancellationToken);

        if (claimed == 0)
        {
            return;
        }

        var delivery = await context.NotificationDeliveries
            .AsNoTracking()
            .SingleAsync(item => item.Id == deliveryId, cancellationToken);
        var recorder = serviceProvider.GetRequiredService<INotificationDeliveryRecorder>();
        try
        {
            await recorder.RecordAsync(delivery, cancellationToken);
            await context.NotificationDeliveries
                .Where(item => item.Id == deliveryId
                    && item.Status == NotificationDeliveryStatus.Processing
                    && item.AttemptCount == delivery.AttemptCount)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(item => item.Status, NotificationDeliveryStatus.Recorded)
                    .SetProperty(item => item.NextAttemptAtUtc, (DateTime?)null)
                    .SetProperty(item => item.LastError, (string?)null)
                    .SetProperty(item => item.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await PersistFailureAsync(context, delivery, exception, cancellationToken);
        }
    }

    private static Task<int> PersistFailureAsync(
        EventAlertDbContext context,
        NotificationDelivery delivery,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isPermanent = exception is NotificationDeliveryException { IsTransient: false };
        var isTerminal = isPermanent || delivery.AttemptCount >= MaximumAttempts;
        var now = DateTime.UtcNow;
        var error = exception.Message.Length <= 2000 ? exception.Message : exception.Message[..2000];
        DateTime? nextAttemptAtUtc = isTerminal ? null : now + RetryDelay(delivery.AttemptCount);

        return context.NotificationDeliveries
            .Where(item => item.Id == delivery.Id
                && item.Status == NotificationDeliveryStatus.Processing
                && item.AttemptCount == delivery.AttemptCount)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(item => item.Status, isTerminal
                    ? NotificationDeliveryStatus.Failed
                    : NotificationDeliveryStatus.Pending)
                .SetProperty(item => item.NextAttemptAtUtc, nextAttemptAtUtc)
                .SetProperty(item => item.LastError, error)
                .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
    }

    private static TimeSpan RetryDelay(int attemptCount)
    {
        var seconds = Math.Min(30, Math.Pow(2, attemptCount - 1));
        return TimeSpan.FromSeconds(seconds);
    }
}