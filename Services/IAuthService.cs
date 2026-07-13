namespace Appifylab.Services;

using Appifylab.Dtos.Auth;

public interface IAuthService
{
    Task<(AuthResponse response, string rawRefreshToken, DateTime refreshExpiresAt)> RegisterAsync(RegisterRequest request);
    Task<(AuthResponse response, string rawRefreshToken, DateTime refreshExpiresAt)> LoginAsync(LoginRequest request);
    Task<(AuthResponse response, string rawRefreshToken, DateTime refreshExpiresAt)> RefreshAsync(string rawRefreshToken);
    Task RevokeAsync(string rawRefreshToken);
}

public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message) : base(message) { }
}