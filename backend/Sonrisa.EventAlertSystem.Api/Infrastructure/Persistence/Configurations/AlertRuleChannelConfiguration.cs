using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sonrisa.EventAlertSystem.Api.Domain;

namespace Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence.Configurations;

public sealed class AlertRuleChannelConfiguration : IEntityTypeConfiguration<AlertRuleChannel>
{
    public void Configure(EntityTypeBuilder<AlertRuleChannel> builder)
    {
        builder.ToTable("AlertRuleChannels");
        builder.HasKey(channel => channel.Id);
        builder.Property(channel => channel.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(channel => new { channel.AlertRuleId, channel.Channel }).IsUnique();
    }
}