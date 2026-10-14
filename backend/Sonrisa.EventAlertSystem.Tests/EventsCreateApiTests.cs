using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class EventsCreateApiTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("unknown", HttpStatusCode.Unauthorized)]
    [InlineData("demo-user", HttpStatusCode.Forbidden)]
    public async Task SubmitEvent_RequiresDemoAdmin(string? identity, HttpStatusCode expectedStatus)
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);

        using var response = await SubmitEventAsync(client, ValidRequest(), identity);

        Assert.Equal(expectedStatus, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        Assert.Equal(0, await context.Events.CountAsync());
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task SubmitEvent_WithInvalidInput_ReturnsValidationProblem(object requestBody)
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);

        using var response = await SubmitEventAsync(client, requestBody, "demo-admin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
    }

    [Fact]
    public async Task SubmitEvent_PreservesTextAndReturnsCreatedResponseContract()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var request = new
        {
            category = "  Weather  ",
            title = "  Storm warning  ",
            description = "  Heavy rain expected.  ",
            occurredAt = "2026-05-01T12:30:45.9876543+02:00"
        };

        using var response = await SubmitEventAsync(client, request, "DEMO-ADMIN");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal(
            ["category", "createdAtUtc", "description", "id", "isDuplicate", "occurredAtUtc", "title"],
            root.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.False(root.GetProperty("isDuplicate").GetBoolean());
        Assert.Equal(request.category, root.GetProperty("category").GetString());
        Assert.Equal(request.title, root.GetProperty("title").GetString());
        Assert.Equal(request.description, root.GetProperty("description").GetString());
        Assert.Equal(DateTimeOffset.Parse("2026-05-01T10:30:45.9876543Z"),
            root.GetProperty("occurredAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task SubmitEvent_CanonicalEquivalentPayloadReturnsExistingEventAsDuplicate()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var firstResponse = await SubmitEventAsync(client, new
        {
            category = "Weather",
            title = "Flash flood",
            description = "Road closed",
            occurredAt = "2026-05-01T12:30:45.900+02:00"
        }, "demo-admin");
        using var duplicateResponse = await SubmitEventAsync(client, new
        {
            category = " weather ",
            title = "FLASH\tFLOOD",
            description = " road   closed ",
            occurredAt = "2026-05-01T10:30:45Z"
        }, "demo-admin");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        using var firstDocument = await JsonDocument.ParseAsync(await firstResponse.Content.ReadAsStreamAsync());
        using var duplicateDocument = await JsonDocument.ParseAsync(await duplicateResponse.Content.ReadAsStreamAsync());
        Assert.Equal(firstDocument.RootElement.GetProperty("id").GetInt32(),
            duplicateDocument.RootElement.GetProperty("id").GetInt32());
        Assert.True(duplicateDocument.RootElement.GetProperty("isDuplicate").GetBoolean());

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        Assert.Equal(1, await context.Events.CountAsync());
        Assert.Equal(0, await context.NotificationDeliveries.CountAsync());
    }

    [Fact]
    public async Task SubmitEvent_CreatesDeliveriesOnlyForMatchingConfiguredChannels()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var matchingRule = new AlertRule
        {
            UserId = "demo-user",
            Name = "Flood alerts",
            Category = "Weather",
            Keywords = [new AlertRuleKeyword { Keyword = "flood" }],
            Channels =
            [
                new AlertRuleChannel { Channel = NotificationChannel.Email },
                new AlertRuleChannel { Channel = NotificationChannel.Slack }
            ]
        };
        var noChannelRule = new AlertRule { UserId = "demo-user", Name = "No channel", Category = "Weather" };
        var nonMatchingRule = new AlertRule
        {
            UserId = "demo-user",
            Name = "Wrong keyword",
            Keywords = [new AlertRuleKeyword { Keyword = "earthquake" }],
            Channels = [new AlertRuleChannel { Channel = NotificationChannel.Email }]
        };
        await SeedRulesAsync(factory, matchingRule, noChannelRule, nonMatchingRule);

        using var response = await SubmitEventAsync(client, ValidRequest(), "demo-admin");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        List<NotificationDelivery> deliveries = [];
        while (DateTime.UtcNow < deadline)
        {
            deliveries = await context.NotificationDeliveries.AsNoTracking().ToListAsync();
            if (deliveries.Count == 2 && deliveries.All(delivery => delivery.Status == NotificationDeliveryStatus.Recorded))
            {
                break;
            }

            await Task.Delay(25);
        }

        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, delivery => Assert.Equal(NotificationDeliveryStatus.Recorded, delivery.Status));
        Assert.Equal(
            [NotificationChannel.Email, NotificationChannel.Slack],
            deliveries.Select(delivery => delivery.Channel).OrderBy(channel => channel));
        Assert.All(deliveries, delivery => Assert.Equal(matchingRule.Id, delivery.AlertRuleId));
    }

    [Fact]
    public async Task SubmitEvent_WhenDeliveryInsertFails_RollsBackEvent()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        await SeedRulesAsync(factory, new AlertRule
        {
            UserId = "demo-user",
            Name = "Matching rule",
            Channels = [new AlertRuleChannel { Channel = NotificationChannel.Email }]
        });
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
            await setupContext.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectDelivery BEFORE INSERT ON NotificationDeliveries
                BEGIN
                    SELECT RAISE(ABORT, 'forced delivery failure');
                END;
                """);
        }

        using var response = await SubmitEventAsync(client, ValidRequest(), "demo-admin");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        Assert.Equal(0, await context.Events.CountAsync());
        Assert.Equal(0, await context.NotificationDeliveries.CountAsync());
    }

    public static IEnumerable<object[]> InvalidRequests =>
    [
        [new { title = "Storm", description = "Rain", occurredAt = "2026-05-01T12:30:45Z" }],
        [new { category = "", title = "Storm", description = "Rain", occurredAt = "2026-05-01T12:30:45Z" }],
        [new { category = "Weather", title = "", description = "Rain", occurredAt = "2026-05-01T12:30:45Z" }],
        [new { category = new string('c', 101), title = "Storm", description = "Rain", occurredAt = "2026-05-01T12:30:45Z" }],
        [new { category = "Weather", title = new string('t', 301), description = "Rain", occurredAt = "2026-05-01T12:30:45Z" }],
        [new { category = "Weather", title = "Storm", description = new string('d', 4001), occurredAt = "2026-05-01T12:30:45Z" }],
        [new { category = "Weather", title = "Storm", description = "Rain", occurredAt = "2026-05-01 12:30:45" }],
        [new { category = "   ", title = "Storm", description = "Rain", occurredAt = "2026-05-01T12:30:45Z" }],
        [new { category = "Weather", title = "   ", description = "Rain", occurredAt = "2026-05-01T12:30:45Z" }]
    ];

    private static object ValidRequest() => new
    {
        category = "Weather",
        title = "Flood warning",
        description = "A flood warning was issued",
        occurredAt = "2026-05-01T12:30:45Z"
    };

    private static async Task<HttpResponseMessage> SubmitEventAsync(
        HttpClient client,
        object requestBody,
        string? identity)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events")
        {
            Content = JsonContent.Create(requestBody)
        };
        if (identity is not null)
        {
            request.Headers.Add("X-Demo-Identity", identity);
        }

        return await client.SendAsync(request);
    }

    private static async Task SeedRulesAsync(AlertRulesApiFactory factory, params AlertRule[] rules)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        await context.AlertRules.AddRangeAsync(rules);
        await context.SaveChangesAsync();
    }
}