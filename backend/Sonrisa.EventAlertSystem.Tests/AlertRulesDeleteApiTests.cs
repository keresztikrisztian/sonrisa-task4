using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class AlertRulesDeleteApiTests
{
    [Fact]
    public async Task DeleteAlertRule_LogicalDeletePreservesRuleChildrenAndDeliveryHistory()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var rule = CreateRuleWithDelivery();
        await factory.SeedRulesAsync(rule);

        using var response = await DeleteAlertRuleAsync(client, rule.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());

        using var listResponse = await GetAlertRulesAsync(client);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<RulePageResponse>()
            ?? throw new InvalidOperationException("The response body did not contain a rule page.");
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/alert-rules/{rule.Id}")
        {
            Content = JsonContent.Create(new
            {
                name = "Changed",
                keywords = Array.Empty<string>(),
                channels = Array.Empty<string>(),
                isEnabled = true
            })
        };
        putRequest.Headers.Add("X-Demo-Identity", "demo-user");
        using var putResponse = await client.SendAsync(putRequest);
        Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        var persistedRule = await context.AlertRules
            .AsSplitQuery()
            .Include(existing => existing.Keywords)
            .Include(existing => existing.Channels)
            .SingleAsync(existing => existing.Id == rule.Id);
        var persistedDelivery = await context.NotificationDeliveries
            .Include(delivery => delivery.Event)
            .SingleAsync(delivery => delivery.AlertRuleId == rule.Id);

        Assert.True(persistedRule.IsDeleted);
        Assert.Equal("demo-user", persistedRule.UserId);
        Assert.Contains(persistedRule.Keywords, keyword => keyword.Keyword == "flood");
        Assert.Contains(persistedRule.Channels, channel => channel.Channel == NotificationChannel.Email);
        Assert.Equal(NotificationDeliveryStatus.Recorded, persistedDelivery.Status);
        Assert.Equal(persistedRule.Id, persistedDelivery.AlertRuleId);
        Assert.True(persistedDelivery.Event.Id > 0);
    }

    [Fact]
    public async Task DeleteAlertRule_WithoutIdentityHeader_ReturnsUnauthorized()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await client.DeleteAsync("/api/alert-rules/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAlertRule_WithDemoAdmin_ReturnsForbidden()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/alert-rules/1");
        request.Headers.Add("X-Demo-Identity", "demo-admin");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAlertRule_WhenMissing_ReturnsNotFound()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);

        using var response = await DeleteAlertRuleAsync(client, 12345);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAlertRule_WhenAlreadyDeleted_ReturnsNotFound()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var deletedRule = CreateRuleWithDelivery();
        deletedRule.IsDeleted = true;
        await factory.SeedRulesAsync(deletedRule);

        using var response = await DeleteAlertRuleAsync(client, deletedRule.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAlertRule_WhenOwnedByAnotherIdentity_ReturnsNotFound()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var otherRule = CreateRuleWithDelivery();
        otherRule.UserId = "demo-admin";
        await factory.SeedRulesAsync(otherRule);

        using var response = await DeleteAlertRuleAsync(client, otherRule.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static AlertRule CreateRuleWithDelivery()
    {
        var occurredAt = new DateTime(2025, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var eventItem = new Event
        {
            Fingerprint = Guid.NewGuid().ToString("N"),
            Category = "Weather",
            Title = "Flood alert",
            Description = "Flood warning",
            OccurredAtUtc = occurredAt
        };
        var rule = new AlertRule
        {
            UserId = "demo-user",
            Name = "Flood rule",
            CreatedAtUtc = occurredAt,
            UpdatedAtUtc = occurredAt
        };
        rule.Keywords.Add(new AlertRuleKeyword { Keyword = "flood" });
        rule.Channels.Add(new AlertRuleChannel { Channel = NotificationChannel.Email });
        rule.Deliveries.Add(new NotificationDelivery
        {
            Channel = NotificationChannel.Email,
            Status = NotificationDeliveryStatus.Recorded,
            AttemptCount = 1,
            Event = eventItem,
            CreatedAtUtc = occurredAt,
            UpdatedAtUtc = occurredAt
        });

        return rule;
    }

    private static async Task<HttpResponseMessage> DeleteAlertRuleAsync(HttpClient client, int id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/alert-rules/{id}");
        request.Headers.Add("X-Demo-Identity", "demo-user");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetAlertRulesAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/alert-rules?pageSize=100");
        request.Headers.Add("X-Demo-Identity", "demo-user");
        return await client.SendAsync(request);
    }

    private sealed record RulePageResponse(RuleResponse[] Items, int Page, int PageSize, int TotalCount);

    private sealed record RuleResponse(int Id, string Name, string[] Keywords, string[] Channels);
}