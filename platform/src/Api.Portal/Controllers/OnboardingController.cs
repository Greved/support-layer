using Api.Portal.Models.Responses;
using Core.Auth;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/onboarding")]
[Authorize]
public class OnboardingController(AppDbContext db, TenantContext tenantContext) : ControllerBase
{
    private const int TotalSteps = 4;

    [HttpGet]
    public async Task<ActionResult<OnboardingResponse>> GetState()
    {
        var completed = await db.OnboardingSteps
            .Where(o => o.TenantId == tenantContext.TenantId!.Value)
            .Select(o => o.Step)
            .ToListAsync();

        return Ok(new OnboardingResponse(completed, completed.Count >= TotalSteps));
    }

    [HttpPost("complete/{step:int}")]
    public async Task<ActionResult<OnboardingResponse>> CompleteStep(int step)
    {
        if (step < 1 || step > TotalSteps)
            return BadRequest(new { error = $"Step must be between 1 and {TotalSteps}." });

        var exists = await db.OnboardingSteps
            .AnyAsync(o => o.TenantId == tenantContext.TenantId!.Value && o.Step == step);

        if (!exists)
        {
            db.OnboardingSteps.Add(new OnboardingStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId!.Value,
                Step = step,
                CompletedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        return await GetState();
    }
}
