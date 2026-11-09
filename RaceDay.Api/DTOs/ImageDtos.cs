using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

/// <summary>multipart/form-data body for the image upload endpoints.</summary>
public class ImageUploadRequest
{
    /// <summary>The image file (JPG, PNG, WEBP or GIF, max 5 MB).</summary>
    [Required]
    public IFormFile File { get; set; } = null!;
}

public class ImageUploadResponse
{
    /// <summary>Public Azure Blob Storage URL of the uploaded image.</summary>
    public string ImageUrl { get; set; } = string.Empty;
}