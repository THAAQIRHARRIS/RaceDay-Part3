using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

public class ApiResult<T>
{
    public bool Success { get; init; }
    public HttpStatusCode Status { get; init; }
    public T? Data { get; init; }

    /// <summary>A message that is safe to show to the user (never a raw exception).</summary>
    public string Error { get; init; } = string.Empty;
}

/// <summary>Used as T when an endpoint returns no useful body.</summary>
public class EmptyResponse { }

/// <summary>The API no longer recognises the logged-in user (its session expired or the API restarted).</summary>
public class SessionExpiredException : Exception { }

/// <summary>The API could not be reached.</summary>
public class ApiUnavailableException : Exception
{
    public ApiUnavailableException(Exception inner) : base("The RaceDay API is unavailable.", inner) { }
}

/// <summary>
/// The only gateway from the MVC application to the RaceDay API.
/// It forwards the API's session cookie for the current user and turns API errors into friendly messages.
/// </summary>
public class ApiClient
{
    private const string ApiCookieName = ".RaceDay.Session";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;

    public ApiClient(HttpClient http, IHttpContextAccessor accessor)
    {
        _http = http;
        _accessor = accessor;
    }

    private ISession Session => _accessor.HttpContext!.Session;

    public Task<ApiResult<T>> GetAsync<T>(string url) =>
        SendAsync<T>(HttpMethod.Get, url, null);

    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null) =>
        SendAsync<T>(HttpMethod.Post, url, body == null ? null : JsonContent.Create(body));

    public Task<ApiResult<T>> PutAsync<T>(string url, object body) =>
        SendAsync<T>(HttpMethod.Put, url, JsonContent.Create(body));

    public Task<ApiResult<T>> DeleteAsync<T>(string url) =>
        SendAsync<T>(HttpMethod.Delete, url, null);

    /// <summary>Sends an image to an API upload endpoint as multipart/form-data (field name "file").</summary>
    public Task<ApiResult<ImageUploadDto>> UploadImageAsync(string url, IFormFile file)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        if (MediaTypeHeaderValue.TryParse(file.ContentType, out var contentType))
            fileContent.Headers.ContentType = contentType;
        form.Add(fileContent, "file", Path.GetFileName(file.FileName));

        return SendAsync<ImageUploadDto>(HttpMethod.Post, url, form);
    }

    public async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content, bool expireSessionOn401 = true)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        var cookie = Session.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", $"{ApiCookieName}={cookie}");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiUnavailableException(ex);
        }
        catch (TaskCanceledException ex)   // timeout
        {
            throw new ApiUnavailableException(ex);
        }

        using (response)
        {
            CaptureApiCookie(response);

            if (response.StatusCode == HttpStatusCode.Unauthorized && expireSessionOn401 && !string.IsNullOrEmpty(cookie))
                throw new SessionExpiredException();

            if (response.IsSuccessStatusCode)
            {
                T? data = default;
                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    try
                    {
                        data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
                    }
                    catch (JsonException)
                    {
                        // empty or non-JSON body: nothing to map
                    }
                }
                return new ApiResult<T> { Success = true, Status = response.StatusCode, Data = data };
            }

            return new ApiResult<T>
            {
                Success = false,
                Status = response.StatusCode,
                Error = await ReadErrorAsync(response)
            };
        }
    }

    /// <summary>Remembers the API's session cookie (set at login) so later calls act as the same user.</summary>
    private void CaptureApiCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;

        var prefix = ApiCookieName + "=";
        foreach (var header in values)
        {
            if (!header.StartsWith(prefix, StringComparison.Ordinal)) continue;

            var value = header.Substring(prefix.Length).Split(';')[0];
            if (string.IsNullOrEmpty(value))
                Session.Remove(SessionKeys.ApiCookie);
            else
                Session.SetString(SessionKeys.ApiCookie, value);
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (status >= 500)
            return "Something went wrong on our side. Please try again in a moment.";

        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    // ASP.NET validation errors: { "errors": { "Field": ["message", ...] } }
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = new List<string>();
                        foreach (var field in errors.EnumerateObject())
                        {
                            if (field.Value.ValueKind != JsonValueKind.Array) continue;
                            foreach (var item in field.Value.EnumerateArray())
                            {
                                var message = item.GetString();
                                if (!string.IsNullOrWhiteSpace(message)) messages.Add(message);
                            }
                        }
                        if (messages.Count > 0) return string.Join(" ", messages.Distinct());
                    }

                    // Our own errors: ProblemDetails with a curated "detail" message.
                    if (root.TryGetProperty("detail", out var detail) &&
                        detail.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(detail.GetString()))
                        return detail.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            // fall through to the generic messages
        }

        return status switch
        {
            400 => "Please check the information you entered and try again.",
            401 => "Please log in to continue.",
            403 => "You don't have permission to do that.",
            404 => "We couldn't find what you were looking for.",
            409 => "That conflicts with existing information.",
            _ => "The request could not be completed."
        };
    }
}
