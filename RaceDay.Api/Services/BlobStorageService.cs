using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

/// <summary>Settings bound from the "AzureBlobStorage" section of configuration.</summary>
public class BlobStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "raceday-images";
}

/// <summary>The uploaded file is missing, too large, or not a supported image.</summary>
public class ImageValidationException : Exception
{
    public ImageValidationException(string message) : base(message) { }
}

/// <summary>No Azure connection string has been configured.</summary>
public class StorageNotConfiguredException : Exception
{
    public StorageNotConfiguredException() : base("Azure Blob Storage is not configured.") { }
}

public interface IBlobStorageService
{
    /// <summary>Validates an image and uploads it to Azure Blob Storage. Returns the public blob URL.</summary>
    Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);

    /// <summary>Best-effort removal of an image this service uploaded earlier (used when an image is replaced).</summary>
    Task DeleteIfOwnedAsync(string? blobUrl, CancellationToken cancellationToken = default);
}

public class BlobStorageService : IBlobStorageService
{
    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif"
    };

    private readonly BlobStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private BlobContainerClient? _container;

    public BlobStorageService(IOptions<BlobStorageOptions> options, ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ImageValidationException("Please choose an image file to upload.");
        if (file.Length > MaxBytes)
            throw new ImageValidationException("The image must be 5 MB or smaller.");

        var extension = Path.GetExtension(file.FileName);
        if (!ContentTypes.TryGetValue(extension, out var contentType))
            throw new ImageValidationException("Only JPG, PNG, WEBP or GIF images are allowed.");

        // Check the file's real content, not just its name, so a renamed document is rejected.
        var header = new byte[12];
        int read;
        await using (var headerStream = file.OpenReadStream())
        {
            read = await headerStream.ReadAtLeastAsync(header.AsMemory(), header.Length, false, cancellationToken);
        }
        if (!LooksLikeImage(header, read))
            throw new ImageValidationException("That file is not a valid image.");

        var container = await GetContainerAsync(cancellationToken);
        var blobName = $"{folder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var blob = container.GetBlobClient(blobName);

        await using var uploadStream = file.OpenReadStream();
        await blob.UploadAsync(uploadStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,
                CacheControl = "public, max-age=31536000"
            }
        }, cancellationToken);

        return blob.Uri.AbsoluteUri;
    }

    public async Task DeleteIfOwnedAsync(string? blobUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobUrl) || string.IsNullOrWhiteSpace(_options.ConnectionString))
            return;

        try
        {
            if (!Uri.TryCreate(blobUrl, UriKind.Absolute, out var uri)) return;

            var container = await GetContainerAsync(cancellationToken);
            var builder = new BlobUriBuilder(uri);

            // Only delete blobs that live in our own container.
            if (!string.Equals(uri.Host, container.Uri.Host, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(builder.BlobContainerName, container.Name, StringComparison.OrdinalIgnoreCase))
                return;

            await container.GetBlobClient(builder.BlobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete the old image {Url}.", blobUrl);
        }
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container != null) return _container;
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new StorageNotConfiguredException();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_container == null)
            {
                var client = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
                // Images must be viewable in the browser, so blobs (not the container listing) are public.
                await client.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
                _container = client;
            }
            return _container;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool LooksLikeImage(byte[] h, int length)
    {
        if (length < 4) return false;

        // JPEG
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return true;
        // PNG
        if (length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 &&
            h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return true;
        // GIF ("GIF8")
        if (h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38) return true;
        // WEBP ("RIFF" .... "WEBP")
        if (length >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 &&
            h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50) return true;

        return false;
    }
}
