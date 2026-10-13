using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sonrisa.EventAlertSystem.Api.Domain;

namespace Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence.Configurations;

public sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("AlertRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.UserId).HasMaxLength(100).IsRequired();
        builder.Property(rule => rule.Name).HasMaxLength(120).IsRequired();
        builder.Property(rule => rule.Category).HasMaxLength(100);
        builder.Property(rule => rule.CreatedAtUtc).IsRequired();
        builder.Property(rule => rule.UpdatedAtUtc).IsRequired();

        builder.HasMany(rule => rule.Keywords)
            .WithOne(keyword => keyword.AlertRule)
            .HasForeignKey(keyword => keyword.AlertRuleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(rule => rule.Channels)
            .WithOne(channel => channel.AlertRule)
            .HasForeignKey(channel => channel.AlertRuleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(rule => rule.Deliveries)
            .WithOne(delivery => delivery.AlertRule)
            .HasForeignKey(delivery => delivery.AlertRuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}