namespace Sonrisa.EventAlertSystem.Api.Domain;

public sealed class AlertRuleKeyword
{
    public int Id { get; set; }
    public int AlertRuleId { get; set; }
    public required string Keyword { get; set; }
    public AlertRule AlertRule { get; set; } = null!;
}