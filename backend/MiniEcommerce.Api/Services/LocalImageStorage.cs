using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using MiniEcommerce.Api.Interfaces;

namespace MiniEcommerce.Api.Services;

public class LocalImageStorage : IImageStorage
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private readonly string _webRoot;
    private readonly ILogger<LocalImageStorage> _logger;

    public LocalImageStorage(IWebHostEnvironment env, ILogger<LocalImageStorage> logger)
    {
        _webRoot = env.WebRootPath;
        _logger = logger;
    }

    public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        if (stream.Length > MaxFileSize)
            throw new Exceptions.ValidationException("Image exceeds 5 MB limit.");

        // Validate image format via ImageSharp
        IImageFormat? format;
        try
        {
            using var probe = await Image.LoadAsync(stream, cancellationToken);
            format = probe.Metadata.DecodedImageFormat;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid image file upload");
            throw new Exceptions.ValidationException("File is not a valid image.");
        }

        if (format is null)
            throw new Exceptions.ValidationException("Could not detect image format.");

        // Reset stream position for saving
        stream.Position = 0;

        var now = DateTimeOffset.UtcNow;
        var ext = format.FileExtensions.FirstOrDefault() ?? "png";
        var relativePath = Path.Combine("images", now.ToString("yyyy"), now.ToString("mm"),
            $"{Guid.NewGuid()}.{ext}");
        var fullPath = Path.Combine(_webRoot, relativePath);

        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);
        await stream.CopyToAsync(fileStream, cancellationToken);

        _logger.LogInformation("Saved image to {Path}", relativePath);
        return GetPublicUrl(relativePath);
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        // Convert public URL back to file path
        var relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_webRoot, relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted image at {Path}", fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string relativePath)
    {
        return "/" + relativePath.Replace('\\', '/');
    }
}
