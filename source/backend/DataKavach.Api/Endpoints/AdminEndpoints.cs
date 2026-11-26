using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Services;
using Microsoft.Extensions.Options;

namespace DataKavach.Endpoints;

public sealed class BackupOptions
{
    public string Directory { get; set; } = "";
}

public sealed class BackupService(Db db, IOptions<BackupOptions> opt, IOptions<StorageOptions> storage, AuditService audit)
{
    public string Dir => string.IsNullOrWhiteSpace(opt.Value.Directory) ? Path.Combine(Path.GetFullPath(storage.Value.Root), "..", "backups") : opt.Value.Directory;

    public async Task<object> RunAsync(CurrentUser u)
    {
        var dir = Path.GetFullPath(Dir);
        Directory.CreateDirectory(dir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string? dbFile = null;
        if (db.Dialect == Dialect.SqlServer)
        {
            var name = Db.Str(await db.ScalarAsync("SELECT DB_NAME()"))!;
            dbFile = Path.Combine(dir, $"DataKavach_{stamp}.bak");
            await db.ExecAsync($"BACKUP DATABASE [{name.Replace("]", "]]")}] TO DISK = ? WITH INIT, CHECKSUM, NAME = ?", dbFile, $"Data Kavach backup {stamp}");
        }
        var zip = Path.Combine(dir, $"DataKavach_files_{stamp}.zip");
        var root = Path.GetFullPath(storage.Value.Root);
        await Task.Run(() => ZipFile.CreateFromDirectory(root, zip, CompressionLevel.Optimal, false));
        await audit.LogAsync("BACKUP", "system", null, null, new { dbFile, zip }, u);
        return new { database = dbFile, files = zip, note = db.Dialect == Dialect.SqlServer ? "Database backup is written by the SQL Server service account; the folder must be writable by it." : "SQLite mode: copy the database file separately." };
    }

    public IEnumerable<object> List()
    {
        var dir = Path.GetFullPath(Dir);
        if (!Directory.Exists(dir)) return Array.Empty<object>();
        return new DirectoryInfo(dir).GetFiles().Where(f => f.Name.StartsWith("DataKavach")).OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new { name = f.Name, size = f.Length, created = f.CreationTimeUtc });
    }
}

public static class AdminEndpoints
{
    static object ShapeUser(Dictionary<string, object?> u, Dictionary<long, string> orgNames) => new
    {
        id = Db.Long(u["Id"]), email = Db.Str(u["Email"]), name = Db.Str(u["FullName"]), role = Db.Str(u["Role"]), department = Db.Str(u["Department"]), phone = Db.Str(u["Phone"]),
        isActive = Db.Bool(u["IsActive"]), mfaEnabled = Db.Bool(u["MfaEnabled"]), lastLoginAt = Db.Dt(u["LastLoginAt"]), lockedUntil = Db.Dt(u["LockoutUntil"]), createdAt = Db.Dt(u["CreatedAt"]),
        userType = Db.Str(u.GetValueOrDefault("UserType")) ?? "Provider", organizationId = Db.Long(u.GetValueOrDefault("OrganizationId")),
        organizationName = orgNames.GetValueOrDefault(Db.Long(u.GetValueOrDefault("OrganizationId")))
    };

    /// <summary>Loads a target user and checks the caller may manage it (customers: only users of their own organisation).</summary>
    static async Task<Dictionary<string, object?>> ManagedUser(Db db, CurrentUser caller, long id)
    {
        var t = await db.FirstAsync($"SELECT * FROM {db.Q("Users")} WHERE {db.Q("Id")} = ? AND {db.Q("IsDeleted")} = 0", id) ?? throw ApiError.NotFound("User");
        if (caller.IsCustomer && (Db.Str(t["UserType"]) != "Customer" || Db.Long(t["OrganizationId"]) != caller.OrgId)) throw new ApiError(403, "You can only manage users of your own organisation.");
        if (Db.Str(t["Role"]) == Roles.SuperAdmin && caller.Role != Roles.SuperAdmin) throw ApiError.Forbidden("Super Administrator changes");
        return t;
    }

