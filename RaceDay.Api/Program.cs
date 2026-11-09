using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Database (EF Core, Code-First, SQL Server) ----
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- Server-side session (stores UserId and Role after login) ----
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

// ---- Azure Blob Storage (image uploads) ----
builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection("AzureBlobStorage"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

builder.Services.AddControllers();

// ---- Swagger ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "Event management API for South African road running, walking and cycling events. " +
            "Authentication is session based: call POST /api/auth/login first, after which the " +
            "browser sends the session cookie automatically, so every protected endpoint can be " +
            "tested directly from this page. Roles: Organiser and Participant."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// ---- Optional: apply EF Core migrations on start-up (used by Docker / docker-compose) ----
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            // The SQL Server container can take a while to accept connections.
            app.Logger.LogWarning(ex, "Database not ready (attempt {Attempt} of 10). Retrying in 5 seconds...", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    options.RoutePrefix = "swagger";
});

// The MVC app calls this API over plain http (locally and inside Docker), so skip the https redirect there.
var inContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
if (!app.Environment.IsDevelopment() && !inContainer)
{
    app.UseHttpsRedirection();
}

app.UseSession();      // must come before MapControllers so filters can read the session
app.MapControllers();

app.Run();

// Exposed so the unit/integration test project (point 5) can use WebApplicationFactory<Program>
public partial class Program { }
