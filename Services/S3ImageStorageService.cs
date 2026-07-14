using Amazon.S3;
using Amazon.S3.Model;
using Appifylab.Common;
using Microsoft.Extensions.Options;

namespace Appifylab.Services;

public class S3ImageStorageService : IImageStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly S3Settings _settings;
    private readonly ILogger<S3ImageStorageService> _logger;

    public S3ImageStorageService(IAmazonS3 s3, IOptions<S3Settings> settings, ILogger<S3ImageStorageService> logger)
    {
        _s3 = s3;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> UploadAsync(Stream content, string contentType, string fileExtension, CancellationToken ct = default)
    {
        // Version-7 GUIDs keep keys roughly time-sortable while staying unguessable.
        var key = $"{_settings.KeyPrefix}{Guid.CreateVersion7()}{fileExtension}";

        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
            // No CannedACL: modern buckets have ACLs disabled (bucket-owner-enforced).
            // Grant public read via bucket policy or serve through CloudFront instead.
        };

        await _s3.PutObjectAsync(request, ct);

        return $"{PublicBaseUrl}/{key}";
    }

    public async Task DeleteAsync(string imageUrl, CancellationToken ct = default)
    {
        var key = ExtractKey(imageUrl);
        if (key is null)
            return;

        await _s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key
        }, ct);
    }

    private string PublicBaseUrl =>
        string.IsNullOrWhiteSpace(_settings.PublicBaseUrl)
            ? $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com"
            : _settings.PublicBaseUrl.TrimEnd('/');

    // Recovers the object key from a stored URL so we can delete it. Returns null if the URL
    // doesn't belong to our configured base (e.g. legacy local "/uploads/..." paths) so callers
    // don't attempt a bogus S3 delete.
    private string? ExtractKey(string imageUrl)
    {
        var prefix = $"{PublicBaseUrl}/";
        if (!imageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Image URL {ImageUrl} does not match S3 base {Prefix}; skipping delete.", imageUrl, prefix);
            return null;
        }

        return imageUrl[prefix.Length..];
    }
}
