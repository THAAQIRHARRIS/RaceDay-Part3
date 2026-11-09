namespace RaceDay.Api.DTOs;

public class ImageUploadResponse
{
    /// <summary>Public Azure Blob Storage URL of the uploaded image.</summary>
    public string ImageUrl { get; set; } = string.Empty;
}
