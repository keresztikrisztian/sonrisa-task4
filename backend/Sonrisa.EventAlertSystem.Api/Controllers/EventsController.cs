using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sonrisa.EventAlertSystem.Api.Application;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;

namespace Sonrisa.EventAlertSystem.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize(Policy = "Events.Admin")]
public sealed class EventsController(EventAlertDbContext context, AlertMatchingService matchingService) : ControllerBase
{
    private static readonly Regex IsoTimestampWithOffset = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant);

    [HttpPost]
    [ProducesResponseType(typeof(EventSubmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(EventSubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventSubmissionResponse>> Submit(
        [FromBody] CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseOccurredAt(request.OccurredAt, out var occurredAt))
        {
            ModelState.AddModelError(nameof(request.OccurredAt), "OccurredAt must be an ISO-8601 timestamp with an explicit UTC offset.");
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Title))
        {
            if (string.IsNullOrWhiteSpace(request.Category))
            {
                ModelState.AddModelError(nameof(request.Category), "Category must not be empty or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                ModelState.AddModelError(nameof(request.Title), "Title must not be empty or whitespace.");
            }

            return ValidationProblem(ModelState);
        }

        var fingerprint = EventFingerprint.Create(request.Category, request.Title, request.Description!, occurredAt);
        var existing = await context.Events.AsNoTracking()
            .FirstOrDefaultAsync(eventItem => eventItem.Fingerprint == fingerprint, cancellationToken);
        if (existing is not null)
        {
            return Ok(ToSubmissionResponse(existing, isDuplicate: true));
        }

        var eventItem = new Event
        {
            Fingerprint = fingerprint,
            Category = request.Category,
            Title = request.Title,
            Description = request.Description!,
            OccurredAtUtc = occurredAt.UtcDateTime
        };

        var rules = await context.AlertRules
            .AsSplitQuery()
            .Include(rule => rule.Keywords)
            .Include(rule => rule.Channels)
            .Where(rule => rule.IsEnabled && !rule.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var rule in matchingService.Match(eventItem, rules))
        {
            foreach (var channel in rule.Channels.Select(configuredChannel => configuredChannel.Channel)
                         .Where(channel => channel is NotificationChannel.Email or NotificationChannel.Slack))
            {
                eventItem.Deliveries.Add(new NotificationDelivery
                {
                    AlertRule = rule,
                    Channel = channel
                });
            }
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.Events.Add(eventItem);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsFingerprintUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            var duplicate = await context.Events.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Fingerprint == fingerprint, cancellationToken);
            if (duplicate is null)
            {
                throw;
            }

            return Ok(ToSubmissionResponse(duplicate, isDuplicate: true));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict();
        }

        return StatusCode(StatusCodes.Status201Created, ToSubmissionResponse(eventItem, isDuplicate: false));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponse<EventResponse>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? fromUtc,
        [FromQuery] string? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidatePagination(page, pageSize);
        DateTimeOffset? from = null;
        DateTimeOffset? to = null;
        if (fromUtc is not null)
        {
            if (TryParseOccurredAt(fromUtc, out var parsedFrom))
            {
                from = parsedFrom;
            }
            else
            {
                errors["fromUtc"] = ["fromUtc must be an ISO-8601 timestamp with an explicit UTC offset."];
            }
        }

        if (toUtc is not null)
        {
            if (TryParseOccurredAt(toUtc, out var parsedTo))
            {
                to = parsedTo;
            }
            else
            {
                errors["toUtc"] = ["toUtc must be an ISO-8601 timestamp with an explicit UTC offset."];
            }
        }

        if (from.HasValue && to.HasValue && from.Value >= to.Value)
        {
            errors["toUtc"] = ["toUtc must be later than fromUtc."];
        }

        if (errors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(errors));
        }

        var events = context.Events.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim().ToUpperInvariant();
            events = events.Where(eventItem => eventItem.Category.ToUpper() == normalizedCategory);
        }

        if (from.HasValue)
        {
            var fromValue = from.Value.UtcDateTime;
            events = events.Where(eventItem => eventItem.OccurredAtUtc >= fromValue);
        }

        if (to.HasValue)
        {
            var toValue = to.Value.UtcDateTime;
            events = events.Where(eventItem => eventItem.OccurredAtUtc < toValue);
        }

        var totalCount = await events.CountAsync(cancellationToken);
        var items = await events
            .OrderByDescending(eventItem => eventItem.OccurredAtUtc)
            .ThenByDescending(eventItem => eventItem.Id)
            .Skip((int)((long)(page - 1) * pageSize))
            .Take(pageSize)
            .Select(eventItem => new EventResponse(
                eventItem.Id,
                eventItem.Category,
                eventItem.Title,
                eventItem.Description,
                eventItem.OccurredAtUtc,
                eventItem.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PaginatedResponse<EventResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var eventItem = await context.Events
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Deliveries)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (eventItem is null)
        {
            return NotFound();
        }

        var deliveries = eventItem.Deliveries
            .OrderByDescending(delivery => delivery.CreatedAtUtc)
            .ThenByDescending(delivery => delivery.Id)
            .Select(ToDeliveryResponse)
            .ToArray();
        return Ok(new EventDetailResponse(
            eventItem.Id,
            eventItem.Category,
            eventItem.Title,
            eventItem.Description,
            eventItem.OccurredAtUtc,
            eventItem.CreatedAtUtc,
            deliveries));
    }

    private static bool TryParseOccurredAt(string? value, out DateTimeOffset occurredAt)
    {
        occurredAt = default;
        return value is not null
            && IsoTimestampWithOffset.IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out occurredAt);
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

    private static bool IsFingerprintUniqueViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        } sqliteException
            && sqliteException.Message.Contains("Events.Fingerprint", StringComparison.Ordinal);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 1555 or 2067
        };
    }

    private static EventSubmissionResponse ToSubmissionResponse(Event eventItem, bool isDuplicate)
    {
        return new EventSubmissionResponse(
            eventItem.Id,
            eventItem.Category,
            eventItem.Title,
            eventItem.Description,
            eventItem.OccurredAtUtc,
            eventItem.CreatedAtUtc,
            isDuplicate);
    }

    private static NotificationDeliveryResponse ToDeliveryResponse(NotificationDelivery delivery)
    {
        return new NotificationDeliveryResponse(
            delivery.Id,
            delivery.EventId,
            delivery.AlertRuleId,
            delivery.Channel.ToString(),
            delivery.Status.ToString(),
            delivery.AttemptCount,
            delivery.CreatedAtUtc,
            delivery.UpdatedAtUtc,
            delivery.SentAtUtc);
    }

    public sealed record PaginatedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

    public sealed record EventResponse(
        int Id,
        string Category,
        string Title,
        string Description,
        DateTime OccurredAtUtc,
        DateTime CreatedAtUtc);

    public sealed record EventDetailResponse(
        int Id,
        string Category,
        string Title,
        string Description,
        DateTime OccurredAtUtc,
        DateTime CreatedAtUtc,
        IReadOnlyList<NotificationDeliveryResponse> Deliveries);

    public sealed record NotificationDeliveryResponse(
        int Id,
        int EventId,
        int AlertRuleId,
        string Channel,
        string Status,
        int AttemptCount,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime? SentAtUtc);

    public sealed record CreateEventRequest
    {
        [Required]
        [StringLength(100)]
        public string? Category { get; init; }

        [Required]
        [StringLength(300)]
        public string? Title { get; init; }

        [Required(AllowEmptyStrings = true)]
        [StringLength(4000)]
        public string? Description { get; init; }

        [Required]
        public string? OccurredAt { get; init; }
    }

    public sealed record EventSubmissionResponse(
        int Id,
        string Category,
        string Title,
        string Description,
        DateTime OccurredAtUtc,
        DateTime CreatedAtUtc,
        bool IsDuplicate);
}