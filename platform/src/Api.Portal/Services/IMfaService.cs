namespace Api.Portal.Services;

public interface IMfaService
{
    string GenerateSecret();
    string GetTotpUri(string secret, string email, string issuer = "SupportLayer");
    string[] GenerateBackupCodes(int count = 10);
    bool VerifyTotp(string secret, string code);
}
