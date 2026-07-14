using System.Text;

namespace Appifylab.Common;

public static class CursorHelper
{
    public record Cursor(DateTime CreatedAt, Guid Id);

    public static string Encode(DateTime createdAt, Guid id)
    {
        var raw = $"{createdAt.Ticks}_{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static Cursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('_');
            var ticks = long.Parse(parts[0]);
            var id = Guid.Parse(parts[1]);
            return new Cursor(new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch
        {
            return null; // malformed cursor -> just start from the beginning
        }
    }
}