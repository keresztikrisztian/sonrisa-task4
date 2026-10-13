using Microsoft.EntityFrameworkCore;
using Sonrisa.EventAlertSystem.Api.Domain;

namespace Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;

public sealed class EventAlertDbContext(DbContextOptions<EventAlertDbContext> options) : DbContext(options)
{
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertRuleKeyword> AlertRuleKeywords => Set<AlertRuleKeyword>();
    public DbSet<AlertRuleChannel> AlertRuleChannels => Set<AlertRuleChannel>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventAlertDbContext).Assembly);
    }
}