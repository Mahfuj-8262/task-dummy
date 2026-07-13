namespace Appifylab.Data.Models;

public enum PostVisibility
{
    Public,
    Private
}

public class Post
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public PostVisibility Visibility { get; set; } = PostVisibility.Public;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
}