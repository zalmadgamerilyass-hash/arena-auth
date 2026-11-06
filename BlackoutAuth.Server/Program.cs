using Microsoft.EntityFrameworkCore;
using BlackoutAuth.Server.Data;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite Database
string dbPath = Environment.GetEnvironmentVariable("DB_PATH") 
    ?? Path.Combine(AppContext.BaseDirectory, "blackout_auth.db");

// Ensure directory exists if custom path provided (e.g. Railway volume /data/blackout_auth.db)
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
{
    Directory.CreateDirectory(dbDir);
}

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Add Controllers & JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Port configuration (e.g. 5000 or port from env/commandline)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Initialize Database & Seed default admin / demo app
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    AuthDbContext.InitializeSeedData(db);
}

app.UseCors("AllowAll");

// GLOBAL DEXTER CREDIT HEADERS
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Developer", "DEXTER");
    context.Response.Headers.Append("X-Powered-By", "Arena Auth - Developed by DEXTER");
    await next();
});

// Serve static files from wwwroot (Web Dashboard)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// Fallback to index.html for dashboard single-page navigation
app.MapFallbackToFile("index.html");

Console.WriteLine("==========================================================");
Console.WriteLine("          ⚡ ARENA AUTH SERVER STARTED ⚡             ");
Console.WriteLine("==========================================================");
Console.WriteLine($"  API Endpoint:       http://0.0.0.0:{port}/api/v1/auth   ");
Console.WriteLine($"  Admin Web Panel:    http://0.0.0.0:{port}/index.html    ");
Console.WriteLine($"  Database Path:      {dbPath}");
Console.WriteLine("  *** Login to admin panel with your credentials ***      ");
Console.WriteLine("  Default App Name:   BlackoutApp                         ");
Console.WriteLine("  Default Secret:     blackout_secret_9999a8b7c6d5e4f321  ");
Console.WriteLine("==========================================================");

app.Run();
