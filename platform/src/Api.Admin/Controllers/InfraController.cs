using Api.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/infra")]
[Authorize]
public class InfraController(IInfraHealthService healthService, IQdrantAdminService qdrant) : ControllerBase
{
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var result = await healthService.CheckAllAsync(ct);
        var statusCode = result.Overall == "healthy" ? 200 : 207;
        return StatusCode(statusCode, result);
    }

    [HttpGet("collections")]
    public async Task<IActionResult> Collections(CancellationToken ct)
    {
        var collections = await qdrant.ListCollectionsAsync(ct);
        return Ok(collections);
    }
}
