using Sonrisa.EventAlertSystem.Api.Domain;

namespace Sonrisa.EventAlertSystem.Api.Application;

public sealed class AlertMatchingService
{
    public IReadOnlyList<AlertRule> Match(Event eventItem, IEnumerable<AlertRule> rules)
    {
        return rules
            .Where(rule => rule.IsEnabled && !rule.IsDeleted)
            .Where(rule => CategoryMatches(rule, eventItem))
            .Where(rule => KeywordsMatch(rule, eventItem))
            .ToArray();
    }

    private static bool CategoryMatches(AlertRule rule, Event eventItem)
    {
        return string.IsNullOrWhiteSpace(rule.Category)
            || string.Equals(rule.Category.Trim(), eventItem.Category.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool KeywordsMatch(AlertRule rule, Event eventItem)
    {
        var keywords = rule.Keywords
            .Select(keyword => keyword.Keyword.Trim())
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword));

        return !keywords.Any()
            || keywords.Any(keyword => eventItem.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || eventItem.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}