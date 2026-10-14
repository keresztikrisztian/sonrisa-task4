using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class NotificationDeliveryRecordingWorkerTests
{
    [Fact]
    public async Task SubmittedDelivery_IsAsynchronouslyRecordedWithoutMarkingItSent()
    {
        using var factory = await AlertRulesApiFactory.CreateAsync();
        using var client = factory.CreateClient(AlertRulesApiFactory.TestClientOptions);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
            context.AlertRules.Add(new AlertRule
            {
                UserId = "demo-user",
                Name = "All events",
                Channels = [new AlertRuleChannel { Channel = NotificationChannel.Email }]
            });
            await context.SaveChangesAsync();
        }

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
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        NotificationDelivery? recordedDelivery = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
            recordedDelivery = await context.NotificationDeliveries.AsNoTracking().SingleAsync();
            if (recordedDelivery.Status == NotificationDeliveryStatus.Recorded)
            {
                break;
            }

            await Task.Delay(25);
        }

        Assert.NotNull(recordedDelivery);
        Assert.Equal(NotificationDeliveryStatus.Recorded, recordedDelivery.Status);
        Assert.Equal(0, recordedDelivery.AttemptCount);
        Assert.Null(recordedDelivery.SentAtUtc);
        Assert.True(recordedDelivery.UpdatedAtUtc >= recordedDelivery.CreatedAtUtc);
    }
}