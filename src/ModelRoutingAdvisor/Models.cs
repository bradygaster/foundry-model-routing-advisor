namespace ModelRoutingAdvisor;

public sealed record ModelRequest(string Prompt, ModelPath Path, string ModelId);

public sealed record ModelResponse(string Content, string ModelId);

public enum ModelErrorCategory
{
    InvalidConfiguration,
    Authentication,
    Authorization,
    RateLimited,
    Timeout,
    Transport,
    Service,
    InvalidResponse,
    Cancelled
}

public sealed class ModelTransportException : Exception
{
    public ModelTransportException(
        ModelErrorCategory category,
        string message,
        bool isTransient = false,
        int? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        IsTransient = isTransient;
        StatusCode = statusCode;
    }

    public ModelErrorCategory Category { get; }

    public bool IsTransient { get; }

    public int? StatusCode { get; }
}

public sealed record ModelExecutionResult(
    bool Succeeded,
    ModelResponse? Response,
    ModelErrorCategory? ErrorCategory,
    string? ErrorMessage,
    int Attempts)
{
    public static ModelExecutionResult Success(ModelResponse response, int attempts) =>
        new(true, response, null, null, attempts);

    public static ModelExecutionResult Failure(
        ModelErrorCategory category,
        string message,
        int attempts) =>
        new(false, null, category, message, attempts);
}
