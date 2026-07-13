namespace Appifylab.Services;

using Appifylab.Data.Models;

public interface IJwtTokenService
{
    (string token, DateTime expiresAt) GenerateAccessToken(User user);
    (string rawToken, string tokenHash, DateTime expiresAt) GenerateRefreshToken();
    string HashToken(string rawToken);
}