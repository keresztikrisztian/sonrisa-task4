using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class EventsQueryApiTests
{
    [Theory]
    [InlineData("/api/events", null, HttpStatusCode.Unauthorized)]
    [InlineData("/api/events", "demo-user", HttpStatusCode.Forbidden)]
    [InlineData("/api/notification-deliveries", null, HttpStatusCode.Unauthorized)]
    [InlineData("/api/notification-deliveries", "demo-user", HttpStatusCode.Forbidden)]
    public async Task AdminQueries_RequireDemoAdmin(string path, string? identity, HttpStatusCode expectedStatus)
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);

        using var response = await GetAsync(client, path, identity);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_AppliesFiltersPaginationOrderingAndDtoContract()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var oldEvent = CreateEvent("old", "Weather", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var firstIncluded = CreateEvent("included-1", "Weather", new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));
        var secondIncluded = CreateEvent("included-2", "weather", new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));
        var wrongCategory = CreateEvent("wrong-category", "Markets", new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));
        var upperBound = CreateEvent("upper-bound", "Weather", new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc));
        await SeedAsync(factory, oldEvent, firstIncluded, secondIncluded, wrongCategory, upperBound);

        using var response = await GetAsync(
            client,
            "/api/events?category=WEATHER&fromUtc=2026-05-02T00%3A00%3A00Z&toUtc=2026-05-03T00%3A00%3A00Z&page=1&pageSize=1",
            "demo-admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(1, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(secondIncluded.Id, item.GetProperty("id").GetInt32());
        Assert.Equal(
            ["category", "createdAtUtc", "description", "id", "occurredAtUtc", "title"],
            item.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetEventDetail_IncludesDeliveryDtosAndReturnsNotFoundForMissingId()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var rule = new AlertRule { UserId = "demo-user", Name = "Rule" };
        var eventItem = CreateEvent("detail", "Weather", DateTime.UtcNow);
        eventItem.Deliveries.Add(new NotificationDelivery
        {
            AlertRule = rule,
            Channel = NotificationChannel.Slack,
            Status = NotificationDeliveryStatus.Recorded,
            AttemptCount = 0
        });
        await SeedAsync(factory, eventItem);

        using var response = await GetAsync(client, $"/api/events/{eventItem.Id}", "demo-admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal(eventItem.Id, root.GetProperty("id").GetInt32());
        var delivery = Assert.Single(root.GetProperty("deliveries").EnumerateArray());
        Assert.Equal(
            ["alertRuleId", "attemptCount", "channel", "createdAtUtc", "eventId", "id", "sentAtUtc", "status", "updatedAtUtc"],
            delivery.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("Slack", delivery.GetProperty("channel").GetString());
        Assert.Equal("Recorded", delivery.GetProperty("status").GetString());

        using var missingResponse = await GetAsync(client, "/api/events/99999", "demo-admin");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Theory]
    [InlineData("/api/events?page=0")]
    [InlineData("/api/events?pageSize=101")]
    [InlineData("/api/events?fromUtc=not-a-date")]
    [InlineData("/api/events?fromUtc=2026-05-03T00%3A00%3A00Z&toUtc=2026-05-02T00%3A00%3A00Z")]
    [InlineData("/api/notification-deliveries?status=unknown")]
    [InlineData("/api/notification-deliveries?channel=SMS")]
    [InlineData("/api/notification-deliveries?eventId=0")]
    public async Task AdminQueries_WithInvalidFiltersOrPagination_ReturnBadRequest(string path)
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);

        using var response = await GetAsync(client, path, "demo-admin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetNotificationDeliveries_FiltersAndPagesInStableDescendingOrder()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var eventItem = CreateEvent("delivery-event", "Weather", DateTime.UtcNow);
        var createdAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        eventItem.Deliveries =
        [
            new NotificationDelivery
            {
                AlertRule = new AlertRule { UserId = "demo-user", Name = "Pending rule" },
                Channel = NotificationChannel.Email,
                Status = NotificationDeliveryStatus.Pending,
                CreatedAtUtc = createdAt
            },
            new NotificationDelivery
            {
                AlertRule = new AlertRule { UserId = "demo-user", Name = "Slack rule" },
                Channel = NotificationChannel.Slack,
                Status = NotificationDeliveryStatus.Recorded,
                CreatedAtUtc = createdAt
            },
            new NotificationDelivery
            {
                AlertRule = new AlertRule { UserId = "demo-user", Name = "Recorded rule" },
                Channel = NotificationChannel.Email,
                Status = NotificationDeliveryStatus.Recorded,
                CreatedAtUtc = createdAt.AddMinutes(1)
            }
        ];
        await SeedAsync(factory, eventItem);

        using var response = await GetAsync(
            client,
            $"/api/notification-deliveries?eventId={eventItem.Id}&status=Recorded&channel=Email&pageSize=1",
            "demo-admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(1, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal("Email", item.GetProperty("channel").GetString());
        Assert.Equal("Recorded", item.GetProperty("status").GetString());
    }

    private static Event CreateEvent(string fingerprint, string category, DateTime occurredAtUtc)
    {
        return new Event
        {
            Fingerprint = fingerprint,
            Category = category,
            Title = fingerprint,
            Description = "Event description",
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = occurredAtUtc
        };
    }

    private static async Task SeedAsync(AlertRulesApiFactory factory, params Event[] events)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        await context.Events.AddRangeAsync(events);
        await context.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? identity)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (identity is not null)
        {
            request.Headers.Add("X-Demo-Identity", identity);
        }

        return await client.SendAsync(request);
    }
}