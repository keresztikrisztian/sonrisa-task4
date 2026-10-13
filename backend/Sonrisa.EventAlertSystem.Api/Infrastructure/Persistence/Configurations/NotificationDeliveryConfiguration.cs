using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sonrisa.EventAlertSystem.Api.Domain;

namespace Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence.Configurations;

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(delivery => delivery.LastError).HasMaxLength(2000);
        builder.Property(delivery => delivery.CreatedAtUtc).IsRequired();
        builder.Property(delivery => delivery.UpdatedAtUtc).IsRequired();
        builder.HasIndex(delivery => new { delivery.EventId, delivery.AlertRuleId, delivery.Channel }).IsUnique();
    }
}