using Appifylab.Data.Models;

namespace Appifylab.Dtos.Posts;

public record AuthorDto(Guid UserId, string FirstName, string LastName);

public record LikerDto(Guid UserId, string FirstName, string LastName);

public record PostResponse(
    Guid Id,
    string Content,
    string? ImageUrl,
    PostVisibility Visibility,
    DateTime CreatedAt,
    AuthorDto Author,
    int LikeCount,
    bool IsLikedByCurrentUser,
    int CommentCount,
    List<LikerDto> LikersPreview
);

public record FeedResponse(List<PostResponse> Posts, string? NextCursor);

public record CommentRequest(string Content, Guid? ParentCommentId);

public record CommentResponse(
    Guid Id,
    string Content,
    DateTime CreatedAt,
    AuthorDto Author,
    int LikeCount,
    bool IsLikedByCurrentUser,
    List<LikerDto> LikersPreview,
    List<CommentResponse> Replies
);