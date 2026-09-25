using Xunit;

namespace ModelRoutingAdvisor.Tests;

public sealed class FoundryConfigurationTests
{
    [Fact]
    public void ProjectEndpointUsesFoundryTokenAudience()
    {
        Assert.Equal(
            "https://ai.azure.com/.default",
            FoundryModelTransport.TokenScope);
    }
}
