using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class ConcurrentEventSubmissionApiTests
{
    [Fact]
    public async Task ConcurrentIdenticalSubmissions_CreateOneEventAndOneDeliveryPerChannel()
    {
        using var factory = await ConcurrentEventsApiFactory.CreateAsync();
        using var client = factory.CreateClient(ConcurrentEventsApiFactory.TestClientOptions);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
            context.AlertRules.Add(new AlertRule
            {
                UserId = "demo-user",
                Name = "All events",
                Channels =
                [
                    new AlertRuleChannel { Channel = NotificationChannel.Email },
                    new AlertRuleChannel { Channel = NotificationChannel.Slack }
                ]
            });
            await context.SaveChangesAsync();
        }

        var requests = new[]
        {
            SubmitAsync(client),
            SubmitAsync(client)
        };
        using var firstResponse = await requests[0];
        using var secondResponse = await requests[1];

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Created],
            new[] { firstResponse.StatusCode, secondResponse.StatusCode }.OrderBy(status => (int)status));

        using var firstDocument = await JsonDocument.ParseAsync(await firstResponse.Content.ReadAsStreamAsync());
        using var secondDocument = await JsonDocument.ParseAsync(await secondResponse.Content.ReadAsStreamAsync());
        Assert.Equal(firstDocument.RootElement.GetProperty("id").GetInt32(),
            secondDocument.RootElement.GetProperty("id").GetInt32());
        Assert.NotEqual(firstDocument.RootElement.GetProperty("isDuplicate").GetBoolean(),
            secondDocument.RootElement.GetProperty("isDuplicate").GetBoolean());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        Assert.Single(await verificationContext.Events.ToListAsync());
        var deliveries = await verificationContext.NotificationDeliveries.AsNoTracking().ToListAsync();
        Assert.Collection(deliveries.OrderBy(delivery => delivery.Channel),
            emailDelivery => Assert.Equal(NotificationChannel.Email, emailDelivery.Channel),
            slackDelivery => Assert.Equal(NotificationChannel.Slack, slackDelivery.Channel));
        Assert.Single(deliveries.Select(delivery => delivery.EventId).Distinct());
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events")
        {
            Content = JsonContent.Create(new
            {
                category = "Weather",
                title = "Storm",
                description = "Heavy rain",
                occurredAt = "2026-05-01T12:30:45Z"
            })
        };
        request.Headers.Add("X-Demo-Identity", "demo-admin");
        return await client.SendAsync(request);
    }
}

internal sealed class ConcurrentEventsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"sonrisa-concurrent-{Guid.NewGuid():N}.db");
    private readonly FingerprintLookupBarrier _lookupBarrier = new();

    public static WebApplicationFactoryClientOptions TestClientOptions => new()
    {
        BaseAddress = new Uri("https://localhost")
    };

    public static async Task<ConcurrentEventsApiFactory> CreateAsync()
    {
        var factory = new ConcurrentEventsApiFactory();
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
            await context.Database.EnsureCreatedAsync();
            return factory;
        }
        catch
        {
            factory.Dispose();
            throw;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddLogging(logging => logging.ClearProviders());
            services.RemoveAll<IDbContextOptionsConfiguration<EventAlertDbContext>>();
            services.RemoveAll<DbContextOptions<EventAlertDbContext>>();
            services.RemoveAll<EventAlertDbContext>();
            services.AddDbContext<EventAlertDbContext>(options => options
                .UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = _databasePath,
                    DefaultTimeout = 30,
                    Pooling = false
                }.ToString())
                .AddInterceptors(_lookupBarrier));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

internal sealed class FingerprintLookupBarrier : DbCommandInterceptor
{
    private readonly TaskCompletionSource _bothLookupsExecuted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _fingerprintLookupCount;

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FROM \"Events\"", StringComparison.Ordinal)
            && command.CommandText.Contains("\"Fingerprint\"", StringComparison.Ordinal)
            && Interlocked.Increment(ref _fingerprintLookupCount) <= 2)
        {
            if (Volatile.Read(ref _fingerprintLookupCount) == 2)
            {
                _bothLookupsExecuted.TrySetResult();
            }

            await _bothLookupsExecuted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }

        return result;
    }
}