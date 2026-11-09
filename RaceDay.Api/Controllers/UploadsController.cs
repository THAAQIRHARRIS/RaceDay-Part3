using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Image uploads. Files are validated here, stored in Azure Blob Storage, and only the blob URL is saved in the database.
/// The MVC application never talks to Azure directly - it sends the file to these endpoints.
/// </summary>
public class UploadsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _storage;
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(RaceDayDbContext db, IBlobStorageService storage, ILogger<UploadsController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Upload (or replace) an event's banner image.</summary>
    /// <remarks>
    /// Role required: Organiser (owner of the event). Send multipart/form-data with a single file field named
    /// <c>file</c> (JPG, PNG, WEBP or GIF, max 5 MB). The image is stored in Azure Blob Storage and its URL
    /// is saved on the event.
    /// </remarks>
    [HttpPost("api/events/{id:int}/banner")]
    [SessionAuthorize(Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ImageUploadResponse>> UploadBanner(int id, [FromForm] ImageUploadRequest request, CancellationToken cancellationToken)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id, cancellationToken);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(StatusCodes.Status403Forbidden, "You can only change the banner of your own events.");

        var (url, error) = await TryUploadAsync(request.File, "event-banners", cancellationToken);
        if (error != null) return error;

        var oldUrl = ev.BannerImageUrl;
        ev.BannerImageUrl = url;
        await _db.SaveChangesAsync(cancellationToken);
        await _storage.DeleteIfOwnedAsync(oldUrl, cancellationToken);

        return Ok(new ImageUploadResponse { ImageUrl = url! });
    }

    /// <summary>Upload (or replace) the logged-in user's profile picture.</summary>
    /// <remarks>
    /// Role required: any logged-in user. Send multipart/form-data with a single file field named
    /// <c>file</c> (JPG, PNG, WEBP or GIF, max 5 MB).
    /// </remarks>
    [HttpPost("api/users/me/profile-image")]
    [SessionAuthorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ImageUploadResponse>> UploadProfileImage([FromForm] ImageUploadRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId, cancellationToken);
        if (user == null) return Fail(StatusCodes.Status404NotFound, "User not found.");

        var (url, error) = await TryUploadAsync(request.File, "profile-images", cancellationToken);
        if (error != null) return error;

        var oldUrl = user.ProfileImageUrl;
        user.ProfileImageUrl = url;
        await _db.SaveChangesAsync(cancellationToken);
        await _storage.DeleteIfOwnedAsync(oldUrl, cancellationToken);

        return Ok(new ImageUploadResponse { ImageUrl = url! });
    }

    private async Task<(string? Url, ObjectResult? Error)> TryUploadAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        try
        {
            return (await _storage.UploadImageAsync(file, folder, cancellationToken), null);
        }
        catch (ImageValidationException ex)
        {
            return (null, Fail(StatusCodes.Status400BadRequest, ex.Message));
        }
        catch (StorageNotConfiguredException)
        {
            return (null, Fail(StatusCodes.Status503ServiceUnavailable, "Image storage is not configured on the server."));
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Blob Storage request failed.");
            return (null, Fail(StatusCodes.Status503ServiceUnavailable, "Image storage is currently unavailable. Please try again later."));
        }
    }
}
