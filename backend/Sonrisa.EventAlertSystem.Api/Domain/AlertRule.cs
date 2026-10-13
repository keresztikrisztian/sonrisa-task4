namespace Sonrisa.EventAlertSystem.Api.Domain;

public sealed class AlertRule
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public string? Category { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<AlertRuleKeyword> Keywords { get; set; } = new List<AlertRuleKeyword>();
    public ICollection<AlertRuleChannel> Channels { get; set; } = new List<AlertRuleChannel>();
    public ICollection<NotificationDelivery> Deliveries { get; set; } = new List<NotificationDelivery>();
}