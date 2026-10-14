using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sonrisa.EventAlertSystem.Api.Domain;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;

namespace Sonrisa.EventAlertSystem.Api.Controllers;

[ApiController]
[Route("api/alert-rules")]
[Authorize(Policy = "AlertRules.User")]
public sealed class AlertRulesController(EventAlertDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<AlertRuleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponse<AlertRuleResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return Unauthorized();
        }

        var errors = new Dictionary<string, string[]>();
        if (page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (pageSize < 1 || pageSize > 100)
        {
            errors["pageSize"] = ["Page size must be between 1 and 100."];
        }

        if (errors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(errors));
        }

        var offset = (long)(page - 1) * pageSize;
        if (offset > int.MaxValue)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["page"] = ["Page is too large."]
            }));
        }

        var ownedRules = context.AlertRules
            .AsNoTracking()
            .Where(rule => rule.UserId == ownerId && !rule.IsDeleted);

        var totalCount = await ownedRules.CountAsync(cancellationToken);
        var items = await ownedRules
            .OrderByDescending(rule => rule.CreatedAtUtc)
            .ThenByDescending(rule => rule.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .AsSplitQuery()
            .Select(rule => new AlertRuleResponse(
                rule.Id,
                rule.Name,
                rule.Category,
                rule.Keywords.Select(keyword => keyword.Keyword).ToArray(),
                rule.Channels.Select(channel => channel.Channel.ToString()).ToArray(),
                rule.IsEnabled,
                rule.CreatedAtUtc,
                rule.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PaginatedResponse<AlertRuleResponse>(items, page, pageSize, totalCount));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AlertRuleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AlertRuleResponse>> Create(
        [FromBody] CreateAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return Unauthorized();
        }

        if (!TryNormalizeCollections(request.Keywords, request.Channels, out var keywords, out var channels))
        {
            return ValidationProblem(ModelState);
        }

        var rule = new AlertRule
        {
            UserId = ownerId,
            Name = request.Name!,
            Category = request.Category,
            IsEnabled = request.IsEnabled ?? true
        };
        foreach (var keyword in keywords)
        {
            rule.Keywords.Add(new AlertRuleKeyword { Keyword = keyword });
        }

        foreach (var channel in channels)
        {
            rule.Channels.Add(new AlertRuleChannel { Channel = channel });
        }

        context.AlertRules.Add(rule);
        await context.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ToResponse(rule));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AlertRuleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertRuleResponse>> Update(
        int id,
        [FromBody] UpdateAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return Unauthorized();
        }

        if (!TryNormalizeCollections(request.Keywords, request.Channels, out var keywords, out var channels))
        {
            return ValidationProblem(ModelState);
        }

        var rule = await context.AlertRules
            .AsSplitQuery()
            .Include(existing => existing.Keywords)
            .Include(existing => existing.Channels)
            .FirstOrDefaultAsync(existing =>
                existing.Id == id && existing.UserId == ownerId && !existing.IsDeleted,
                cancellationToken);

        if (rule is null)
        {
            return NotFound();
        }

        context.AlertRuleKeywords.RemoveRange(rule.Keywords);
        context.AlertRuleChannels.RemoveRange(rule.Channels);
        rule.Keywords.Clear();
        rule.Channels.Clear();
        rule.Name = request.Name!;
        rule.Category = request.Category;
        rule.IsEnabled = request.IsEnabled!.Value;
        rule.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var keyword in keywords)
        {
            rule.Keywords.Add(new AlertRuleKeyword { Keyword = keyword });
        }

        foreach (var channel in channels)
        {
            rule.Channels.Add(new AlertRuleChannel { Channel = channel });
        }

        await context.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(rule));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return Unauthorized();
        }

        var rule = await context.AlertRules.FirstOrDefaultAsync(existing =>
            existing.Id == id && existing.UserId == ownerId && !existing.IsDeleted,
            cancellationToken);

        if (rule is null)
        {
            return NotFound();
        }

        rule.IsDeleted = true;
        rule.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    public sealed record PaginatedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

    public sealed record CreateAlertRuleRequest
    {
        [Required]
        [StringLength(120)]
        public string? Name { get; init; }

        [StringLength(100)]
        public string? Category { get; init; }

        public List<string?>? Keywords { get; init; }

        public List<string?>? Channels { get; init; }

        public bool? IsEnabled { get; init; }
    }

    public sealed record UpdateAlertRuleRequest
    {
        [Required]
        [StringLength(120)]
        public string? Name { get; init; }

        [StringLength(100)]
        public string? Category { get; init; }

        [Required]
        public List<string?>? Keywords { get; init; }

        [Required]
        public List<string?>? Channels { get; init; }

        [Required]
        public bool? IsEnabled { get; init; }
    }

    public sealed record AlertRuleResponse(
        int Id,
        string Name,
        string? Category,
        IReadOnlyList<string> Keywords,
        IReadOnlyList<string> Channels,
        bool IsEnabled,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private static AlertRuleResponse ToResponse(AlertRule rule)
    {
        return new AlertRuleResponse(
            rule.Id,
            rule.Name,
            rule.Category,
            rule.Keywords.Select(keyword => keyword.Keyword).ToArray(),
            rule.Channels.Select(channel => channel.Channel.ToString()).ToArray(),
            rule.IsEnabled,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc);
    }

    private bool TryNormalizeCollections(
        IEnumerable<string?>? submittedKeywords,
        IEnumerable<string?>? submittedChannels,
        out List<string> keywords,
        out List<NotificationChannel> channels)
    {
        keywords = [];
        var seenKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var submittedKeyword in submittedKeywords ?? [])
        {
            var keyword = submittedKeyword?.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                ModelState.AddModelError(nameof(CreateAlertRuleRequest.Keywords), "Keywords cannot be null, empty, or whitespace.");
                continue;
            }

            if (keyword.Length > 100)
            {
                ModelState.AddModelError(nameof(CreateAlertRuleRequest.Keywords), "Keywords cannot exceed 100 characters.");
                continue;
            }

            if (!seenKeywords.Add(keyword))
            {
                ModelState.AddModelError(nameof(CreateAlertRuleRequest.Keywords), "Keywords must be unique after trimming, ignoring case.");
                continue;
            }

            keywords.Add(keyword);
        }

        channels = [];
        var seenChannels = new HashSet<NotificationChannel>();
        foreach (var submittedChannel in submittedChannels ?? [])
        {
            if (submittedChannel is not ("Email" or "Slack"))
            {
                ModelState.AddModelError(nameof(CreateAlertRuleRequest.Channels), "Channels must be Email or Slack.");
                continue;
            }

            var channel = submittedChannel == "Email" ? NotificationChannel.Email : NotificationChannel.Slack;
            if (!seenChannels.Add(channel))
            {
                ModelState.AddModelError(nameof(CreateAlertRuleRequest.Channels), "Channels must not contain duplicates.");
                continue;
            }

            channels.Add(channel);
        }

        return ModelState.IsValid;
    }
}