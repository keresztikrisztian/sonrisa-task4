using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;

namespace Sonrisa.EventAlertSystem.Api.Controllers;

[ApiController]
[Route("api/notification-deliveries")]
[Authorize(Policy = "Events.Admin")]
public sealed class NotificationDeliveriesController(EventAlertDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<DeliveryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponse<DeliveryResponse>>> GetAll(
        [FromQuery] int? eventId,
        [FromQuery] string? status,
        [FromQuery] string? channel,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidatePagination(page, pageSize);
        if (eventId.HasValue && eventId.Value < 1)
        {
            errors["eventId"] = ["eventId must be greater than zero."];
        }

        NotificationDeliveryStatus? parsedStatus = null;
        if (status is not null)
        {
            if (Enum.TryParse<NotificationDeliveryStatus>(status, ignoreCase: true, out var value)
                && Enum.IsDefined(value))
            {
                parsedStatus = value;
            }
            else
            {
                errors["status"] = ["status is not a supported delivery status."];
            }
        }

        NotificationChannel? parsedChannel = null;
        if (channel is not null)
        {
            if (Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out var value)
                && Enum.IsDefined(value))
            {
                parsedChannel = value;
            }
            else
            {
                errors["channel"] = ["channel must be Email or Slack."];
            }
        }

        if (errors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(errors));
        }

        var deliveries = context.NotificationDeliveries.AsNoTracking();
        if (eventId.HasValue)
        {
            deliveries = deliveries.Where(delivery => delivery.EventId == eventId.Value);
        }

        if (parsedStatus.HasValue)
        {
            deliveries = deliveries.Where(delivery => delivery.Status == parsedStatus.Value);
        }

        if (parsedChannel.HasValue)
        {
            deliveries = deliveries.Where(delivery => delivery.Channel == parsedChannel.Value);
        }

        var totalCount = await deliveries.CountAsync(cancellationToken);
        var items = await deliveries
            .OrderByDescending(delivery => delivery.CreatedAtUtc)
            .ThenByDescending(delivery => delivery.Id)
            .Skip((int)((long)(page - 1) * pageSize))
            .Take(pageSize)
            .Select(delivery => new DeliveryResponse(
                delivery.Id,
                delivery.EventId,
                delivery.AlertRuleId,
                delivery.Channel.ToString(),
                delivery.Status.ToString(),
                delivery.AttemptCount,
                delivery.CreatedAtUtc,
                delivery.UpdatedAtUtc,
                delivery.SentAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PaginatedResponse<DeliveryResponse>(items, page, pageSize, totalCount));
    }

    private static Dictionary<string, string[]> ValidatePagination(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (pageSize < 1 || pageSize > 100)
        {
            errors["pageSize"] = ["Page size must be between 1 and 100."];
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            errors["page"] = ["Page is too large."];
        }

        return errors;
    }

    public sealed record PaginatedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

    public sealed record DeliveryResponse(
        int Id,
        int EventId,
        int AlertRuleId,
        string Channel,
        string Status,
        int AttemptCount,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime? SentAtUtc);
}