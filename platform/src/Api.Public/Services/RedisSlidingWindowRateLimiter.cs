using StackExchange.Redis;

namespace Api.Public.Services;

public class RedisSlidingWindowRateLimiter(IConnectionMultiplexer redis) : IRateLimiter
{
    private const int WindowSeconds = 60;
    private const int ExpirySeconds = 70;

    public async Task CheckAsync(Guid apiKeyId, int maxPerMinute, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = $"ratelimit:{apiKeyId}";
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = nowMs - WindowSeconds * 1000;

        var tran = db.CreateTransaction();
        _ = tran.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);
        _ = tran.SortedSetAddAsync(key, Guid.NewGuid().ToString(), nowMs);
        var countTask = tran.SortedSetLengthAsync(key);
        _ = tran.KeyExpireAsync(key, TimeSpan.FromSeconds(ExpirySeconds));

        await tran.ExecuteAsync();
        var count = await countTask;

        if (count > maxPerMinute)
            throw new RateLimitExceededException(ExpirySeconds);
    }
}
