using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sonrisa.EventAlertSystem.Api.Domain;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class AlertRulesUpdateApiTests
{
    [Fact]
    public async Task UpdateAlertRule_FullyReplacesFieldsAndChildCollections()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var createdAt = new DateTime(2020, 4, 5, 6, 7, 8, DateTimeKind.Utc);
        var existingRule = CreateExistingRule(createdAt);
        await factory.SeedRulesAsync(existingRule);

        using var response = await PutAlertRuleAsync(client, existingRule.Id, new
        {
            name = "Updated flood rule",
            category = "Weather",
            keywords = new[] { "old keyword", "  flood  " },
            channels = new[] { "Slack" },
            isEnabled = false,
            userId = "demo-admin"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadAsByteArrayAsync();
        var updatedRule = JsonSerializer.Deserialize<AlertRuleResponse>(responseBody, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The response body did not contain an alert rule.");

        Assert.Equal(existingRule.Id, updatedRule.Id);
        Assert.Equal("Updated flood rule", updatedRule.Name);
        Assert.Equal("Weather", updatedRule.Category);
        Assert.Equal(["flood", "old keyword"], updatedRule.Keywords.OrderBy(keyword => keyword, StringComparer.Ordinal));
        Assert.Equal(["Slack"], updatedRule.Channels);
        Assert.False(updatedRule.IsEnabled);
        Assert.Equal(createdAt, updatedRule.CreatedAtUtc);
        Assert.True(updatedRule.UpdatedAtUtc > existingRule.UpdatedAtUtc);
        AssertResponseFields(responseBody, "id", "name", "category", "keywords", "channels",
            "isEnabled", "createdAtUtc", "updatedAtUtc");

        using var listResponse = await GetAlertRulesAsync(client);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listDocument = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        var persistedRule = Assert.Single(listDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(existingRule.Id, persistedRule.GetProperty("id").GetInt32());
        Assert.Equal(["flood", "old keyword"], persistedRule.GetProperty("keywords").EnumerateArray()
            .Select(keyword => keyword.GetString())
            .OrderBy(keyword => keyword, StringComparer.Ordinal));
        Assert.Equal(["Slack"], persistedRule.GetProperty("channels").EnumerateArray()
            .Select(channel => channel.GetString()));
    }

    [Fact]
    public async Task UpdateAlertRule_WithEmptyCollections_ClearsExistingChildren()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var existingRule = CreateExistingRule(new DateTime(2020, 4, 5, 6, 7, 8, DateTimeKind.Utc));
        await factory.SeedRulesAsync(existingRule);

        using var response = await PutAlertRuleAsync(client, existingRule.Id, new
        {
            name = "Unrestricted replacement",
            category = (string?)null,
            keywords = Array.Empty<string>(),
            channels = Array.Empty<string>(),
            isEnabled = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedRule = await response.Content.ReadFromJsonAsync<AlertRuleResponse>()
            ?? throw new InvalidOperationException("The response body did not contain an alert rule.");

        Assert.Empty(updatedRule.Keywords);
        Assert.Empty(updatedRule.Channels);
        Assert.True(updatedRule.IsEnabled);
    }

    [Fact]
    public async Task UpdateAlertRule_WithoutIdentityHeader_ReturnsUnauthorized()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/alert-rules/1")
        {
            Content = JsonContent.Create(ValidUpdateRequest())
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAlertRule_WithDemoAdmin_ReturnsForbidden()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await PutAlertRuleAsync(client, 1, ValidUpdateRequest(), "demo-admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAlertRule_WhenMissing_ReturnsNotFound()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await PutAlertRuleAsync(client, 1, ValidUpdateRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAlertRule_WhenLogicallyDeleted_ReturnsNotFound()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var deletedRule = CreateExistingRule(new DateTime(2020, 4, 5, 6, 7, 8, DateTimeKind.Utc));
        deletedRule.IsDeleted = true;
        await factory.SeedRulesAsync(deletedRule);

        using var response = await PutAlertRuleAsync(client, deletedRule.Id, ValidUpdateRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAlertRule_WhenOwnedByAnotherIdentity_ReturnsNotFound()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var otherRule = CreateExistingRule(new DateTime(2020, 4, 5, 6, 7, 8, DateTimeKind.Utc));
        otherRule.UserId = "demo-admin";
        await factory.SeedRulesAsync(otherRule);

        using var response = await PutAlertRuleAsync(client, otherRule.Id, ValidUpdateRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task UpdateAlertRule_WithInvalidInput_ReturnsValidationProblem(object requestBody)
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var existingRule = CreateExistingRule(new DateTime(2020, 4, 5, 6, 7, 8, DateTimeKind.Utc));
        await factory.SeedRulesAsync(existingRule);

        using var response = await PutAlertRuleAsync(client, existingRule.Id, requestBody);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
    }

    public static IEnumerable<object[]> InvalidRequests =>
    [
        [new { category = "Weather", keywords = new[] { "valid" }, channels = new[] { "Email" }, isEnabled = true }],
        [new { name = "", category = "Weather", keywords = new[] { "valid" }, channels = new[] { "Email" }, isEnabled = true }],
        [new { name = new string('n', 121), category = "Weather", keywords = new[] { "valid" }, channels = new[] { "Email" }, isEnabled = true }],
        [new { name = "Valid", category = new string('c', 101), keywords = new[] { "valid" }, channels = new[] { "Email" }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", keywords = new[] { new string('k', 101) }, channels = new[] { "Email" }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", keywords = new string?[] { "   " }, channels = new[] { "Email" }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", keywords = new string?[] { "valid", null }, channels = new[] { "Email" }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", keywords = new[] { "Flood", " flood " }, channels = new[] { "Email" }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", keywords = new[] { "valid" }, channels = new[] { "Sms" }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", keywords = new[] { "valid" }, channels = new[] { "Slack", "Slack" }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", keywords = new[] { "valid" }, channels = new string?[] { null }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", keywords = new[] { "valid" }, channels = new[] { "Email" } }],
        [new { name = "Valid", category = "Weather", keywords = new[] { "valid" }, isEnabled = true }],
        [new { name = "Valid", category = "Weather", channels = new[] { "Email" }, isEnabled = true }]
    ];

    private static object ValidUpdateRequest() => new
    {
        name = "Updated rule",
        category = "Weather",
        keywords = new[] { "updated" },
        channels = new[] { "Email" },
        isEnabled = true
    };

    private static AlertRule CreateExistingRule(DateTime createdAtUtc)
    {
        var rule = new AlertRule
        {
            UserId = "demo-user",
            Name = "Original rule",
            Category = "Original category",
            IsEnabled = true,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
        rule.Keywords.Add(new AlertRuleKeyword { Keyword = "old keyword" });
        rule.Channels.Add(new AlertRuleChannel
        {
            Channel = NotificationChannel.Email
        });
        return rule;
    }

    private static async Task<HttpResponseMessage> PutAlertRuleAsync(
        HttpClient client,
        int id,
        object requestBody,
        string? identity = "demo-user")
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/alert-rules/{id}")
        {
            Content = JsonContent.Create(requestBody)
        };
        if (identity is not null)
        {
            request.Headers.Add("X-Demo-Identity", identity);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetAlertRulesAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/alert-rules?pageSize=100");
        request.Headers.Add("X-Demo-Identity", "demo-user");
        return await client.SendAsync(request);
    }

    private static void AssertResponseFields(byte[] responseBody, params string[] expectedFields)
    {
        using var document = JsonDocument.Parse(responseBody);
        var actualFields = document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedFields.OrderBy(name => name, StringComparer.Ordinal), actualFields);
    }

    private sealed record AlertRuleResponse(
        int Id,
        string Name,
        string? Category,
        string[] Keywords,
        string[] Channels,
        bool IsEnabled,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}