namespace Sonrisa.EventAlertSystem.Api.Domain;

public sealed class Event
{
    public int Id { get; set; }
    public required string Fingerprint { get; set; }
    public required string Category { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<NotificationDelivery> Deliveries { get; set; } = new List<NotificationDelivery>();
}