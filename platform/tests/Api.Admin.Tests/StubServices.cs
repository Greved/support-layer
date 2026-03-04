using Api.Admin.Models.Responses;
using Api.Admin.Services;

namespace Api.Admin.Tests;

public class StubInfraHealthService : IInfraHealthService
{
    public Task<InfraHealthResponse> CheckAllAsync(CancellationToken ct = default)
    {
        var services = new List<ServiceHealth>
        {
            new("rag-core", "healthy", LatencyMs: 5),
            new("qdrant", "healthy", LatencyMs: 3),
            new("postgres", "healthy", LatencyMs: 1),
        };
        return Task.FromResult(new InfraHealthResponse("healthy", services));
    }
}

public class StubQdrantAdminService : IQdrantAdminService
{
    public Task<IReadOnlyList<CollectionInfoResponse>> ListCollectionsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<CollectionInfoResponse> result =
        [
            new CollectionInfoResponse("tenant_acme", "acme", 1000, 500),
        ];
        return Task.FromResult(result);
    }

    public Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
        => Task.CompletedTask;
}
