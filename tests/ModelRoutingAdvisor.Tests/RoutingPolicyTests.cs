using Xunit;

namespace ModelRoutingAdvisor.Tests;

public sealed class RoutingPolicyTests
{
    private readonly DeterministicRoutingPolicy _policy = new();

    [Fact]
    public void ShortSimplePromptUsesLowCostPath()
    {
        var decision = _policy.Decide("Summarize this paragraph.");

        Assert.Equal(ModelPath.LowCost, decision.Path);
        Assert.Equal(0, decision.Score);
    }

    [Fact]
    public void ArchitecturePromptUsesHighCapabilityPath()
    {
        var decision = _policy.Decide(
            "Compare the architecture and security trade-offs, step-by-step.");

        Assert.Equal(ModelPath.HighCapability, decision.Path);
        Assert.True(decision.Score >= 4);
        Assert.Contains(decision.Reasons, reason => reason.Contains("capability keywords"));
    }

    [Fact]
    public void IdenticalPromptsProduceIdenticalDecisions()
    {
        const string prompt = "Diagnose this stack trace:\n```text\nException: failed\n```";

        var first = _policy.Decide(prompt);
        var second = _policy.Decide(prompt);

        Assert.Equal(first.Path, second.Path);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Reasons.ToArray(), second.Reasons.ToArray());
    }

    [Fact]
    public void EmptyPromptIsRejected()
    {
        Assert.Throws<ArgumentException>(() => _policy.Decide(" "));
    }
}
