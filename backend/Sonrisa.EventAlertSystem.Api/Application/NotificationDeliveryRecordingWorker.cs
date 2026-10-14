using Microsoft.EntityFrameworkCore;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;

namespace Sonrisa.EventAlertSystem.Api.Application;

public sealed class NotificationDeliveryRecordingWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RecordPendingDeliveriesAsync(stoppingToken);
        }
    }

    private async Task RecordPendingDeliveriesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        var pendingDeliveries = await context.NotificationDeliveries
            .Where(delivery => delivery.Status == NotificationDeliveryStatus.Pending)
            .OrderBy(delivery => delivery.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var delivery in pendingDeliveries)
        {
            delivery.Status = NotificationDeliveryStatus.Processing;
            delivery.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            delivery.Status = NotificationDeliveryStatus.Recorded;
            delivery.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}