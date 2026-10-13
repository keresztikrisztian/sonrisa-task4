using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class PersistenceConstraintTests
{
    [Fact]
    public async Task EventFingerprint_MustBeUnique()
    {
        await using var connection = await CreateDatabaseConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Events.Add(CreateEvent("duplicate-fingerprint"));
        await context.SaveChangesAsync();
        context.Events.Add(CreateEvent("duplicate-fingerprint"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task DeliveryEventRuleAndChannelCombination_MustBeUnique()
    {
        await using var connection = await CreateDatabaseConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var eventItem = CreateEvent("delivery-event");
        var rule = new AlertRule { UserId = "demo-user", Name = "Test rule" };
        context.AddRange(eventItem, rule);
        await context.SaveChangesAsync();

        context.NotificationDeliveries.Add(CreateDelivery(eventItem.Id, rule.Id));
        await context.SaveChangesAsync();
        context.NotificationDeliveries.Add(CreateDelivery(eventItem.Id, rule.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task RuleKeywords_MustBeUniqueCaseInsensitively()
    {
        await using var connection = await CreateDatabaseConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var rule = new AlertRule { UserId = "demo-user", Name = "Test rule" };
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        context.AlertRuleKeywords.Add(new AlertRuleKeyword { AlertRuleId = rule.Id, Keyword = "Flood" });
        await context.SaveChangesAsync();
        context.AlertRuleKeywords.Add(new AlertRuleKeyword { AlertRuleId = rule.Id, Keyword = "flood" });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task RuleChannels_MustBeUnique()
    {
        await using var connection = await CreateDatabaseConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var rule = new AlertRule { UserId = "demo-user", Name = "Test rule" };
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        context.AlertRuleChannels.Add(new AlertRuleChannel
        {
            AlertRuleId = rule.Id,
            Channel = NotificationChannel.Email
        });
        await context.SaveChangesAsync();
        context.AlertRuleChannels.Add(new AlertRuleChannel
        {
            AlertRuleId = rule.Id,
            Channel = NotificationChannel.Email
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task InitialCreateMigration_AppliesAndEnforcesKeyUniqueConstraints()
    {
        await using var connection = await CreateDatabaseConnection();
        await using var context = CreateContext(connection);

        await context.Database.MigrateAsync();

        var eventItem = CreateEvent("migrated-fingerprint");
        var rule = new AlertRule { UserId = "demo-user", Name = "Migrated test rule" };
        context.AddRange(eventItem, rule);
        await context.SaveChangesAsync();

        context.Events.Add(CreateEvent("migrated-fingerprint"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        context.NotificationDeliveries.Add(CreateDelivery(eventItem.Id, rule.Id));
        await context.SaveChangesAsync();
        context.NotificationDeliveries.Add(CreateDelivery(eventItem.Id, rule.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        context.AlertRuleKeywords.Add(new AlertRuleKeyword { AlertRuleId = rule.Id, Keyword = "Flood" });
        await context.SaveChangesAsync();
        context.AlertRuleKeywords.Add(new AlertRuleKeyword { AlertRuleId = rule.Id, Keyword = "flood" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        context.AlertRuleChannels.Add(new AlertRuleChannel
        {
            AlertRuleId = rule.Id,
            Channel = NotificationChannel.Email
        });
        await context.SaveChangesAsync();
        context.AlertRuleChannels.Add(new AlertRuleChannel
        {
            AlertRuleId = rule.Id,
            Channel = NotificationChannel.Email
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static async Task<SqliteConnection> CreateDatabaseConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static EventAlertDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<EventAlertDbContext>()
            .UseSqlite(connection)
            .Options;
        return new EventAlertDbContext(options);
    }

    private static Event CreateEvent(string fingerprint)
    {
        return new Event
        {
            Fingerprint = fingerprint,
            Category = "Weather",
            Title = "Storm warning",
            Description = "A storm warning was issued",
            OccurredAtUtc = DateTime.UtcNow
        };
    }

    private static NotificationDelivery CreateDelivery(int eventId, int ruleId)
    {
        return new NotificationDelivery
        {
            EventId = eventId,
            AlertRuleId = ruleId,
            Channel = NotificationChannel.Email
        };
    }
}