namespace Api.Portal.Services;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string resetLink);
    Task SendInviteAsync(string toEmail, string inviteLink, string tenantName);
}
