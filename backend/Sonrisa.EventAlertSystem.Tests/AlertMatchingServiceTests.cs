using Sonrisa.EventAlertSystem.Api.Application;
using Sonrisa.EventAlertSystem.Api.Domain;
using Xunit;

namespace Sonrisa.EventAlertSystem.Tests;

public sealed class AlertMatchingServiceTests
{
    private readonly AlertMatchingService _service = new();

    [Fact]
    public void CategoryOnlyRule_MatchesEqualCategory()
    {
        var rule = CreateRule(category: "Weather");

        var matches = _service.Match(CreateEvent(category: "Weather"), [rule]);

        Assert.Single(matches);
    }

    [Fact]
    public void CategoryOnlyRule_TrimsSurroundingWhitespace()
    {
        var rule = CreateRule(category: "  Weather  ");

        var matches = _service.Match(CreateEvent(category: " Weather "), [rule]);

        Assert.Single(matches);
    }

    [Fact]
    public void KeywordOnlyRule_MatchesTitleOrDescription()
    {
        var rule = CreateRule(keywords: ["flood"]);

        var matches = _service.Match(CreateEvent(description: "A flood warning was issued"), [rule]);

        Assert.Single(matches);
    }

    [Fact]
    public void KeywordOnlyRule_TrimsSurroundingWhitespace()
    {
        var rule = CreateRule(keywords: ["  flood  "]);

        var matches = _service.Match(CreateEvent(title: "  Flood warning  "), [rule]);

        Assert.Single(matches);
    }

    [Fact]
    public void RuleWithOnlyEmptyOrWhitespaceKeywords_MatchesAnyEvent()
    {
        var rule = CreateRule(keywords: ["", "   ", "\t"]);

        var matches = _service.Match(CreateEvent(title: "Clear skies"), [rule]);

        Assert.Single(matches);
    }

    [Fact]
    public void NonMatchingKeyword_DoesNotMatch()
    {
        var rule = CreateRule(keywords: ["earthquake"]);

        var matches = _service.Match(CreateEvent(title: "Clear skies", description: "Forecast update"), [rule]);

        Assert.Empty(matches);
    }

    [Fact]
    public void CategoryAndKeywordRule_RequiresBothConditions()
    {
        var rule = CreateRule(category: "Weather", keywords: ["flood"]);

        var categoryOnlyEvent = CreateEvent(category: "Weather", title: "Rain expected");
        var keywordOnlyEvent = CreateEvent(category: "News", title: "Flood warning");

        Assert.Empty(_service.Match(categoryOnlyEvent, [rule]));
        Assert.Empty(_service.Match(keywordOnlyEvent, [rule]));
        Assert.Single(_service.Match(CreateEvent(category: "Weather", title: "Flood warning"), [rule]));
    }

    [Fact]
    public void MultipleKeywords_UseOrMatching()
    {
        var rule = CreateRule(keywords: ["flood", "wildfire"]);

        var matches = _service.Match(CreateEvent(title: "Wildfire evacuation"), [rule]);

        Assert.Single(matches);
    }

    [Fact]
    public void CategoryAndKeywordComparisons_AreCaseInsensitive()
    {
        var rule = CreateRule(category: "WEATHER", keywords: ["FLOOD"]);

        var matches = _service.Match(CreateEvent(category: "weather", title: "Flood warning"), [rule]);

        Assert.Single(matches);
    }

    [Fact]
    public void EmptyConditions_MatchAnyEvent()
    {
        var rule = CreateRule(category: " ");

        var matches = _service.Match(CreateEvent(category: "Markets"), [rule]);

        Assert.Single(matches);
    }

    [Fact]
    public void DisabledAndLogicallyDeletedRules_DoNotMatch()
    {
        var disabledRule = CreateRule();
        disabledRule.IsEnabled = false;
        var deletedRule = CreateRule();
        deletedRule.IsDeleted = true;

        var matches = _service.Match(CreateEvent(), [disabledRule, deletedRule]);

        Assert.Empty(matches);
    }

    [Fact]
    public void Match_ReturnsEveryMatchingRule()
    {
        var rules = new[]
        {
            CreateRule(category: "Weather"),
            CreateRule(keywords: ["storm"]),
            CreateRule(category: "Markets")
        };

        var matches = _service.Match(CreateEvent(category: "Weather", title: "Storm approaching"), rules);

        Assert.Equal(2, matches.Count);
        Assert.Contains(rules[0], matches);
        Assert.Contains(rules[1], matches);
    }

    private static AlertRule CreateRule(string? category = null, string[]? keywords = null)
    {
        var rule = new AlertRule { UserId = "demo-user", Name = "Test rule", Category = category };
        foreach (var keyword in keywords ?? [])
        {
            rule.Keywords.Add(new AlertRuleKeyword { Keyword = keyword });
        }

        return rule;
    }

    private static Event CreateEvent(
        string category = "Weather",
        string title = "Clear skies",
        string description = "Forecast update")
    {
        return new Event
        {
            Fingerprint = Guid.NewGuid().ToString("N"),
            Category = category,
            Title = title,
            Description = description,
            OccurredAtUtc = DateTime.UtcNow
        };
    }
}