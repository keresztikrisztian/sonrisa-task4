namespace Sonrisa.EventAlertSystem.Api.Domain;

public sealed class NotificationDelivery
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int AlertRuleId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
    public Event Event { get; set; } = null!;
    public AlertRule AlertRule { get; set; } = null!;
}