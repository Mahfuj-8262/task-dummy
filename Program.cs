using System.Text;
using Amazon;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Appifylab.Common;
using Appifylab.Data;
using Appifylab.Endpoints;
using Appifylab.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// --- Database ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- JWT settings ---
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt settings are missing from configuration.");

if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || Encoding.UTF8.GetByteCount(jwtSettings.Secret) < 32)
    throw new InvalidOperationException("Jwt:Secret must be set (via user-secrets/env var) and at least 32 bytes long.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// --- AWS S3 image storage ---
builder.Services.Configure<S3Settings>(builder.Configuration.GetSection("S3"));
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var s3 = sp.GetRequiredService<IOptions<S3Settings>>().Value;

    var config = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(s3.Region) };
    if (!string.IsNullOrWhiteSpace(s3.ServiceUrl)) // LocalStack/MinIO for local dev
    {
        config.ServiceURL = s3.ServiceUrl;
        config.ForcePathStyle = true;
    }

    // Explicit keys only for local dev; otherwise use the default AWS credential chain (IAM role, env vars, ~/.aws).
    return !string.IsNullOrWhiteSpace(s3.AccessKey) && !string.IsNullOrWhiteSpace(s3.SecretKey)
        ? new AmazonS3Client(s3.AccessKey, s3.SecretKey, config)
        : new AmazonS3Client(config);
});

// --- App services ---
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IImageStorageService, S3ImageStorageService>();
builder.Services.AddScoped<IPostService, PostService>();

// --- CORS: needed so the browser sends/receives the refresh-token cookie cross-origin ---
const string CorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// Images are stored in and served from Amazon S3 (optionally fronted by CloudFront),
// so the app no longer serves uploads off local disk.

app.MapAuthEndpoints();
app.MapPostEndpoints(); // ADDED

app.Run();