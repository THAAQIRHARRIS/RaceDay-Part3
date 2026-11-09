using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ApiExceptionFilter>();                          // friendly handling when the API is down / session expired
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());   // CSRF protection on every POST
});

// ---- Session: stores who is logged in (id, role, name) and the API's own session cookie ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

// ---- Typed HttpClient for the Part 2 API. The MVC app never touches the database or Azure directly. ----
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5080/";
if (!apiBaseUrl.EndsWith('/')) apiBaseUrl += "/";

builder.Services.AddHttpClient<ApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    // Cookies are forwarded manually (per logged-in user), so the handler must not keep a shared cookie jar.
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });

var app = builder.Build();

// Invariant culture everywhere: "10.5" means ten and a half no matter what the server's regional settings are.
var culture = CultureInfo.InvariantCulture;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = new List<CultureInfo> { culture },
    SupportedUICultures = new List<CultureInfo> { culture }
});

// Never show raw exceptions to users.
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

var inContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
if (!inContainer)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Stop the browser caching pages, so the Back button after logging out cannot show private pages.
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store, no-cache";
    await next();
});

app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
