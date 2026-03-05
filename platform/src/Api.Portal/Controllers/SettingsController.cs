using Api.Portal.Models.Requests;
using Api.Portal.Models.Responses;
using Core.Auth;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/settings")]
[Authorize]
public class SettingsController(AppDbContext db, TenantContext tenantContext) : ControllerBase
{
    private static readonly string[] DefaultEventTypes =
        ["ingestion.complete", "ingestion.error", "quota.80", "quota.100"];

    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationPreferencesResponse>> GetNotifications()
    {
        var existing = await db.NotificationPreferences
            .Where(n => n.TenantId == tenantContext.TenantId!.Value)
            .ToListAsync();

        var result = DefaultEventTypes.Select(et =>
        {
            var pref = existing.FirstOrDefault(n => n.EventType == et);
            return new NotificationPreferenceItem(et,
                pref?.EmailEnabled ?? true,
                pref?.InAppEnabled ?? true);
        }).ToList();

        return Ok(new NotificationPreferencesResponse(result));
    }

    [HttpPut("notifications")]
    public async Task<ActionResult<NotificationPreferencesResponse>> UpdateNotifications(
        [FromBody] UpdateNotificationPreferencesRequest request)
    {
        foreach (var toggle in request.Preferences)
        {
            var pref = await db.NotificationPreferences
                .FirstOrDefaultAsync(n => n.TenantId == tenantContext.TenantId!.Value && n.EventType == toggle.EventType);

            if (pref is null)
            {
                db.NotificationPreferences.Add(new NotificationPreference
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantContext.TenantId!.Value,
                    EventType = toggle.EventType,
                    EmailEnabled = toggle.EmailEnabled,
                    InAppEnabled = toggle.InAppEnabled,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                pref.EmailEnabled = toggle.EmailEnabled;
                pref.InAppEnabled = toggle.InAppEnabled;
                pref.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return await GetNotifications();
    }
}
