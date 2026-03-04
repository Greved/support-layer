using Api.Admin.Models.Responses;

namespace Api.Admin.Services;

public interface IQdrantAdminService
{
    Task<IReadOnlyList<CollectionInfoResponse>> ListCollectionsAsync(CancellationToken ct = default);
    Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default);
}