    /// <summary>Validates role/organisation combination: customer roles need a client organisation, provider roles must not have one.</summary>
    static async Task<(string type, long? org)> ResolveType(Db db, CurrentUser caller, string role, long? orgId)
    {
        if (!Roles.All.Contains(role)) throw ApiError.Bad("Unknown role");
        if (caller.IsCustomer)
        {
            if (!Roles.IsCustomerRole(role)) throw new ApiError(403, "Customer administrators can assign customer roles only.");
            return ("Customer", caller.OrgId);
        }
        if (role == Roles.SuperAdmin && caller.Role != Roles.SuperAdmin) throw ApiError.Forbidden("Only a Super Administrator can create another Super Administrator");
        if (!Roles.IsCustomerRole(role)) return ("Provider", null);
        if (orgId is null or <= 0) throw ApiError.Bad("Select the client organisation for a customer portal user.");
        if (await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Organizations")} WHERE {db.Q("Id")} = ? AND {db.Q("IsDeleted")} = 0", orgId) == 0) throw ApiError.NotFound("Organisation");
        return ("Customer", orgId);
    }

    static async Task<Dictionary<long, string>> OrgNames(Db db) =>
        (await db.QueryAsync($"SELECT {db.Q("Id")}, {db.Q("Name")} FROM {db.Q("Organizations")}")).ToDictionary(r => Db.Long(r["Id"]), r => Db.Str(r["Name"]) ?? "");

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireAuthorization();

        // ---------------- users (provider staff + customer portal users)
        g.MapGet("/users", async (HttpContext ctx, Db db, EntityService es) =>
        {
            var cu = ctx.U();
            await es.Require(cu, "admin.users");
            var w = $"{db.Q("IsDeleted")} = 0"; var args = new List<object?>();
            if (cu.IsCustomer) { w += $" AND {db.Q("UserType")} = 'Customer' AND {db.Q("OrganizationId")} = ?"; args.Add(cu.OrgId); }
            else if (ctx.QLong("orgId") is { } fo) { w += $" AND {db.Q("OrganizationId")} = ?"; args.Add(fo); }
            var names = await OrgNames(db);
            return Results.Ok((await db.QueryAsync($"SELECT * FROM {db.Q("Users")} WHERE {w} ORDER BY {db.Q("FullName")}", args.ToArray())).Select(u => ShapeUser(u, names)));
        });
        g.MapGet("/users/directory", async (HttpContext ctx, Db db) =>
        {
            var cu = ctx.U();
            var w = $"{db.Q("IsDeleted")} = 0 AND {db.Q("IsActive")} = 1"; var args = new List<object?>();
            if (cu.IsCustomer) { w += $" AND {db.Q("OrganizationId")} = ?"; args.Add(cu.OrgId); }
            return Results.Ok((await db.QueryAsync($"SELECT {db.Q("Id")}, {db.Q("FullName")}, {db.Q("Email")}, {db.Q("Department")} FROM {db.Q("Users")} WHERE {w} ORDER BY {db.Q("FullName")}", args.ToArray()))
                .Select(u => new { id = Db.Long(u["Id"]), name = Db.Str(u["FullName"]), email = Db.Str(u["Email"]), department = Db.Str(u["Department"]) }));
        });
        g.MapPost("/users", async (HttpContext ctx, Db db, EntityService es, SeedService seed, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.users");
            var b = await ctx.Body();
            var role = b.Str("role") ?? (u.IsCustomer ? Roles.CustomerViewer : "Read-only User");
            var (type, org) = await ResolveType(db, u, role, b.Long("organizationId"));
            var id = await seed.CreateUserAsync(b.Str("email") ?? "", b.Str("name") ?? "", b.Str("password") ?? "", role, mustChange: true, userType: type, orgId: org);
            await db.UpdateAsync("Users", id, new Dictionary<string, object?> { ["Department"] = b.Str("department"), ["Phone"] = b.Str("phone"), ["CreatedBy"] = u.Email });
            await audit.LogAsync("CREATE", "users", id, org ?? 0, new { email = b.Str("email"), role, type }, u);
            return Results.Ok(new { id });
        });
        g.MapPut("/users/{id:long}", async (long id, HttpContext ctx, Db db, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.users");
            var b = await ctx.Body();
            var target = await ManagedUser(db, u, id);
            var role = b.Str("role") ?? Db.Str(target["Role"])!;
            var (type, org) = await ResolveType(db, u, role, b.Long("organizationId") ?? (Db.Long(target["OrganizationId"]) is var to and > 0 ? to : null));
            if (id == u.Id && (!b.Bool("isActive") || role != u.Role)) throw ApiError.Bad("You cannot deactivate yourself or change your own role.");
            var vals = new Dictionary<string, object?> { ["FullName"] = b.Str("name") ?? Db.Str(target["FullName"]), ["Role"] = role, ["UserType"] = type, ["OrganizationId"] = org, ["Department"] = b.Str("department"), ["Phone"] = b.Str("phone"), ["IsActive"] = b.Bool("isActive"), ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email };
            await db.UpdateAsync("Users", id, vals);
            await audit.LogAsync("UPDATE", "users", id, org ?? 0, new { role, type, org, active = b.Bool("isActive") }, u);
            return Results.Ok();
        });
        g.MapPost("/users/{id:long}/reset-password", async (long id, HttpContext ctx, Db db, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.users");
            await ManagedUser(db, u, id);
            var b = await ctx.Body();
            var pw = b.Str("password") ?? "";
            if (Passwords.Validate(pw) is { } err) throw ApiError.Bad(err);
            await db.UpdateAsync("Users", id, new Dictionary<string, object?> { ["PasswordHash"] = Passwords.Hash(pw), ["MustChangePassword"] = true, ["FailedAttempts"] = 0, ["LockoutUntil"] = null });
            await audit.LogAsync("PASSWORD_RESET", "users", id, u.IsCustomer ? u.OrgId : null, null, u);
            return Results.Ok();
        });
        g.MapPost("/users/{id:long}/reset-mfa", async (long id, HttpContext ctx, Db db, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.users");
            await ManagedUser(db, u, id);
            await db.UpdateAsync("Users", id, new Dictionary<string, object?> { ["MfaEnabled"] = false, ["MfaSecret"] = null });
            await audit.LogAsync("MFA_RESET", "users", id, u.IsCustomer ? u.OrgId : null, null, u);
            return Results.Ok();
        });
        g.MapDelete("/users/{id:long}", async (long id, HttpContext ctx, Db db, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.users");
            if (id == u.Id) throw ApiError.Bad("You cannot delete your own account.");
            await ManagedUser(db, u, id);
            await db.UpdateAsync("Users", id, new Dictionary<string, object?> { ["IsDeleted"] = true, ["IsActive"] = false, ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email });
            await audit.LogAsync("DELETE", "users", id, u.IsCustomer ? u.OrgId : null, null, u);
            return Results.Ok();
        });

        // ---------------- roles & permissions
        g.MapGet("/roles", async (HttpContext ctx, PermissionService perms, EntityService es) =>
        {
            await es.Require(ctx.U(), "admin.users");
            var list = new List<object>();
            foreach (var r in ctx.U().IsCustomer ? Roles.CustomerRoles : Roles.All) list.Add(new { role = r, permissions = (await perms.ForRoleAsync(r)).OrderBy(x => x) });
            return Results.Ok(new { roles = list, all = Roles.AllPermissions() });
        });
        g.MapPut("/roles/{role}", async (string role, HttpContext ctx, Db db, PermissionService perms, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.users");
            ctx.EnsureProvider();
            if (role == Roles.SuperAdmin) throw ApiError.Bad("Super Administrator always has all permissions.");
            if (!Roles.All.Contains(role)) throw ApiError.NotFound("Role");
            var list = (await JsonSerializer.DeserializeAsync<List<string>>(ctx.Request.Body) ?? new()).Where(p => Roles.AllPermissions().Contains(p)).Distinct().ToList();
            await using var c = await db.OpenAsync();
            using var tx = c.BeginTransaction();
            await db.ExecAsync(c, tx, $"DELETE FROM {db.Q("RolePermissions")} WHERE {db.Q("Role")} = ?", role);
            foreach (var p in list) await db.InsertAsync("RolePermissions", new Dictionary<string, object?> { ["Role"] = role, ["Permission"] = p, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email }, c, tx);
            tx.Commit();
            perms.Invalidate();
            await audit.LogAsync("UPDATE", "rolePermissions", null, null, new { role, permissions = list.Count }, u);
            return Results.Ok();
        });

        // ---------------- audit
        g.MapGet("/audit", async (HttpContext ctx, Db db, EntityService es) =>
        {
            await es.Require(ctx.U(), "admin.audit");
            var where = new List<string> { "1=1" }; var args = new List<object?>();
            var cu = ctx.U();
            if (cu.IsCustomer) { where.Add($"{db.Q("OrganizationId")} = ?"); args.Add(cu.OrgId); }
            else if (ctx.QLong("orgId") is { } o) { where.Add($"{db.Q("OrganizationId")} IN (0, ?)"); args.Add(o); }
            if (ctx.Q("entity") is { Length: > 0 } e) { where.Add($"{db.Q("Entity")} = ?"); args.Add(e); }
            if (ctx.QLong("entityId") is { } eid) { where.Add($"{db.Q("EntityId")} = ?"); args.Add(eid); }
            if (ctx.Q("action") is { Length: > 0 } a) { where.Add($"{db.Q("Action")} = ?"); args.Add(a); }
            if (ctx.Q("user") is { Length: > 0 } us) { where.Add(db.Like(db.Q("UserEmail"))); args.Add($"%{us}%"); }
            var w = string.Join(" AND ", where);
            var page = int.TryParse(ctx.Q("page"), out var p) ? Math.Max(1, p) : 1;
            var total = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("AuditLogs")} WHERE {w}", args.ToArray());
            var rows = await db.QueryAsync(db.Page($"SELECT * FROM {db.Q("AuditLogs")} WHERE {w} ORDER BY {db.Q("Id")} DESC", (page - 1) * 100, 100), args.ToArray());
            return Results.Ok(new { total, rows = rows.Select(r => Json.Shape(r, null)) });
        });
        g.MapGet("/audit/verify", async (HttpContext ctx, AuditService audit, EntityService es) =>
        {
            await es.Require(ctx.U(), "admin.audit");
            ctx.EnsureProvider();
            var (ok, n, broken) = await audit.VerifyChainAsync();
            return Results.Ok(new { intact = ok, checkedEntries = n, firstBrokenEntry = broken });
        });

        // ---------------- settings
        g.MapGet("/settings", async (HttpContext ctx, SettingsService s, EntityService es) =>
        {
            await es.Require(ctx.U(), "admin.settings");
            var org = ctx.OrgIdOrZero();
            return Results.Ok(new { rightsResponseDays = await s.GetAsync("rights.responseDays", org) ?? "30" });
        });
        g.MapPut("/settings", async (HttpContext ctx, SettingsService s, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.settings");
            var b = await ctx.Body();
            if (b.Long("rightsResponseDays") is { } d) { if (d < 1 || d > 90) throw ApiError.Bad("Response period must be 1-90 days"); await s.SetAsync("rights.responseDays", d.ToString(), ctx.OrgIdOrZero()); }
            await audit.LogAsync("UPDATE", "settings", null, ctx.OrgIdOrZero(), b.GetRawText(), u);
            return Results.Ok();
        });

        // ---------------- integration API keys
        g.MapGet("/apikeys", async (HttpContext ctx, Db db, EntityService es) =>
        {
            await es.Require(ctx.U(), "admin.apikeys");
            var rows = await db.QueryAsync($"SELECT * FROM {db.Q("ApiKeys")} WHERE {db.Q("OrganizationId")} = ? ORDER BY {db.Q("Id")} DESC", ctx.OrgId());
            return Results.Ok(rows.Select(r => { var s = Json.Shape(r, Schema.Get("apiKeys")); s.Remove("keyHash"); return s; }));
        });
        g.MapPost("/apikeys", async (HttpContext ctx, Db db, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.apikeys");
            var b = await ctx.Body();
            var raw = "dk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var id = await db.InsertAsync("ApiKeys", new Dictionary<string, object?> { ["OrganizationId"] = ctx.OrgId(), ["Name"] = b.Str("name") ?? "Integration", ["Prefix"] = raw[..10], ["KeyHash"] = Hashing.Sha256Hex(raw), ["IsActive"] = true, ["Scopes"] = "consent,rights", ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email });
            await audit.LogAsync("CREATE", "apiKeys", id, ctx.OrgId(), new { name = b.Str("name") }, u);
            return Results.Ok(new { id, key = raw, note = "Copy this key now - it is not shown again." });
        });
        g.MapDelete("/apikeys/{id:long}", async (long id, HttpContext ctx, Db db, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "admin.apikeys");
            ctx.EnsureOrg(Db.Long(await db.ScalarAsync($"SELECT {db.Q("OrganizationId")} FROM {db.Q("ApiKeys")} WHERE {db.Q("Id")} = ?", id)));
            await db.UpdateAsync("ApiKeys", id, new Dictionary<string, object?> { ["IsActive"] = false, ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email });
            await audit.LogAsync("REVOKE", "apiKeys", id, null, null, u);
            return Results.Ok();
        });

        // ---------------- backup
        g.MapPost("/admin/backup", async (HttpContext ctx, BackupService b, EntityService es) =>
        {
            await es.Require(ctx.U(), "admin.backup");
            ctx.EnsureProvider();
            return Results.Ok(await b.RunAsync(ctx.U()));
        });
        g.MapGet("/admin/backups", async (HttpContext ctx, BackupService b, EntityService es) =>
        {
            await es.Require(ctx.U(), "admin.backup");
            ctx.EnsureProvider();
            return Results.Ok(new { directory = Path.GetFullPath(b.Dir), files = b.List() });
        });
    }
}
