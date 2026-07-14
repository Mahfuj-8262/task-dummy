using System.Security.Claims;
using Appifylab.Common;
using Appifylab.Data.Models;
using Appifylab.Dtos.Posts;
using Appifylab.Services;

namespace Appifylab.Endpoints;

public static class PostEndpoints
{
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public static void MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        var posts = app.MapGroup("/api/posts").RequireAuthorization().WithTags("Posts");

        posts.MapPost("/", async (HttpRequest request, IPostService svc, ClaimsPrincipal user, IImageStorageService imageStorage) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "Expected multipart/form-data." });

            var form = await request.ReadFormAsync();
            var content = form["content"].ToString();

            if (string.IsNullOrWhiteSpace(content))
                return Results.BadRequest(new { message = "Content is required." });

            if (!Enum.TryParse<PostVisibility>(form["visibility"].ToString(), ignoreCase: true, out var visibility))
                visibility = PostVisibility.Public;

            string? imageUrl = null;
            var file = form.Files["image"];
            if (file is not null)
            {
                if (file.Length > MaxImageBytes)
                    return Results.BadRequest(new { message = "Image exceeds 5MB limit." });

                if (!AllowedImageTypes.Contains(file.ContentType))
                    return Results.BadRequest(new { message = "Unsupported image type." });

                await using var stream = file.OpenReadStream();
                var extension = Path.GetExtension(file.FileName);
                imageUrl = await imageStorage.UploadAsync(stream, file.ContentType, extension, request.HttpContext.RequestAborted);
            }

            var result = await svc.CreatePostAsync(user.GetUserId(), content, visibility, imageUrl);
            return Results.Created($"/api/posts/{result.Id}", result);
        });

        posts.MapGet("/", async (IPostService svc, ClaimsPrincipal user, string? cursor, int pageSize = 20) =>
        {
            var feed = await svc.GetFeedAsync(user.GetUserId(), cursor, pageSize);
            return Results.Ok(feed);
        });

        posts.MapPost("/{postId:guid}/like", async (Guid postId, IPostService svc, ClaimsPrincipal user) =>
        {
            try
            {
                var liked = await svc.ToggleLikeAsync(user.GetUserId(), postId);
                return Results.Ok(new { liked });
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        });

        posts.MapGet("/{postId:guid}/comments", async (Guid postId, IPostService svc, ClaimsPrincipal user) =>
        {
            try
            {
                return Results.Ok(await svc.GetCommentsAsync(user.GetUserId(), postId));
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        });

        posts.MapPost("/{postId:guid}/comments", async (Guid postId, CommentRequest request, IPostService svc, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return Results.BadRequest(new { message = "Content is required." });

            try
            {
                var comment = await svc.AddCommentAsync(user.GetUserId(), postId, request.Content, request.ParentCommentId);
                return Results.Created($"/api/posts/{postId}/comments/{comment.Id}", comment);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        });

        var comments = app.MapGroup("/api/comments").RequireAuthorization().WithTags("Comments"); // CHANGED: captured as a variable

        comments.MapPost("/{commentId:guid}/like", async (Guid commentId, IPostService svc, ClaimsPrincipal user) =>
        {
            try
            {
                var liked = await svc.ToggleCommentLikeAsync(user.GetUserId(), commentId);
                return Results.Ok(new { liked });
            }
            catch (NotFoundException) { return Results.NotFound(); }
        });

        comments.MapDelete("/{commentId:guid}", async (Guid commentId, IPostService svc, ClaimsPrincipal user) => // ADDED
        {
            try
            {
                await svc.DeleteCommentAsync(user.GetUserId(), commentId);
                return Results.NoContent();
            }
            catch (NotFoundException) { return Results.NotFound(); }
            catch (ForbiddenException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        });
        
        posts.MapDelete("/{postId:guid}", async (Guid postId, IPostService svc, ClaimsPrincipal user) =>
        {
            try
            {
                await svc.DeletePostAsync(user.GetUserId(), postId);
                return Results.NoContent();
            }
            catch (NotFoundException) { return Results.NotFound(); }
            catch (ForbiddenException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        });
    }
}