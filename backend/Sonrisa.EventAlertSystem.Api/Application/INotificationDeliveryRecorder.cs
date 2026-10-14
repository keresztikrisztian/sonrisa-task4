using Sonrisa.EventAlertSystem.Api.Domain;

namespace Sonrisa.EventAlertSystem.Api.Application;

public interface INotificationDeliveryRecorder
{
    Task RecordAsync(NotificationDelivery delivery, CancellationToken cancellationToken);
}

public sealed class RecordingNotificationDeliveryRecorder : INotificationDeliveryRecorder
{
    public Task RecordAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class NotificationDeliveryException(string message, bool isTransient) : Exception(message)
{
    public bool IsTransient { get; } = isTransient;
}