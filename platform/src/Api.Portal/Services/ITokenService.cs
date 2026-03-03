using Core.Entities;

namespace Api.Portal.Services;

public interface ITokenService
{
    string IssueAccessToken(User user);
    Task<string> IssueRefreshTokenAsync(Guid userId);
    Task<(string accessToken, string refreshToken)> RefreshAsync(string token);
    Task RevokeAsync(string token);
}
