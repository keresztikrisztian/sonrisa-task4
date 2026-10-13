using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sonrisa.EventAlertSystem.Api.Domain;

namespace Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(eventItem => eventItem.Id);
        builder.Property(eventItem => eventItem.Fingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(eventItem => eventItem.Fingerprint).IsUnique();
        builder.Property(eventItem => eventItem.Category).HasMaxLength(100).IsRequired();
        builder.Property(eventItem => eventItem.Title).HasMaxLength(300).IsRequired();
        builder.Property(eventItem => eventItem.Description).HasMaxLength(4000).IsRequired();
        builder.Property(eventItem => eventItem.OccurredAtUtc).IsRequired();
        builder.Property(eventItem => eventItem.CreatedAtUtc).IsRequired();
        builder.HasMany(eventItem => eventItem.Deliveries)
            .WithOne(delivery => delivery.Event)
            .HasForeignKey(delivery => delivery.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}