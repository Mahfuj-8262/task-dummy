using Appifylab.Data.Models;
using Appifylab.Dtos.Posts;

namespace Appifylab.Services;

public interface IPostService
{
    Task<PostResponse> CreatePostAsync(Guid userId, string content, PostVisibility visibility, string? imageUrl);
    Task<FeedResponse> GetFeedAsync(Guid currentUserId, string? cursor, int pageSize);
    Task<bool> ToggleLikeAsync(Guid userId, Guid postId);
    Task<List<CommentResponse>> GetCommentsAsync(Guid currentUserId, Guid postId);
    Task<CommentResponse> AddCommentAsync(Guid userId, Guid postId, string content, Guid? parentCommentId);
    Task<bool> ToggleCommentLikeAsync(Guid userId, Guid commentId);
    Task DeletePostAsync(Guid userId, Guid postId);
    Task DeleteCommentAsync(Guid userId, Guid commentId);
}