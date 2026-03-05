using System.Security.Claims;
using Core.Entities;

namespace Api.Portal.Services;

public interface ITokenService
{
    string IssueAccessToken(User user);
    string IssueTempToken(User user);
    ClaimsPrincipal? ValidateTempToken(string token);
    Task<string> IssueRefreshTokenAsync(Guid userId);
    Task<(string accessToken, string refreshToken)> RefreshAsync(string token);
    Task RevokeAsync(string token);
}
