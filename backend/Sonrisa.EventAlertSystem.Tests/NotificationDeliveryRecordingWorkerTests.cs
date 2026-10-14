using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sonrisa.EventAlertSystem.Api.Application;
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
        Assert.Equal(1, recordedDelivery.AttemptCount);
        Assert.Null(recordedDelivery.SentAtUtc);
        Assert.True(recordedDelivery.UpdatedAtUtc >= recordedDelivery.CreatedAtUtc);
    }

    [Fact]
    public async Task TransientFailure_IsRetriedAndEventuallyRecorded()
    {
        var recorder = new TestDeliveryRecorder((_, attempt) =>
            attempt == 1 ? throw new InvalidOperationException("temporary failure") : Task.CompletedTask);
        using var factory = await AlertRulesApiFactory.CreateAsync(recorder);
        await SeedDeliveryAsync(factory, NotificationDeliveryStatus.Pending);

        var delivery = await WaitForDeliveryAsync(factory, status => status == NotificationDeliveryStatus.Recorded);

        Assert.Equal(NotificationDeliveryStatus.Recorded, delivery.Status);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Null(delivery.LastError);
        Assert.Null(delivery.NextAttemptAtUtc);
        Assert.Equal(2, recorder.Attempts);
    }

    [Fact]
    public async Task RepeatedFailures_EndInPermanentFailureAfterThreeAttempts()
    {
        var recorder = new TestDeliveryRecorder((_, _) => throw new InvalidOperationException("delivery failed"));
        using var factory = await AlertRulesApiFactory.CreateAsync(recorder);
        await SeedDeliveryAsync(factory, NotificationDeliveryStatus.Pending);

        var delivery = await WaitForDeliveryAsync(factory, status => status == NotificationDeliveryStatus.Failed);

        Assert.Equal(NotificationDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(3, delivery.AttemptCount);
        Assert.Equal("delivery failed", delivery.LastError);
        Assert.Null(delivery.NextAttemptAtUtc);
        Assert.Equal(3, recorder.Attempts);
    }

    [Fact]
    public async Task PermanentFailure_IsTerminalWithoutRetry()
    {
        var recorder = new TestDeliveryRecorder((_, _) =>
            throw new NotificationDeliveryException("invalid destination", isTransient: false));
        using var factory = await AlertRulesApiFactory.CreateAsync(recorder);
        await SeedDeliveryAsync(factory, NotificationDeliveryStatus.Pending);

        var delivery = await WaitForDeliveryAsync(factory, status => status == NotificationDeliveryStatus.Failed);

        Assert.Equal(NotificationDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal("invalid destination", delivery.LastError);
        Assert.Null(delivery.NextAttemptAtUtc);
        Assert.Equal(1, recorder.Attempts);
    }

    [Fact]
    public async Task StaleProcessingDelivery_IsRecoveredAfterRestart()
    {
        var recorder = new TestDeliveryRecorder((_, _) => Task.CompletedTask);
        using var factory = await AlertRulesApiFactory.CreateAsync(recorder);
        await SeedDeliveryAsync(factory, NotificationDeliveryStatus.Processing, attemptCount: 1,
            updatedAtUtc: DateTime.UtcNow.AddMinutes(-2));

        var delivery = await WaitForDeliveryAsync(factory, status => status == NotificationDeliveryStatus.Recorded);

        Assert.Equal(2, delivery.AttemptCount);
        Assert.Equal(1, recorder.Attempts);
    }

    [Fact]
    public async Task ConcurrentWorkers_ClaimDeliveryOnlyOnce()
    {
        var recorder = new BlockingDeliveryRecorder();
        using var factory = await AlertRulesApiFactory.CreateAsync(recorder);
        await SeedDeliveryAsync(factory, NotificationDeliveryStatus.Pending);
        var secondWorker = new NotificationDeliveryRecordingWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>());
        await secondWorker.StartAsync(CancellationToken.None);
        NotificationDelivery delivery;

        try
        {
            await recorder.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromSeconds(2));
            recorder.Release.TrySetResult();
            delivery = await WaitForDeliveryAsync(factory, status => status == NotificationDeliveryStatus.Recorded);
        }
        finally
        {
            recorder.Release.TrySetResult();
            await secondWorker.StopAsync(CancellationToken.None);
        }

        Assert.Equal(1, recorder.Attempts);
        Assert.Equal(1, delivery.AttemptCount);
    }

    private static async Task SeedDeliveryAsync(
        AlertRulesApiFactory factory,
        NotificationDeliveryStatus status,
        int attemptCount = 0,
        DateTime? updatedAtUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
        var rule = new AlertRule { UserId = "demo-user", Name = "All events" };
        var @event = new Event
        {
            Fingerprint = Guid.NewGuid().ToString("N"),
            Category = "Weather",
            Title = "Storm",
            Description = "Heavy rain",
            OccurredAtUtc = DateTime.UtcNow
        };
        context.NotificationDeliveries.Add(new NotificationDelivery
        {
            Event = @event,
            AlertRule = rule,
            Channel = NotificationChannel.Email,
            Status = status,
            AttemptCount = attemptCount,
            UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task<NotificationDelivery> WaitForDeliveryAsync(
        AlertRulesApiFactory factory,
        Func<NotificationDeliveryStatus, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        NotificationDelivery? lastDelivery = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EventAlertDbContext>();
            lastDelivery = await context.NotificationDeliveries.AsNoTracking().SingleAsync();
            if (condition(lastDelivery.Status))
            {
                return lastDelivery;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Notification delivery reached {lastDelivery?.Status} after {lastDelivery?.AttemptCount} attempts.");
    }

    private sealed class TestDeliveryRecorder(Func<NotificationDelivery, int, Task> record)
        : INotificationDeliveryRecorder
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        public Task RecordAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = Interlocked.Increment(ref _attempts);
            return record(delivery, attempt);
        }
    }

    private sealed class BlockingDeliveryRecorder : INotificationDeliveryRecorder
    {
        private int _attempts;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Attempts => Volatile.Read(ref _attempts);

        public async Task RecordAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }
}