using Microsoft.EntityFrameworkCore;
using Appifylab.Common;
using Appifylab.Data;
using Appifylab.Data.Models;
using Appifylab.Dtos.Posts;

namespace Appifylab.Services;

public class PostService : IPostService
{
    private readonly AppDbContext _db;

    public PostService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PostResponse> CreatePostAsync(Guid userId, string content, PostVisibility visibility, string? imageUrl)
    {
        var post = new Post
        {
            UserId = userId,
            Content = content,
            Visibility = visibility,
            ImageUrl = imageUrl
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FirstAsync(u => u.Id == userId);

        return new PostResponse(
            post.Id, post.Content, post.ImageUrl, post.Visibility, post.CreatedAt,
            new AuthorDto(user.Id, user.FirstName, user.LastName),
            LikeCount: 0, IsLikedByCurrentUser: false, CommentCount: 0, LikersPreview: new List<LikerDto>()
        );
    }

    public async Task<FeedResponse> GetFeedAsync(Guid currentUserId, string? cursor, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var decoded = CursorHelper.Decode(cursor);

        // Visibility filter: everyone's public posts + only MY private posts. This is the core guard for the feed itself.
        var query = _db.Posts
            .Where(p => p.Visibility == PostVisibility.Public || p.UserId == currentUserId);

        if (decoded is not null)
        {
            query = query.Where(p =>
                p.CreatedAt < decoded.CreatedAt ||
                (p.CreatedAt == decoded.CreatedAt && p.Id < decoded.Id));
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(pageSize + 1) // fetch one extra to know if there's a next page
            .Select(p => new
            {
                p.Id, p.Content, p.ImageUrl, p.Visibility, p.CreatedAt,
                Author = new AuthorDto(p.User.Id, p.User.FirstName, p.User.LastName),
                LikeCount = p.Likes.Count,
                IsLiked = p.Likes.Any(l => l.UserId == currentUserId),
                CommentCount = p.Comments.Count,
                LikersPreview = p.Likes
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(5)
                    .Select(l => new LikerDto(l.User.Id, l.User.FirstName, l.User.LastName))
                    .ToList()
            })
            .ToListAsync();

        var hasMore = rows.Count > pageSize;
        var page = rows.Take(pageSize).ToList();

        var results = page.Select(r => new PostResponse(
            r.Id, r.Content, r.ImageUrl, r.Visibility, r.CreatedAt,
            r.Author, r.LikeCount, r.IsLiked, r.CommentCount, r.LikersPreview
        )).ToList();

        string? nextCursor = hasMore
            ? CursorHelper.Encode(page[^1].CreatedAt, page[^1].Id)
            : null;

        return new FeedResponse(results, nextCursor);
    }

    public async Task<bool> ToggleLikeAsync(Guid userId, Guid postId)
    {
        await EnsurePostAccessibleAsync(postId, userId);

        var existing = await _db.Likes.SingleOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);
        if (existing is not null)
        {
            _db.Likes.Remove(existing);
            await _db.SaveChangesAsync();
            return false; // now unliked
        }

        _db.Likes.Add(new Like { PostId = postId, UserId = userId });
        await _db.SaveChangesAsync();
        return true; // now liked
    }

    public async Task<List<CommentResponse>> GetCommentsAsync(Guid currentUserId, Guid postId)
    {
        await EnsurePostAccessibleAsync(postId, currentUserId);

        var comments = await _db.Comments
            .Where(c => c.PostId == postId)
            .Include(c => c.User)
            .Include(c => c.CommentLikes).ThenInclude(cl => cl.User)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var byParent = comments.ToLookup(c => c.ParentCommentId);

        List<CommentResponse> Build(Guid? parentId)
        {
            return byParent[parentId] // CHANGED: no TryGetValue needed — Lookup returns an empty sequence for missing keys instead of throwing
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CommentResponse(
                    c.Id, c.Content, c.CreatedAt,
                    new AuthorDto(c.User.Id, c.User.FirstName, c.User.LastName),
                    c.CommentLikes.Count,
                    c.CommentLikes.Any(cl => cl.UserId == currentUserId),
                    c.CommentLikes
                        .OrderByDescending(cl => cl.CreatedAt)
                        .Take(5)
                        .Select(cl => new LikerDto(cl.User.Id, cl.User.FirstName, cl.User.LastName))
                        .ToList(),
                    Build(c.Id) // recurse into replies
                ))
                .ToList();
        }

        return Build(null);
    }

    public async Task<CommentResponse> AddCommentAsync(Guid userId, Guid postId, string content, Guid? parentCommentId)
    {
        await EnsurePostAccessibleAsync(postId, userId);

        if (parentCommentId is not null)
        {
            var parentExists = await _db.Comments.AnyAsync(c => c.Id == parentCommentId && c.PostId == postId);
            if (!parentExists)
                throw new NotFoundException("Parent comment not found.");
        }

        var comment = new Comment
        {
            PostId = postId,
            UserId = userId,
            Content = content,
            ParentCommentId = parentCommentId
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FirstAsync(u => u.Id == userId);

        return new CommentResponse(
            comment.Id, comment.Content, comment.CreatedAt,
            new AuthorDto(user.Id, user.FirstName, user.LastName),
            LikeCount: 0, IsLikedByCurrentUser: false,
            LikersPreview: new List<LikerDto>(), Replies: new List<CommentResponse>()
        );
    }

    public async Task<bool> ToggleCommentLikeAsync(Guid userId, Guid commentId)
    {
        var comment = await _db.Comments
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment is null)
            throw new NotFoundException("Comment not found.");

        // Guard: a comment on a private post is only interactable by that post's owner.
        if (comment.Post.Visibility == PostVisibility.Private && comment.Post.UserId != userId)
            throw new NotFoundException("Comment not found.");

        var existing = await _db.CommentLikes.SingleOrDefaultAsync(cl => cl.CommentId == commentId && cl.UserId == userId);
        if (existing is not null)
        {
            _db.CommentLikes.Remove(existing);
            await _db.SaveChangesAsync();
            return false;
        }

        _db.CommentLikes.Add(new CommentLike { CommentId = commentId, UserId = userId });
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<Post> EnsurePostAccessibleAsync(Guid postId, Guid currentUserId)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId);

        if (post is null)
            throw new NotFoundException("Post not found.");

        // Guard: same 404 for "doesn't exist" and "exists but private and not yours" — don't leak existence.
        if (post.Visibility == PostVisibility.Private && post.UserId != currentUserId)
            throw new NotFoundException("Post not found.");

        return post;
    }

    public async Task DeletePostAsync(Guid userId, Guid postId)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post is null)
            throw new NotFoundException("Post not found.");

        if (post.UserId != userId)
            throw new ForbiddenException("You can only delete your own posts.");

        _db.Posts.Remove(post); // cascades to Comments, Likes, CommentLikes via FK config
        await _db.SaveChangesAsync();
    }

    public async Task DeleteCommentAsync(Guid userId, Guid commentId)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null)
            throw new NotFoundException("Comment not found.");

        if (comment.UserId != userId)
            throw new ForbiddenException("You can only delete your own comments.");

        _db.Comments.Remove(comment); // cascades to Replies and CommentLikes via FK config
        await _db.SaveChangesAsync();
    }
}

