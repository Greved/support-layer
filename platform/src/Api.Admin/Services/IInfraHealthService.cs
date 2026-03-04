using Api.Admin.Models.Responses;

namespace Api.Admin.Services;

public interface IInfraHealthService
{
    Task<InfraHealthResponse> CheckAllAsync(CancellationToken ct = default);
}
