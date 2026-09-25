using System.Net;
using System.Text;
using Azure.Core;
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

    [Fact]
    public void ProjectEndpointIsNormalizedWithTrailingSlash()
    {
        var endpoint = FoundryOptions.NormalizeProjectEndpoint(
            new Uri("https://example.services.ai.azure.com/api/projects/example-project"));

        Assert.Equal(
            "https://example.services.ai.azure.com/api/projects/example-project/",
            endpoint.AbsoluteUri);
    }

    [Fact]
    public async Task TransportUsesProjectResponsesRouteAndReportsRespondingModel()
    {
        var handler = new RecordingHandler();
        var options = new FoundryOptions(
            new Uri("https://example.services.ai.azure.com/api/projects/example-project/"),
            "gpt-5-mini",
            "model-router-advisor");
        var transport = new FoundryModelTransport(
            new HttpClient(handler),
            options,
            new StaticTokenCredential());

        var response = await transport.SendAsync(
            new ModelRequest("test", ModelPath.HighCapability, "model-router-advisor"),
            CancellationToken.None);

        Assert.Equal(
            "https://example.services.ai.azure.com/api/projects/example-project/openai/v1/responses",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Contains("\"model\":\"model-router-advisor\"", handler.RequestBody);
        Assert.Contains("\"max_output_tokens\":256", handler.RequestBody);
        Assert.Equal("gpt-5.4-mini-2026-03-17", response.ModelId);
    }

    private sealed class StaticTokenCredential : TokenCredential
    {
        private static readonly AccessToken Token = new("test-token", DateTimeOffset.MaxValue);

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => Token;

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => ValueTask.FromResult(Token);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "model": "gpt-5.4-mini-2026-03-17",
                      "output": [
                        {
                          "content": [
                            {
                              "type": "output_text",
                              "text": "route-ok"
                            }
                          ]
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
