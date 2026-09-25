using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace ModelRoutingAdvisor;

public sealed class FakeModelTransport : IModelTransport
{
    public Task<ModelResponse> SendAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = request.Path switch
        {
            ModelPath.LowCost => $"Offline low-cost response for: {request.Prompt}",
            ModelPath.HighCapability => $"Offline high-capability response for: {request.Prompt}",
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Path, "Unknown model path.")
        };

        return Task.FromResult(new ModelResponse(content, request.ModelId));
    }
}

public sealed record FoundryOptions(
    Uri ProjectEndpoint,
    string LowCostDeployment,
    string HighCapabilityDeployment)
{
    public const string EndpointVariable = "FOUNDRY_ENDPOINT";
    public const string LowCostDeploymentVariable = "FOUNDRY_LOW_COST_DEPLOYMENT";
    public const string HighCapabilityDeploymentVariable = "FOUNDRY_HIGH_CAPABILITY_DEPLOYMENT";

    public static FoundryOptions FromEnvironment()
    {
        var endpointValue = Environment.GetEnvironmentVariable(EndpointVariable);
        var lowCostDeployment = Environment.GetEnvironmentVariable(LowCostDeploymentVariable);
        var highCapabilityDeployment = Environment.GetEnvironmentVariable(HighCapabilityDeploymentVariable);

        var missing = new[]
            {
                (EndpointVariable, endpointValue),
                (LowCostDeploymentVariable, lowCostDeployment),
                (HighCapabilityDeploymentVariable, highCapabilityDeployment)
            }
            .Where(setting => string.IsNullOrWhiteSpace(setting.Item2))
            .Select(setting => setting.Item1)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new ModelTransportException(
                ModelErrorCategory.InvalidConfiguration,
                $"Real mode requires environment variables: {string.Join(", ", missing)}.");
        }

        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ModelTransportException(
                ModelErrorCategory.InvalidConfiguration,
                $"{EndpointVariable} must be an absolute HTTPS URI.");
        }

        return new FoundryOptions(NormalizeProjectEndpoint(endpoint), lowCostDeployment!, highCapabilityDeployment!);
    }

    public static Uri NormalizeProjectEndpoint(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            !endpoint.AbsolutePath.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ModelTransportException(
                ModelErrorCategory.InvalidConfiguration,
                $"{EndpointVariable} must be an absolute HTTPS Microsoft Foundry project endpoint.");
        }

        return new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    public string DeploymentFor(ModelPath path) => path switch
    {
        ModelPath.LowCost => LowCostDeployment,
        ModelPath.HighCapability => HighCapabilityDeployment,
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown model path.")
    };
}

public sealed class FoundryModelTransport : IModelTransport
{
    public const string TokenScope = "https://ai.azure.com/.default";
    private static readonly string[] TokenScopes = [TokenScope];

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly FoundryOptions _options;

    public FoundryModelTransport(HttpClient httpClient, FoundryOptions options)
        : this(
            httpClient,
            options,
            new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true
            }))
    {
    }

    internal FoundryModelTransport(
        HttpClient httpClient,
        FoundryOptions options,
        TokenCredential credential)
    {
        _httpClient = httpClient;
        _options = options;
        _credential = credential;
    }

    public async Task<ModelResponse> SendAsync(
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        AccessToken accessToken;

        try
        {
            accessToken = await _credential
                .GetTokenAsync(new TokenRequestContext(TokenScopes), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AuthenticationFailedException exception)
        {
            throw new ModelTransportException(
                ModelErrorCategory.Authentication,
                "DefaultAzureCredential could not acquire a Foundry access token.",
                innerException: exception);
        }

        var deployment = _options.DeploymentFor(request.Path);
        var endpoint = new Uri(_options.ProjectEndpoint, "openai/v1/responses");

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        message.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model = deployment,
                input = request.Prompt,
                max_output_tokens = 256
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var responseBody = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpFailure(response);
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var content = string.Concat(
                document.RootElement
                    .GetProperty("output")
                    .EnumerateArray()
                    .SelectMany(output => output.GetProperty("content").EnumerateArray())
                    .Where(item =>
                        item.TryGetProperty("type", out var type) &&
                        type.GetString() == "output_text")
                    .Select(item => item.GetProperty("text").GetString()));
            var model = document.RootElement.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new JsonException("The response content was empty.");
            }

            return new ModelResponse(content, string.IsNullOrWhiteSpace(model) ? deployment : model);
        }
        catch (JsonException exception)
        {
            throw new ModelTransportException(
                ModelErrorCategory.InvalidResponse,
                "Foundry returned a successful response with an unexpected body.",
                innerException: exception);
        }
    }

    private static ModelTransportException CreateHttpFailure(HttpResponseMessage response)
    {
        var statusCode = response.StatusCode;
        var (category, transient) = statusCode switch
        {
            HttpStatusCode.Unauthorized => (ModelErrorCategory.Authentication, false),
            HttpStatusCode.Forbidden => (ModelErrorCategory.Authorization, false),
            HttpStatusCode.RequestTimeout => (ModelErrorCategory.Timeout, true),
            HttpStatusCode.TooManyRequests => (ModelErrorCategory.RateLimited, true),
            >= HttpStatusCode.InternalServerError => (ModelErrorCategory.Service, true),
            _ => (ModelErrorCategory.Service, false)
        };

        var requestId = response.Headers.TryGetValues("x-request-id", out var values)
            ? values.FirstOrDefault()
            : response.Headers.TryGetValues("apim-request-id", out values)
                ? values.FirstOrDefault()
                : null;
        var diagnostic = string.IsNullOrWhiteSpace(requestId)
            ? "No service request ID was returned."
            : $"Service request ID: {requestId}.";
        var retryAfter = response.Headers.RetryAfter?.Delta ??
            (response.Headers.RetryAfter?.Date is { } retryDate
                ? retryDate - DateTimeOffset.UtcNow
                : statusCode == HttpStatusCode.TooManyRequests
                    ? TimeSpan.FromSeconds(30)
                    : null);

        return new ModelTransportException(
            category,
            $"Foundry returned HTTP {(int)statusCode} ({statusCode}). {diagnostic}",
            transient,
            (int)statusCode,
            retryAfter: retryAfter);
    }
}
