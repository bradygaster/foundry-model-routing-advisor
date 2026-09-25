namespace ModelRoutingAdvisor;

public interface IModelTransport
{
    Task<ModelResponse> SendAsync(ModelRequest request, CancellationToken cancellationToken);
}

public sealed record ResilienceOptions(
    int MaxAttempts,
    TimeSpan AttemptTimeout,
    TimeSpan InitialRetryDelay,
    TimeSpan MaxRetryDelay)
{
    public static ResilienceOptions Default { get; } = new(
        MaxAttempts: 3,
        AttemptTimeout: TimeSpan.FromSeconds(30),
        InitialRetryDelay: TimeSpan.FromMilliseconds(200),
        MaxRetryDelay: TimeSpan.FromSeconds(2));

    public void Validate()
    {
        if (MaxAttempts is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "MaxAttempts must be between 1 and 5.");
        }

        if (AttemptTimeout <= TimeSpan.Zero || AttemptTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(AttemptTimeout),
                "AttemptTimeout must be greater than zero and no more than two minutes.");
        }

        if (InitialRetryDelay < TimeSpan.Zero || MaxRetryDelay < InitialRetryDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InitialRetryDelay),
                "Retry delays must be non-negative and the maximum must not be smaller than the initial delay.");
        }
    }
}

public sealed class ResilientModelClient
{
    private readonly IModelTransport _transport;
    private readonly ResilienceOptions _options;

    public ResilientModelClient(IModelTransport transport, ResilienceOptions options)
    {
        _transport = transport;
        options.Validate();
        _options = options;
    }

    public async Task<ModelExecutionResult> ExecuteAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptTimeout.CancelAfter(_options.AttemptTimeout);

            try
            {
                var response = await _transport
                    .SendAsync(request, attemptTimeout.Token)
                    .ConfigureAwait(false);

                return ModelExecutionResult.Success(response, attempt);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return ModelExecutionResult.Failure(
                    ModelErrorCategory.Cancelled,
                    "The operation was cancelled by the caller.",
                    attempt);
            }
            catch (OperationCanceledException) when (attemptTimeout.IsCancellationRequested)
            {
                if (attempt == _options.MaxAttempts)
                {
                    return ModelExecutionResult.Failure(
                        ModelErrorCategory.Timeout,
                        $"The model call exceeded the {_options.AttemptTimeout.TotalSeconds:0.###}-second attempt timeout.",
                        attempt);
                }
            }
            catch (ModelTransportException exception)
            {
                if (!exception.IsTransient || attempt == _options.MaxAttempts)
                {
                    return ModelExecutionResult.Failure(exception.Category, exception.Message, attempt);
                }
            }
            catch (HttpRequestException exception)
            {
                if (attempt == _options.MaxAttempts)
                {
                    return ModelExecutionResult.Failure(
                        ModelErrorCategory.Transport,
                        $"The model endpoint could not be reached: {exception.Message}",
                        attempt);
                }
            }

            await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("The bounded retry loop completed without a result.");
    }

    private Task DelayBeforeRetryAsync(int failedAttempt, CancellationToken cancellationToken)
    {
        var multiplier = Math.Pow(2, failedAttempt - 1);
        var delayMilliseconds = Math.Min(
            _options.InitialRetryDelay.TotalMilliseconds * multiplier,
            _options.MaxRetryDelay.TotalMilliseconds);

        return Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
    }
}
