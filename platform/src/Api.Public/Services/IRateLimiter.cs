namespace Api.Public.Services;

public class RateLimitExceededException(int retryAfterSeconds) : Exception("Rate limit exceeded")
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}

public interface IRateLimiter
{
    /// <summary>
    /// Checks the sliding-window rate limit for the given key.
    /// Throws <see cref="RateLimitExceededException"/> if the limit is exceeded.
    /// </summary>
    Task CheckAsync(Guid apiKeyId, int maxPerMinute, CancellationToken ct = default);
}
