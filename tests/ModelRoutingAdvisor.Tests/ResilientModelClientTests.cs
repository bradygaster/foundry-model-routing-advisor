using Xunit;

namespace ModelRoutingAdvisor.Tests;

public sealed class ResilientModelClientTests
{
    private static readonly ModelRequest Request =
        new("test", ModelPath.LowCost, "fake");

    [Fact]
    public async Task RetriesTransientFailuresThenSucceeds()
    {
        var transport = new ScriptedTransport(
            _ => throw new ModelTransportException(
                ModelErrorCategory.RateLimited,
                "slow down",
                isTransient: true),
            _ => Task.FromResult(new ModelResponse("ok", "fake")));

        var result = await CreateClient(transport, maxAttempts: 3).ExecuteAsync(Request);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task PermanentFailureIsNotRetried()
    {
        var transport = new ScriptedTransport(
            _ => throw new ModelTransportException(
                ModelErrorCategory.Authorization,
                "forbidden"));

        var result = await CreateClient(transport, maxAttempts: 3).ExecuteAsync(Request);

        Assert.False(result.Succeeded);
        Assert.Equal(ModelErrorCategory.Authorization, result.ErrorCategory);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task TimeoutIsCategorizedAndBounded()
    {
        var transport = new ScriptedTransport(async cancellationToken =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new ModelResponse("late", "fake");
        });

        var client = new ResilientModelClient(
            transport,
            new ResilienceOptions(
                MaxAttempts: 2,
                AttemptTimeout: TimeSpan.FromMilliseconds(25),
                InitialRetryDelay: TimeSpan.Zero,
                MaxRetryDelay: TimeSpan.Zero));

        var result = await client.ExecuteAsync(Request);

        Assert.False(result.Succeeded);
        Assert.Equal(ModelErrorCategory.Timeout, result.ErrorCategory);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task TransientFailuresStopAtMaximumAttempts()
    {
        var transport = new ScriptedTransport(
            _ => throw new HttpRequestException("offline"));

        var result = await CreateClient(transport, maxAttempts: 3).ExecuteAsync(Request);

        Assert.False(result.Succeeded);
        Assert.Equal(ModelErrorCategory.Transport, result.ErrorCategory);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, transport.CallCount);
    }

    private static ResilientModelClient CreateClient(
        IModelTransport transport,
        int maxAttempts) =>
        new(
            transport,
            new ResilienceOptions(
                maxAttempts,
                TimeSpan.FromSeconds(1),
                TimeSpan.Zero,
                TimeSpan.Zero));

    private sealed class ScriptedTransport(
        params Func<CancellationToken, Task<ModelResponse>>[] steps) : IModelTransport
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<ModelResponse> SendAsync(
            ModelRequest request,
            CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _callCount) - 1;
            var step = steps[Math.Min(index, steps.Length - 1)];
            return step(cancellationToken);
        }
    }
}
