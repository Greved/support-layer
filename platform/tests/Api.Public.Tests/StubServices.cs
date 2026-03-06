using Api.Public.Services;
using StackExchange.Redis;

namespace Api.Public.Tests;

public class AlwaysAllowRateLimiter : IRateLimiter
{
    public Task CheckAsync(Guid apiKeyId, int maxPerMinute, CancellationToken ct = default)
        => Task.CompletedTask; // never throws - always allows
}

public class UnavailableRedisRateLimiter : IRateLimiter
{
    public Task CheckAsync(Guid apiKeyId, int maxPerMinute, CancellationToken ct = default)
        => throw new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis is down");
}

public class StubPublicRagClient : IPublicRagClient
{
    public Task<PublicRagResult> QueryAsync(string tenantSlug, string query, CancellationToken ct = default)
        => Task.FromResult(new PublicRagResult("Stub answer.", []));

    public async IAsyncEnumerable<string> StreamQueryAsync(
        string tenantSlug,
        string query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

public class FailingPublicRagClient : IPublicRagClient
{
    public Task<PublicRagResult> QueryAsync(string tenantSlug, string query, CancellationToken ct = default)
        => throw new HttpRequestException("upstream_rag_unavailable");

    public IAsyncEnumerable<string> StreamQueryAsync(
        string tenantSlug,
        string query,
        CancellationToken ct = default)
        => throw new HttpRequestException("upstream_rag_unavailable");
}
