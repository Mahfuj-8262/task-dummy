namespace Appifylab.Common;

public class S3Settings
{
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";

    // Leave AccessKey/SecretKey empty in production and rely on the default AWS credential
    // chain instead (IAM role, environment variables, ~/.aws/credentials). Only set them
    // for local dev via user-secrets/env vars — never commit real keys to appsettings.
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }

    // Object key prefix (acts like a folder). Mirrors the old "/uploads/" layout.
    public string KeyPrefix { get; set; } = "uploads/";

    // Base URL the stored ImageUrl is built from. Point this at a CloudFront distribution
    // to serve via CDN without a code change. When empty, the virtual-hosted S3 URL is used.
    public string? PublicBaseUrl { get; set; }

    // Optional custom endpoint for S3-compatible stores (LocalStack, MinIO) during local dev.
    public string? ServiceUrl { get; set; }
}
