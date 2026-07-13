using System.ComponentModel.DataAnnotations;

namespace Appifylab.Dtos.Auth;

public record RegisterRequest(
    [property: Required, MaxLength(50)] string FirstName,
    [property: Required, MaxLength(50)] string LastName,
    [property: Required, EmailAddress, MaxLength(255)] string Email,
    [property: Required, MinLength(8), MaxLength(100)] string Password
);

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password
);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email
);