using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Services;

namespace DataKavach.Endpoints;

public sealed class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (ApiError e)
        {
            if (ctx.Response.HasStarted) throw;
            ctx.Response.StatusCode = e.Status;
            await ctx.Response.WriteAsJsonAsync(new { error = e.Message });
        }
        catch (KeyNotFoundException e)
        {
            if (ctx.Response.HasStarted) throw;
            ctx.Response.StatusCode = 404;
            await ctx.Response.WriteAsJsonAsync(new { error = e.Message });
        }
        catch (BadHttpRequestException e)
        {
            if (ctx.Response.HasStarted) throw;
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { error = e.Message });
        }
        catch (Exception e)
        {
            log.LogError(e, "Unhandled error on {Path}", ctx.Request.Path);
            if (ctx.Response.HasStarted) throw;
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. The error has been logged.", detail = e is InvalidOperationException ? e.Message : null });
        }
    }
}

public static class Http
{
    public static CurrentUser U(this HttpContext ctx) => CurrentUser.From(ctx.User) ?? throw new ApiError(401, "Not authenticated");

    public static long? QLong(this HttpContext ctx, string k) => long.TryParse(ctx.Request.Query[k], out var v) ? v : null;
    /// <summary>orgId from the query string, enforced against the caller's organisation for customer users.</summary>
    public static long OrgId(this HttpContext ctx)
    {
        var u = CurrentUser.From(ctx.User);
        var id = ctx.QLong("orgId") ?? (u?.IsCustomer == true ? u.OrgId : (long?)null) ?? throw ApiError.Bad("orgId is required");
        if (u is { IsCustomer: true } && id != u.OrgId) throw new ApiError(403, "You do not have access to this organisation.");
        return id;
    }
    /// <summary>Optional orgId (0 = provider-wide defaults). Customers are always pinned to their own organisation.</summary>
    public static long OrgIdOrZero(this HttpContext ctx)
    {
        var u = CurrentUser.From(ctx.User);
        if (u is { IsCustomer: true }) { var q = ctx.QLong("orgId"); if (q != null && q != u.OrgId) throw new ApiError(403, "You do not have access to this organisation."); return u.OrgId; }
        return ctx.QLong("orgId") ?? 0;
    }
    public static void EnsureOrg(this HttpContext ctx, long orgId)
    {
        var u = CurrentUser.From(ctx.User);
        if (u is { IsCustomer: true } && orgId != u.OrgId) throw new ApiError(403, "You do not have access to this organisation.");
    }
    public static void EnsureProvider(this HttpContext ctx)
    {
        if (CurrentUser.From(ctx.User) is { IsCustomer: true }) throw new ApiError(403, "This function is available to the service provider only.");
    }
    public static DateTime? QDate(this HttpContext ctx, string k) => DateTime.TryParse(ctx.Request.Query[k], out var v) ? v : null;
    public static string? Q(this HttpContext ctx, string k) => ctx.Request.Query[k].FirstOrDefault();

    public static async Task<JsonElement> Body(this HttpContext ctx)
    {
        try { return await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body); }
        catch (JsonException) { throw ApiError.Bad("Invalid JSON"); }
    }

    public static string? Str(this JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()) : null;
    public static long? Long(this JsonElement e, string k) => long.TryParse(e.Str(k), out var v) ? v : null;
    public static bool Bool(this JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
    public static DateTime? Date(this JsonElement e, string k) => DateTime.TryParse(e.Str(k), out var v) ? v : null;

    public static IResult File(byte[] data, string contentType, string name) => Results.File(data, contentType, name);
}

/// <summary>Installer/maintenance commands: --migrate, --create-admin, --script-schema, --set-password, --health.</summary>
public static class Cli
{
    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        try
        {
            switch (args[0])
            {
                case "--script-schema":
                {
                    var dir = args.Length > 1 ? args[1] : ".";
                    foreach (var d in new[] { Dialect.SqlServer, Dialect.Sqlite })
                    {
                        var path = Path.Combine(dir, d.ToString(), "0001_initial.sql");
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        await File.WriteAllTextAsync(path, DdlGenerator.CreateAll(d));
                        Console.WriteLine($"Wrote {path}");
                    }
                    return 0;
                }
                case "--migrate":
                {
                    var applied = await sp.GetRequiredService<Migrator>().MigrateAsync();
                    await sp.GetRequiredService<SeedService>().SeedSystemAsync();
                    Console.WriteLine(applied.Count == 0 ? "Database is up to date." : "Applied: " + string.Join(", ", applied));
                    Console.WriteLine($"Schema version: {await sp.GetRequiredService<Migrator>().CurrentVersionAsync()}");
                    return 0;
                }
                case "--create-admin":
                {
                    if (args.Length < 4) { Console.Error.WriteLine("Usage: --create-admin <email> <full name> <password>"); return 1; }
                    var db = sp.GetRequiredService<Db>();
                    var exists = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Users")} WHERE {db.Q("Email")} = ?", args[1].Trim().ToLowerInvariant());
                    if (exists > 0) { Console.WriteLine("Administrator already exists - skipped."); return 0; }
                    var id = await sp.GetRequiredService<SeedService>().CreateUserAsync(args[1], args[2], args[3], Roles.SuperAdmin, mustChange: true);
                    Console.WriteLine($"Created Super Administrator #{id} ({args[1]}). Password change is required at first login.");
                    return 0;
                }
                case "--set-password":
                {
                    if (args.Length < 3) { Console.Error.WriteLine("Usage: --set-password <email> <password>"); return 1; }
                    var err = Passwords.Validate(args[2]);
                    if (err != null) { Console.Error.WriteLine(err); return 1; }
                    var db = sp.GetRequiredService<Db>();
                    var n = await db.ExecAsync($"UPDATE {db.Q("Users")} SET {db.Q("PasswordHash")} = ?, {db.Q("FailedAttempts")} = 0, {db.Q("LockoutUntil")} = NULL, {db.Q("MustChangePassword")} = ? WHERE {db.Q("Email")} = ?", Passwords.Hash(args[2]), true, args[1].Trim().ToLowerInvariant());
                    Console.WriteLine(n > 0 ? "Password reset." : "User not found.");
                    return n > 0 ? 0 : 1;
                }
                case "--health":
                {
                    var db = sp.GetRequiredService<Db>();
                    Console.WriteLine($"Database OK - schema version {await sp.GetRequiredService<Migrator>().CurrentVersionAsync()}, users {await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Users")}")}");
                    Console.WriteLine($"PDF engine: {(sp.GetRequiredService<DataKavach.Reports.PdfConverter>().Available ? "LibreOffice found" : "LibreOffice NOT found")}");
                    return 0;
                }
                default:
                    Console.Error.WriteLine("Unknown command. Use --migrate | --create-admin | --set-password | --script-schema | --health");
                    return 1;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("ERROR: " + e.Message);
            return 2;
        }
    }
}
