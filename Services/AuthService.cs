using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Appifylab.Data;
using Appifylab.Data.Models;
using Appifylab.Dtos.Auth;

namespace Appifylab.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthService(AppDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<(AuthResponse, string, DateTime)> RegisterAsync(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (exists)
            throw new AuthenticationFailedException("An account with this email already exists.");

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await IssueTokensAsync(user);
    }

    public async Task<(AuthResponse, string, DateTime)> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail);

        // Deliberately generic message — don't reveal whether the email exists (user enumeration).
        if (user is null)
            throw new AuthenticationFailedException("Invalid email or password.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new AuthenticationFailedException("Invalid email or password.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync();
        }

        return await IssueTokensAsync(user);
    }

    public async Task<(AuthResponse, string, DateTime)> RefreshAsync(string rawRefreshToken)
    {
        var tokenHash = _jwt.HashToken(rawRefreshToken);
        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (stored is null)
            throw new AuthenticationFailedException("Invalid refresh token.");

        // Reuse of an already-revoked token = likely theft. Nuke every session for this user.
        if (stored.RevokedAt is not null)
        {
            var allTokens = await _db.RefreshTokens
                .Where(rt => rt.UserId == stored.UserId && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var t in allTokens)
                t.RevokedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            throw new AuthenticationFailedException("Session invalidated. Please log in again.");
        }

        if (!stored.IsActive)
            throw new AuthenticationFailedException("Refresh token expired.");

        // Rotate: revoke the old one, mint a new one, link them.
        var (newRaw, newHash, newExpiry) = _jwt.GenerateRefreshToken();
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByTokenHash = newHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = newHash,
            ExpiresAt = newExpiry
        });
        await _db.SaveChangesAsync();

        var (accessToken, accessExpiresAt) = _jwt.GenerateAccessToken(stored.User);
        var response = new AuthResponse(
            accessToken, accessExpiresAt,
            stored.User.Id, stored.User.FirstName, stored.User.LastName, stored.User.Email
        );

        return (response, newRaw, newExpiry);
    }

    public async Task RevokeAsync(string rawRefreshToken)
    {
        var tokenHash = _jwt.HashToken(rawRefreshToken);
        var stored = await _db.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        // If not found, logout is still a no-op success — no need to leak info here either.
    }

    private async Task<(AuthResponse, string, DateTime)> IssueTokensAsync(User user)
    {
        var (accessToken, accessExpiresAt) = _jwt.GenerateAccessToken(user);
        var (rawRefresh, refreshHash, refreshExpiresAt) = _jwt.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = refreshExpiresAt
        });
        await _db.SaveChangesAsync();

        var response = new AuthResponse(
            accessToken, accessExpiresAt,
            user.Id, user.FirstName, user.LastName, user.Email
        );

        return (response, rawRefresh, refreshExpiresAt);
    }
}