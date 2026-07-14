namespace Appifylab.Services;

public interface IImageStorageService
{
    /// <summary>Uploads image bytes and returns a publicly reachable URL.</summary>
    Task<string> UploadAsync(Stream content, string contentType, string fileExtension, CancellationToken ct = default);

    /// <summary>Best-effort delete of a previously uploaded image, given the URL returned by <see cref="UploadAsync"/>.</summary>
    Task DeleteAsync(string imageUrl, CancellationToken ct = default);
}
