using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class DemoIdentitiesApiTests
{
    [Fact]
    public async Task GetDemoIdentities_ReturnsPredefinedIdentitiesWithoutIdentityHeader()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await client.GetAsync("/api/demo-identities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var identities = await response.Content.ReadFromJsonAsync<IdentityResponse[]>()
            ?? throw new InvalidOperationException("The response body did not contain an identity array.");

        Assert.Equal(
            [
                new IdentityResponse("demo-user", "Demo User", "User"),
                new IdentityResponse("demo-admin", "Demo Administrator", "Admin")
            ],
            identities);
    }

    [Fact]
    public async Task GetDemoIdentities_ExposesOnlyApprovedDtoFields()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await client.GetAsync("/api/demo-identities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);

        foreach (var identity in document.RootElement.EnumerateArray())
        {
            var fieldNames = identity.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(["displayName", "id", "role"], fieldNames);
        }
    }

    [Fact]
    public async Task GetDemoIdentities_RemainsPublicWithUnknownIdentityHeader()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/demo-identities");
        request.Headers.Add("X-Demo-Identity", "unknown-identity");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record IdentityResponse(string Id, string DisplayName, string Role);
}