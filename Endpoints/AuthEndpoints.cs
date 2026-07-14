using Microsoft.AspNetCore.Mvc;
using Appifylab.Dtos.Auth;
using Appifylab.Services;

namespace Appifylab.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IAuthService authService,
            HttpContext ctx,
            IWebHostEnvironment env) =>
        {
            try
            {
                var (response, rawRefresh, refreshExpiresAt) = await authService.RegisterAsync(request);
                SetRefreshCookie(ctx, env, rawRefresh, refreshExpiresAt);
                return Results.Ok(response);
            }
            catch (AuthenticationFailedException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        });

        group.MapPost("/login", async (
            LoginRequest request,
            IAuthService authService,
            HttpContext ctx,
            IWebHostEnvironment env) =>
        {
            try
            {
                var (response, rawRefresh, refreshExpiresAt) = await authService.LoginAsync(request);
                SetRefreshCookie(ctx, env, rawRefresh, refreshExpiresAt);
                return Results.Ok(response);
            }
            catch (AuthenticationFailedException)
            {
                return Results.Unauthorized();
            }
        });

        group.MapPost("/refresh", async (
            IAuthService authService,
            HttpContext ctx,
            IWebHostEnvironment env) =>
        {
            if (!ctx.Request.Cookies.TryGetValue("refreshToken", out var rawRefresh) || string.IsNullOrEmpty(rawRefresh))
                return Results.Unauthorized();

            try
            {
                var (response, newRawRefresh, refreshExpiresAt) = await authService.RefreshAsync(rawRefresh);
                SetRefreshCookie(ctx, env, newRawRefresh, refreshExpiresAt);
                return Results.Ok(response);
            }
            catch (AuthenticationFailedException)
            {
                ctx.Response.Cookies.Delete("refreshToken");
                return Results.Unauthorized();
            }
        });

        group.MapPost("/logout", async (
            IAuthService authService,
            HttpContext ctx) =>
        {
            if (ctx.Request.Cookies.TryGetValue("refreshToken", out var rawRefresh) && !string.IsNullOrEmpty(rawRefresh))
            {
                await authService.RevokeAsync(rawRefresh);
            }
            ctx.Response.Cookies.Delete("refreshToken");
            return Results.NoContent();
        });

        group.MapGet("/me", (HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirst("sub")?.Value;
            return Results.Ok(new { userId });
        }).RequireAuthorization();
    }

    private static void SetRefreshCookie(HttpContext ctx, IWebHostEnvironment env, string rawToken, DateTime expiresAt)
    {
        ctx.Response.Cookies.Append("refreshToken", rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/api/auth"
        });
    }
}