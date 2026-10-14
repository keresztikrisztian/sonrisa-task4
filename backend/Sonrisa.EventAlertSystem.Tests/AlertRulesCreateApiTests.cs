using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class AlertRulesCreateApiTests
{
    [Fact]
    public async Task CreateAlertRule_WithoutIdentityHeader_ReturnsUnauthorized()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/alert-rules")
        {
            Content = JsonContent.Create(new { name = "Rule" })
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAlertRule_WithDemoAdmin_ReturnsForbidden()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await PostAlertRuleAsync(client, new { name = "Rule" }, "demo-admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateAlertRule_WithOnlyName_ReturnsCreatedRuleWithDefaults()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await PostAlertRuleAsync(client, new { name = "Weather alerts" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseBody = await response.Content.ReadAsByteArrayAsync();
        var createdRule = JsonSerializer.Deserialize<AlertRuleResponse>(responseBody, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The response body did not contain an alert rule.");

        Assert.True(createdRule.Id > 0);
        Assert.Equal("Weather alerts", createdRule.Name);
        Assert.Null(createdRule.Category);
        Assert.Empty(createdRule.Keywords);
        Assert.Empty(createdRule.Channels);
        Assert.True(createdRule.IsEnabled);
        Assert.True(createdRule.CreatedAtUtc > DateTime.MinValue);
        Assert.True(createdRule.UpdatedAtUtc > DateTime.MinValue);
        AssertResponseFields(responseBody, "id", "name", "category", "keywords", "channels",
            "isEnabled", "createdAtUtc", "updatedAtUtc");
    }

    [Fact]
    public async Task CreateAlertRule_WithEmptyKeywordAndChannelCollections_IsAllowed()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await PostAlertRuleAsync(client, new
        {
            name = "Unrestricted rule",
            keywords = Array.Empty<string>(),
            channels = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdRule = await response.Content.ReadFromJsonAsync<AlertRuleResponse>()
            ?? throw new InvalidOperationException("The response body did not contain an alert rule.");

        Assert.Empty(createdRule.Keywords);
        Assert.Empty(createdRule.Channels);
    }

    [Fact]
    public async Task CreateAlertRule_TrimsKeywordsPersistsChildrenAndUsesAuthenticatedOwner()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await PostAlertRuleAsync(client, new
        {
            name = "Flood alerts",
            category = "Weather",
            keywords = new[] { "  flood  ", "storm" },
            channels = new[] { "Email", "Slack" },
            isEnabled = false,
            userId = "demo-admin"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdRule = await response.Content.ReadFromJsonAsync<AlertRuleResponse>()
            ?? throw new InvalidOperationException("The response body did not contain an alert rule.");

        Assert.Equal("Flood alerts", createdRule.Name);
        Assert.Equal("Weather", createdRule.Category);
        Assert.Equal(["flood", "storm"], createdRule.Keywords);
        Assert.Equal(["Email", "Slack"], createdRule.Channels);
        Assert.False(createdRule.IsEnabled);

        using var listResponse = await GetAlertRulesAsync(client, "demo-user");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listDocument = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        var createdId = createdRule.Id;
        var persistedRule = Assert.Single(listDocument.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetInt32() == createdId);
        Assert.Equal(["flood", "storm"], persistedRule.GetProperty("keywords").EnumerateArray()
            .Select(keyword => keyword.GetString()));
        Assert.Equal(["Email", "Slack"], persistedRule.GetProperty("channels").EnumerateArray()
            .Select(channel => channel.GetString()));
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task CreateAlertRule_WithInvalidInput_ReturnsValidationProblem(object requestBody)
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await PostAlertRuleAsync(client, requestBody);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
    }

    public static IEnumerable<object[]> InvalidRequests =>
    [
        [new { category = "Weather" }],
        [new { name = "" }],
        [new { name = new string('n', 121) }],
        [new { name = "Valid", category = new string('c', 101) }],
        [new { name = "Valid", keywords = new[] { new string('k', 101) } }],
        [new { name = "Valid", keywords = new string?[] { "   " } }],
        [new { name = "Valid", keywords = new string?[] { "flood", null } }],
        [new { name = "Valid", keywords = new[] { "Flood", " flood " } }],
        [new { name = "Valid", channels = new[] { "Email", "Sms" } }],
        [new { name = "Valid", channels = new[] { "Slack", "Slack" } }],
        [new { name = "Valid", channels = new string?[] { null } }]
    ];

    private static async Task<HttpResponseMessage> PostAlertRuleAsync(
        HttpClient client,
        object requestBody,
        string? identity = "demo-user")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/alert-rules")
        {
            Content = JsonContent.Create(requestBody)
        };
        if (identity is not null)
        {
            request.Headers.Add("X-Demo-Identity", identity);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetAlertRulesAsync(HttpClient client, string identity)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/alert-rules?pageSize=100");
        request.Headers.Add("X-Demo-Identity", identity);
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