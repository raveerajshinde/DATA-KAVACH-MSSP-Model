using System.Threading.RateLimiting;
using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Endpoints;
using DataKavach.Reports;
using DataKavach.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.Host.UseWindowsService(o => o.ServiceName = "DataKavach");
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

var cfg = builder.Configuration;
builder.Services.Configure<DbOptions>(cfg.GetSection("Database"));
builder.Services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
builder.Services.Configure<StorageOptions>(cfg.GetSection("Storage"));
builder.Services.Configure<PdfOptions>(cfg.GetSection("Pdf"));
builder.Services.Configure<BackupOptions>(cfg.GetSection("Backup"));
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 60L * 1024 * 1024);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 60L * 1024 * 1024);

builder.Services.AddSingleton(sp => new Db(sp.GetRequiredService<IOptions<DbOptions>>().Value));
builder.Services.AddSingleton<Migrator>();
builder.Services.AddSingleton<PermissionService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<FieldCrypto>();
builder.Services.AddSingleton<PdfConverter>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<EntityService>();
builder.Services.AddScoped<BreachService>();
builder.Services.AddScoped<AssessmentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<RoadmapService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<ImportExportService>();
builder.Services.AddScoped<PolicyService>();
builder.Services.AddScoped<ReportBuilder>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<BackupService>();
foreach (var t in typeof(IEntityHooks).Assembly.GetTypes().Where(t => typeof(IEntityHooks).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false }))
    builder.Services.AddScoped(typeof(IEntityHooks), t);

var storageRoot = Path.GetFullPath(cfg["Storage:Root"] ?? "storage", AppContext.BaseDirectory);
builder.Services.PostConfigure<StorageOptions>(o => o.Root = storageRoot);
var dp = builder.Services.AddDataProtection().SetApplicationName("DataKavach").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(storageRoot, "keys")));
if (OperatingSystem.IsWindows()) dp.ProtectKeysWithDpapi(protectToLocalMachine: true);

builder.Services.AddAuthentication(JwtAuthHandler.SchemeName).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, JwtAuthHandler>(JwtAuthHandler.SchemeName, null);
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "x", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Request.Headers["X-Api-Key"].ToString() is { Length: > 8 } k ? k[..8] : "anon", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase; });

var app = builder.Build();

// ---------------------------------------------------------------- CLI commands (used by the installer)
if (args.Length > 0 && args[0].StartsWith("--"))
{
    Environment.ExitCode = await Cli.RunAsync(app, args);
    return;
}

Directory.CreateDirectory(storageRoot);
var jwtKey = cfg["Jwt:SigningKey"] ?? "";
if (jwtKey.Length < 32) throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters. The installer generates one in appsettings.Local.json.");

using (var scope = app.Services.CreateScope())
{
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    if (cfg.GetValue("Database:AutoMigrate", true))
    {
        var applied = await scope.ServiceProvider.GetRequiredService<Migrator>().MigrateAsync();
        if (applied.Count > 0) log.LogInformation("Applied migrations: {M}", string.Join(", ", applied));
    }
    await scope.ServiceProvider.GetRequiredService<SeedService>().SeedSystemAsync();
}

app.UseMiddleware<ErrorMiddleware>();
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "SAMEORIGIN";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!ctx.Request.Path.StartsWithSegments("/api"))
        h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-src 'self' blob:; object-src 'none'; base-uri 'self'";
    if (ctx.Request.Path.StartsWithSegments("/api")) h["Cache-Control"] = "no-store";
    await next();
});
if (cfg.GetValue("Https:Redirect", false)) app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapEntityEndpoints();
app.MapDomainEndpoints();
app.MapReportEndpoints();
app.MapAdminEndpoints();
app.MapIntegrationEndpoints();
app.MapProviderEndpoints();

app.MapFallback(async ctx =>
{
    if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 404; await ctx.Response.WriteAsJsonAsync(new { error = "Not found" }); return; }
    var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "index.html");
    if (File.Exists(index)) { ctx.Response.ContentType = "text/html"; await ctx.Response.SendFileAsync(index); }
    else { ctx.Response.StatusCode = 404; await ctx.Response.WriteAsync("Frontend not deployed."); }
});

app.Run();

public partial class Program { }
