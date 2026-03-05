using OtpNet;

namespace Api.Portal.Services;

public class MfaService : IMfaService
{
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GetTotpUri(string secret, string email, string issuer = "SupportLayer")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public string[] GenerateBackupCodes(int count = 10)
    {
        var codes = new string[count];
        for (int i = 0; i < count; i++)
        {
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(5);
            codes[i] = Convert.ToHexString(bytes).ToLower();
        }
        return codes;
    }

    public bool VerifyTotp(string secret, string code)
    {
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
