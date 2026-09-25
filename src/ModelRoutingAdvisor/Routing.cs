namespace ModelRoutingAdvisor;

public enum ModelPath
{
    LowCost,
    HighCapability
}

public sealed record RoutingDecision(ModelPath Path, int Score, IReadOnlyList<string> Reasons);

public sealed class DeterministicRoutingPolicy
{
    private const int HighCapabilityThreshold = 4;

    private static readonly string[] CapabilityKeywords =
    [
        "architecture",
        "compare",
        "diagnose",
        "migration",
        "optimize",
        "reason",
        "security",
        "trade-off",
        "tradeoff"
    ];

    public RoutingDecision Decide(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var score = 0;
        var reasons = new List<string>();

        if (prompt.Length >= 800)
        {
            score += 3;
            reasons.Add("long prompt (+3)");
        }

        if (prompt.Contains("```", StringComparison.Ordinal) ||
            prompt.Contains("Exception:", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("stack trace", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
            reasons.Add("code or diagnostic context (+2)");
        }

        var matchedKeywords = CapabilityKeywords
            .Where(keyword => prompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (matchedKeywords.Length > 0)
        {
            score += matchedKeywords.Length * 2;
            reasons.Add($"capability keywords: {string.Join(", ", matchedKeywords)} (+{matchedKeywords.Length * 2})");
        }

        if (prompt.Contains("step-by-step", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("multiple constraints", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
            reasons.Add("explicit multi-step reasoning (+2)");
        }

        var path = score >= HighCapabilityThreshold
            ? ModelPath.HighCapability
            : ModelPath.LowCost;

        if (reasons.Count == 0)
        {
            reasons.Add("no high-capability indicators (+0)");
        }

        return new RoutingDecision(path, score, reasons);
    }
}
