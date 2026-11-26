using System.Globalization;
using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;

namespace DataKavach.Services;

public sealed class ApiError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
    public static ApiError NotFound(string what = "Record") => new(404, $"{what} not found");
    public static ApiError Forbidden(string perm) => new(403, $"You do not have permission ({perm})");
    public static ApiError Bad(string msg) => new(400, msg);
}

public sealed class StorageOptions
{
    public string Root { get; set; } = "storage";
    public long MaxUploadBytes { get; set; } = 50L * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".png", ".jpg", ".jpeg", ".gif", ".txt", ".pptx", ".msg", ".eml", ".json", ".xml", ".log", ".zip" };
}

/// <summary>Hash-chained audit trail: each entry stores SHA-256(previous hash + entry), making tampering detectable.</summary>
public sealed class AuditService(Db db, IHttpContextAccessor http)
{
    static readonly SemaphoreSlim Lock = new(1, 1);

    public async Task LogAsync(string action, string entity, long? entityId, long? orgId, object? details = null, CurrentUser? user = null)
    {
        var ctx = http.HttpContext;
        user ??= ctx != null ? CurrentUser.From(ctx.User) : null;
        var ip = ctx?.Connection.RemoteIpAddress?.ToString();
        var det = details switch { null => null, string s => s, _ => JsonSerializer.Serialize(details) };
        if (det != null && det.Length > 20000) det = det[..20000];
        var at = DateTime.UtcNow;
        at = at.AddTicks(-(at.Ticks % TimeSpan.TicksPerMillisecond));
        await Lock.WaitAsync();
        try
        {
            var prev = Db.Str(await db.ScalarAsync(db.Page($"SELECT {db.Q("Hash")} FROM {db.Q("AuditLogs")} ORDER BY {db.Q("Id")} DESC", 0, 1))) ?? "GENESIS";
            var hash = Hashing.Sha256Hex(Canonical(prev, at, user?.Id ?? 0, user?.Email, orgId ?? 0, action, entity, entityId ?? 0, det));
            await db.InsertAsync("AuditLogs", new Dictionary<string, object?>
            {
                ["At"] = at, ["UserId"] = user?.Id ?? 0, ["UserEmail"] = user?.Email ?? "system", ["OrganizationId"] = orgId ?? 0,
                ["Action"] = action, ["Entity"] = entity, ["EntityId"] = entityId ?? 0, ["Details"] = det, ["Ip"] = ip, ["PrevHash"] = prev, ["Hash"] = hash,
                ["CreatedAt"] = at, ["CreatedBy"] = user?.Email ?? "system"
            });
        }
        finally { Lock.Release(); }
    }

    static string Canonical(string prev, DateTime at, long uid, string? email, long org, string action, string entity, long eid, string? det) =>
        string.Join("|", prev, at.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture), uid, email ?? "system", org, action, entity, eid, det ?? "");

    public async Task<(bool ok, long checkedCount, long? brokenAt)> VerifyChainAsync()
    {
        var rows = await db.QueryAsync($"SELECT * FROM {db.Q("AuditLogs")} ORDER BY {db.Q("Id")}");
        var prev = "GENESIS";
        foreach (var r in rows)
        {
            var at = Db.Dt(r["At"])!.Value;
            var h = Hashing.Sha256Hex(Canonical(prev, at, Db.Long(r["UserId"]), Db.Str(r["UserEmail"]), Db.Long(r["OrganizationId"]), Db.Str(r["Action"])!, Db.Str(r["Entity"])!, Db.Long(r["EntityId"]), Db.Str(r["Details"])));
            if (Db.Str(r["PrevHash"]) != prev || Db.Str(r["Hash"]) != h) return (false, rows.Count, Db.Long(r["Id"]));
            prev = h;
        }
        return (true, rows.Count, null);
    }
}

public sealed class SettingsService(Db db)
{
    public async Task<string?> GetAsync(string key, long orgId = 0)
    {
        var v = await db.ScalarAsync($"SELECT {db.Q("SettingValue")} FROM {db.Q("Settings")} WHERE {db.Q("OrganizationId")} = ? AND {db.Q("SettingKey")} = ?", orgId, key);
        if (v == null && orgId != 0) return await GetAsync(key, 0);
        return Db.Str(v);
    }

    public async Task<T> GetJsonAsync<T>(string key, T fallback, long orgId = 0)
    {
        var s = await GetAsync(key, orgId);
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        try { return JsonSerializer.Deserialize<T>(s, Json.Options) ?? fallback; } catch { return fallback; }
    }

    public async Task SetAsync(string key, string value, long orgId = 0)
    {
        var n = await db.ExecAsync($"UPDATE {db.Q("Settings")} SET {db.Q("SettingValue")} = ?, {db.Q("UpdatedAt")} = ? WHERE {db.Q("OrganizationId")} = ? AND {db.Q("SettingKey")} = ?", value, DateTime.UtcNow, orgId, key);
        if (n == 0) await db.InsertAsync("Settings", new Dictionary<string, object?> { ["OrganizationId"] = orgId, ["SettingKey"] = key, ["SettingValue"] = value, ["CreatedAt"] = DateTime.UtcNow });
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>Converts a DB row into a JSON-friendly dictionary using the entity's column types.</summary>
    public static Dictionary<string, object?> Shape(Dictionary<string, object?> row, EntityDef? e)
    {
        var o = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in row)
        {
            var col = e?.Find(k);
            var type = col?.Type ?? k switch
            {
                "CreatedAt" or "UpdatedAt" => ColType.DateTime,
                "IsDeleted" => ColType.Bool,
                "Id" or "OrganizationId" => ColType.Long,
                _ => (ColType?)null
            };
            o[CamelKey(k)] = type switch
            {
                _ when v == null => null,
                ColType.Bool => Db.Bool(v),
                ColType.Int => Db.Int(v),
                ColType.Long => Db.Long(v),
                ColType.Date => Db.Dt(v)?.ToString("yyyy-MM-dd"),
                ColType.DateTime => Db.Dt(v) is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ") : null,
                ColType.Dec => Convert.ToDecimal(v, CultureInfo.InvariantCulture),
                _ => v is DateTime dt ? dt.ToString("o") : v
            };
        }
        return o;
    }

    public static string CamelKey(string k) => string.IsNullOrEmpty(k) ? k : char.ToLowerInvariant(k[0]) + k[1..];
    public static string PascalKey(string k) => string.IsNullOrEmpty(k) ? k : char.ToUpperInvariant(k[0]) + k[1..];
}
