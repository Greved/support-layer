using Microsoft.Extensions.Logging;

namespace Api.Portal.Services;

/// <summary>
/// No-op email service — logs instead of sending. Replace with SMTP/SendGrid in production.
/// </summary>
public class NoOpEmailService(ILogger<NoOpEmailService> logger) : IEmailService
{
    public Task SendPasswordResetAsync(string toEmail, string resetLink)
    {
        logger.LogInformation("[EMAIL] Password reset for {Email}: {Link}", toEmail, resetLink);
        return Task.CompletedTask;
    }

    public Task SendInviteAsync(string toEmail, string inviteLink, string tenantName)
    {
        logger.LogInformation("[EMAIL] Invite for {Email} to {Tenant}: {Link}", toEmail, tenantName, inviteLink);
        return Task.CompletedTask;
    }
}
