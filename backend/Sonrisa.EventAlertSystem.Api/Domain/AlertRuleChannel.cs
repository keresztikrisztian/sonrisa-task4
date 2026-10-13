namespace Sonrisa.EventAlertSystem.Api.Domain;

public sealed class AlertRuleChannel
{
    public int Id { get; set; }
    public int AlertRuleId { get; set; }
    public NotificationChannel Channel { get; set; }
    public AlertRule AlertRule { get; set; } = null!;
}