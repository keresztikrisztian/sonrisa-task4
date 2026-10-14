using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Application;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class AlertRulesAuthorizationApiTests
{
    [Fact]
    public async Task GetAlertRules_WithoutIdentityHeader_ReturnsUnauthorized()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await GetAlertRulesAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAlertRules_WithUnknownIdentity_ReturnsUnauthorized()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await GetAlertRulesAsync(client, "unknown-identity");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAlertRules_WithDemoAdmin_ReturnsForbidden()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await GetAlertRulesAsync(client, "demo-admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAlertRules_WithDemoUser_ReturnsOk()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await GetAlertRulesAsync(client, "demo-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadPaginatedRulesAsync(response);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.ItemIds);
    }

    [Fact]
    public async Task GetAlertRules_WithCaseInsensitiveIdentity_UsesCanonicalOwnerId()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);

        var ownedRule = new AlertRule { UserId = "demo-user", Name = "Owned rule" };
        var otherRule = new AlertRule { UserId = "demo-admin", Name = "Other rule" };
        await factory.SeedRulesAsync(ownedRule, otherRule);

        using var response = await GetAlertRulesAsync(client, "DEMO-USER");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadPaginatedRulesAsync(response);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal([ownedRule.Id], result.ItemIds);
    }

    [Fact]
    public async Task GetAlertRules_ExcludesNonOwnedAndLogicallyDeletedRules()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);

        var ownedRule = new AlertRule { UserId = "demo-user", Name = "Owned rule" };
        var otherRule = new AlertRule { UserId = "demo-admin", Name = "Other rule" };
        var deletedRule = new AlertRule
        {
            UserId = "demo-user",
            Name = "Deleted rule",
            IsDeleted = true
        };
        await factory.SeedRulesAsync(ownedRule, otherRule, deletedRule);

        using var response = await GetAlertRulesAsync(client, "demo-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadPaginatedRulesAsync(response);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal([ownedRule.Id], result.ItemIds);
    }

    [Fact]
    public async Task GetAlertRules_UsesDefaultPaginationAndStableOrdering()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rules = Enumerable.Range(0, 25)
            .Select(index => new AlertRule
            {
                UserId = "demo-user",
                Name = $"Rule {index}",
                CreatedAtUtc = createdAt.AddMinutes(index)
            })
            .ToArray();
        await factory.SeedRulesAsync(rules);

        using var response = await GetAlertRulesAsync(client, "demo-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadPaginatedRulesAsync(response);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(
            rules.OrderByDescending(rule => rule.CreatedAtUtc)
                .ThenByDescending(rule => rule.Id)
                .Take(20)
                .Select(rule => rule.Id),
            result.ItemIds);
    }

    [Fact]
    public async Task GetAlertRules_WithExplicitPagination_ReturnsOnlyRequestedPage()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rules = Enumerable.Range(0, 5)
            .Select(index => new AlertRule
            {
                UserId = "demo-user",
                Name = $"Rule {index}",
                CreatedAtUtc = createdAt.AddMinutes(index)
            })
            .ToArray();
        await factory.SeedRulesAsync(rules);

        using var response = await GetAlertRulesAsync(client, "demo-user", "?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadPaginatedRulesAsync(response);

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(
            rules.OrderByDescending(rule => rule.CreatedAtUtc)
                .ThenByDescending(rule => rule.Id)
                .Skip(2)
                .Take(2)
                .Select(rule => rule.Id),
            result.ItemIds);
        Assert.Equal(2, result.ItemIds.Length);
    }

    [Fact]
    public async Task GetAlertRules_UsesIdDescendingAsStableTieBreaker()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rules = Enumerable.Range(0, 3)
            .Select(index => new AlertRule
            {
                UserId = "demo-user",
                Name = $"Rule {index}",
                CreatedAtUtc = createdAt
            })
            .ToArray();
        await factory.SeedRulesAsync(rules);

        using var response = await GetAlertRulesAsync(client, "demo-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadPaginatedRulesAsync(response);

        Assert.Equal(rules.OrderByDescending(rule => rule.Id).Select(rule => rule.Id), result.ItemIds);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?page=abc")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=abc")]
    public async Task GetAlertRules_WithInvalidPagination_ReturnsBadRequest(string queryString)
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        using var response = await GetAlertRulesAsync(client, "demo-user", queryString);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetAlertRulesAsync(
        HttpClient client,
        string? identity = null,
        string queryString = "")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/alert-rules{queryString}");
        if (identity is not null)
        {
            request.Headers.Add("X-Demo-Identity", identity);
        }

        return await client.SendAsync(request);
    }

    private static async Task<PaginatedRules> ReadPaginatedRulesAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);

        return new PaginatedRules(
            root.GetProperty("page").GetInt32(),
            root.GetProperty("pageSize").GetInt32(),
            root.GetProperty("totalCount").GetInt32(),
            root.GetProperty("items").EnumerateArray()
            .Select(rule => rule.GetProperty("id").GetInt32())
            .ToArray());
    }

    private sealed record PaginatedRules(int Page, int PageSize, int TotalCount, int[] ItemIds);
}

internal sealed class AlertRulesApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly INotificationDeliveryRecorder? _deliveryRecorder;

    public static WebApplicationFactoryClientOptions TestClientOptions => new()
    {
        BaseAddress = new Uri("https://localhost")
    };

    private AlertRulesApiFactory(INotificationDeliveryRecorder? deliveryRecorder)
    {
        _deliveryRecorder = deliveryRecorder;
        _connection.Open();
    }

    public static async Task<AlertRulesApiFactory> CreateAsync(
        INotificationDeliveryRecorder? deliveryRecorder = null)
    {
        var factory = new AlertRulesApiFactory(deliveryRecorder);
        try
        {
            await factory.InitializeDatabaseAsync();
            return factory;
        }
        catch
        {
            factory.Dispose();
            throw;
        }
    }

    public async Task SeedRulesAsync(params AlertRule[] rules)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        await context.AlertRules.AddRangeAsync(rules);
        await context.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddLogging(logging => logging.ClearProviders());
            services.RemoveAll<IDbContextOptionsConfiguration<EventAlertDbContext>>();
            services.RemoveAll<DbContextOptions<EventAlertDbContext>>();
            services.RemoveAll<EventAlertDbContext>();
            services.AddDbContext<EventAlertDbContext>(options => options.UseSqlite(_connection));
            if (_deliveryRecorder is not null)
            {
                services.RemoveAll<INotificationDeliveryRecorder>();
                services.AddSingleton(_deliveryRecorder);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private async Task InitializeDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}