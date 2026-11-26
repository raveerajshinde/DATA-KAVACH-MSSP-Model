using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Services;

namespace DataKavach.Endpoints;

public static class AuthEndpoints
{
    static async Task<object> Session(TokenService tokens, PermissionService perms, Dictionary<string, object?> u, Db db)
    {
        var type = Db.Str(u.GetValueOrDefault("UserType")) ?? "Provider";
        var org = Db.Long(u.GetValueOrDefault("OrganizationId"));
        if (type == "Customer" && (org <= 0 || await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Organizations")} WHERE {db.Q("Id")} = ? AND {db.Q("IsDeleted")} = 0", org) == 0))
            throw new ApiError(403, "Your customer portal account is not linked to an active organisation. Contact your service provider.");
        var (token, exp) = tokens.Issue(Db.Long(u["Id"]), Db.Str(u["Email"])!, Db.Str(u["FullName"]) ?? "", Db.Str(u["Role"])!, type, org);
        return new
        {
            token, expires = exp,
            user = new { id = Db.Long(u["Id"]), email = Db.Str(u["Email"]), name = Db.Str(u["FullName"]), role = Db.Str(u["Role"]), mfaEnabled = Db.Bool(u["MfaEnabled"]), mustChangePassword = Db.Bool(u["MustChangePassword"]), userType = type, organizationId = org },
            permissions = (await perms.ForRoleAsync(Db.Str(u["Role"])!)).OrderBy(x => x).ToArray()
        };
    }

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", async (Db db, Migrator m, DataKavach.Reports.PdfConverter pdf) =>
        {
            try { return Results.Ok(new { status = "ok", schema = await m.CurrentVersionAsync(), pdf = pdf.Available, time = DateTime.UtcNow, version = typeof(Program).Assembly.GetName().Version?.ToString() }); }
            catch (Exception e) { return Results.Json(new { status = "error", error = e.Message }, statusCode: 503); }
        });

        app.MapGet("/api/setup/status", async (Db db) => new { needsSetup = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Users")}") == 0 });

        app.MapPost("/api/setup", async (HttpContext ctx, Db db, SeedService seed, AuditService audit) =>
        {
            if (await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Users")}") > 0) throw new ApiError(409, "Setup has already been completed.");
            var b = await ctx.Body();
            var id = await seed.CreateUserAsync(b.Str("email") ?? "", b.Str("name") ?? "Administrator", b.Str("password") ?? "", Roles.SuperAdmin);
            await audit.LogAsync("SETUP", "users", id, null, new { email = b.Str("email") }, new CurrentUser(id, b.Str("email") ?? "", b.Str("name") ?? "", Roles.SuperAdmin));
            return Results.Ok(new { created = id });
        });

        app.MapPost("/api/auth/login", async (HttpContext ctx, Db db, TokenService tokens, PermissionService perms, FieldCrypto crypto, AuditService audit) =>
        {
            var b = await ctx.Body();
            var email = (b.Str("email") ?? "").Trim().ToLowerInvariant();
            var u = await db.FirstAsync($"SELECT * FROM {db.Q("Users")} WHERE {db.Q("Email")} = ? AND {db.Q("IsDeleted")} = 0", email);
            const string generic = "Invalid email or password.";
            if (u == null) { await audit.LogAsync("LOGIN_FAILED", "users", null, null, new { email }); throw new ApiError(401, generic); }
            var id = Db.Long(u["Id"]);
            if (!Db.Bool(u["IsActive"])) throw new ApiError(403, "This account is disabled.");
            if (Db.Dt(u["LockoutUntil"]) is { } lu && lu > DateTime.UtcNow) throw new ApiError(423, $"Account locked after repeated failures. Try again after {lu:HH:mm} UTC.");
            var cu = new CurrentUser(id, email, Db.Str(u["FullName"]) ?? "", Db.Str(u["Role"])!, Db.Str(u.GetValueOrDefault("UserType")) ?? "Provider", Db.Long(u.GetValueOrDefault("OrganizationId")));
            if (!Passwords.Verify(Db.Str(u["PasswordHash"])!, b.Str("password") ?? ""))
            {
                var fails = Db.Int(u["FailedAttempts"]) + 1;
                await db.UpdateAsync("Users", id, new Dictionary<string, object?> { ["FailedAttempts"] = fails, ["LockoutUntil"] = fails >= 5 ? DateTime.UtcNow.AddMinutes(15) : null });
                await audit.LogAsync("LOGIN_FAILED", "users", id, null, new { email, fails }, cu);
                throw new ApiError(401, generic);
            }
            if (Db.Bool(u["MfaEnabled"]))
            {
                var code = b.Str("mfaCode");
                if (string.IsNullOrEmpty(code)) return Results.Ok(new { mfaRequired = true });
                if (!Totp.Verify(crypto.Unprotect(Db.Str(u["MfaSecret"])!), code))
                {
                    await audit.LogAsync("MFA_FAILED", "users", id, null, null, cu);
                    throw new ApiError(401, "Invalid authentication code.");
                }
            }
            await db.UpdateAsync("Users", id, new Dictionary<string, object?> { ["FailedAttempts"] = 0, ["LockoutUntil"] = null, ["LastLoginAt"] = DateTime.UtcNow });
            await audit.LogAsync("LOGIN", "users", id, cu.IsCustomer ? cu.OrgId : null, null, cu);
            return Results.Ok(await Session(tokens, perms, u, db));
        }).RequireRateLimiting("login");

        app.MapPost("/api/auth/refresh", async (HttpContext ctx, Db db, TokenService tokens, PermissionService perms) =>
        {
            var cu = ctx.U();
            var u = await db.FirstAsync($"SELECT * FROM {db.Q("Users")} WHERE {db.Q("Id")} = ? AND {db.Q("IsDeleted")} = 0 AND {db.Q("IsActive")} = 1", cu.Id) ?? throw new ApiError(401, "Account no longer active");
            return Results.Ok(await Session(tokens, perms, u, db));
        }).RequireAuthorization();

        app.MapPost("/api/auth/logout", async (HttpContext ctx, AuditService audit) => { await audit.LogAsync("LOGOUT", "users", ctx.U().Id, null); return Results.Ok(); }).RequireAuthorization();

        app.MapPost("/api/auth/change-password", async (HttpContext ctx, Db db, AuditService audit) =>
        {
            var cu = ctx.U();
            var b = await ctx.Body();
            var u = await db.FirstAsync($"SELECT * FROM {db.Q("Users")} WHERE {db.Q("Id")} = ?", cu.Id) ?? throw ApiError.NotFound("User");
            if (!Passwords.Verify(Db.Str(u["PasswordHash"])!, b.Str("currentPassword") ?? "")) throw ApiError.Bad("Current password is incorrect.");
            var np = b.Str("newPassword") ?? "";
            if (Passwords.Validate(np) is { } err) throw ApiError.Bad(err);
            if (Passwords.Verify(Db.Str(u["PasswordHash"])!, np)) throw ApiError.Bad("New password must differ from the current password.");
            await db.UpdateAsync("Users", cu.Id, new Dictionary<string, object?> { ["PasswordHash"] = Passwords.Hash(np), ["MustChangePassword"] = false, ["PasswordChangedAt"] = DateTime.UtcNow });
            await audit.LogAsync("PASSWORD_CHANGE", "users", cu.Id, null);
            return Results.Ok();
        }).RequireAuthorization();

        app.MapPost("/api/auth/mfa/setup", async (HttpContext ctx, Db db, FieldCrypto crypto) =>
        {
            var cu = ctx.U();
            var secret = Totp.NewSecret();
            await db.UpdateAsync("Users", cu.Id, new Dictionary<string, object?> { ["MfaSecret"] = crypto.Protect(secret), ["MfaEnabled"] = false });
            return Results.Ok(new { secret, uri = Totp.Uri(secret, cu.Email) });
        }).RequireAuthorization();

        app.MapPost("/api/auth/mfa/enable", async (HttpContext ctx, Db db, FieldCrypto crypto, AuditService audit) =>
        {
            var cu = ctx.U();
            var b = await ctx.Body();
            var u = await db.FirstAsync($"SELECT * FROM {db.Q("Users")} WHERE {db.Q("Id")} = ?", cu.Id);
            var sec = Db.Str(u?["MfaSecret"]);
            if (sec == null) throw ApiError.Bad("Start MFA setup first.");
            if (!Totp.Verify(crypto.Unprotect(sec), b.Str("code") ?? "")) throw ApiError.Bad("Code did not match. Check the time on your device and try again.");
            await db.UpdateAsync("Users", cu.Id, new Dictionary<string, object?> { ["MfaEnabled"] = true });
            await audit.LogAsync("MFA_ENABLED", "users", cu.Id, null);
            return Results.Ok();
        }).RequireAuthorization();

        app.MapPost("/api/auth/mfa/disable", async (HttpContext ctx, Db db, AuditService audit) =>
        {
            var cu = ctx.U();
            var b = await ctx.Body();
            var u = await db.FirstAsync($"SELECT * FROM {db.Q("Users")} WHERE {db.Q("Id")} = ?", cu.Id);
            if (!Passwords.Verify(Db.Str(u!["PasswordHash"])!, b.Str("password") ?? "")) throw ApiError.Bad("Password is incorrect.");
            await db.UpdateAsync("Users", cu.Id, new Dictionary<string, object?> { ["MfaEnabled"] = false, ["MfaSecret"] = null });
            await audit.LogAsync("MFA_DISABLED", "users", cu.Id, null);
            return Results.Ok();
        }).RequireAuthorization();
    }
}
