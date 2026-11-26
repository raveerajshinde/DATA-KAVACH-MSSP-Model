using System.Globalization;
using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;

namespace DataKavach.Services;

public sealed class SaveContext
{
    public required EntityDef Def { get; init; }
    public required CurrentUser User { get; init; }
    public long? Id { get; init; }
    public long OrgId { get; init; }
    public Dictionary<string, object?>? Existing { get; init; }
    public required Dictionary<string, object?> Values { get; init; }   // column -> typed value (pending write)
    public T? Get<T>(string col) => Values.TryGetValue(col, out var v) ? (T?)v : Existing != null && Existing.TryGetValue(col, out var e) && e != null ? (T?)Convert.ChangeType(e, typeof(T), CultureInfo.InvariantCulture) : default;
    public object? Raw(string col) => Values.TryGetValue(col, out var v) ? v : Existing?.GetValueOrDefault(col);
}

public interface IEntityHooks
{
    string EntityKey { get; }
    Task BeforeSaveAsync(SaveContext ctx) => Task.CompletedTask;
    Task AfterSaveAsync(SaveContext ctx, long id) => Task.CompletedTask;
}

public sealed record ListQuery(long? OrgId, long? ParentId, string? Q, int Page, int PageSize, string? Sort, bool Desc, Dictionary<string, string>? Filters);

public sealed class EntityService(Db db, AuditService audit, PermissionService perms, IEnumerable<IEntityHooks> hooks, IHttpContextAccessor http)
{
    readonly Dictionary<string, IEntityHooks> _hooks = hooks.ToDictionary(h => h.EntityKey, StringComparer.OrdinalIgnoreCase);

    public async Task Require(CurrentUser? u, string perm)
    {
        if (u == null) throw new ApiError(401, "Not authenticated");
        if (!await perms.HasAsync(u, perm)) throw ApiError.Forbidden(perm);
    }

    /// <summary>The signed-in user for this request (null for integration-API / system calls).</summary>
    public CurrentUser? Caller => CurrentUser.From(http.HttpContext?.User);

    /// <summary>Customer portal users may only touch their own organisation. Provider users may access every client.</summary>
    public void EnsureOrg(long orgId, CurrentUser? u = null)
    {
        u ??= Caller;
        if (u != null && u.IsCustomer && orgId != u.OrgId) throw new ApiError(403, "You do not have access to this organisation.");
    }

    public void EnsureProvider(CurrentUser? u = null)
    {
        u ??= Caller;
        if (u != null && u.IsCustomer) throw new ApiError(403, "This function is available to the service provider only.");
    }

    void EnsureRowOrg(EntityDef e, Dictionary<string, object?> row)
    {
        if (e.OrgScoped) EnsureOrg(Db.Long(row["OrganizationId"]));
        else if (e.Key == "organizations") EnsureOrg(Db.Long(row["Id"]));
        else if (e.Key is "reportTemplates" or "files" && Db.Long(row.GetValueOrDefault("OrganizationId")) is var o and > 0) EnsureOrg(o);
    }

    string NotDeleted(EntityDef e) => e.SoftDelete ? $" AND {db.Q("IsDeleted")} = 0" : "";

    public async Task<(List<Dictionary<string, object?>> rows, long total)> ListAsync(EntityDef e, CurrentUser u, ListQuery q)
    {
        await Require(u, $"{e.Module}.view");
        var where = new List<string> { "1=1" };
        var args = new List<object?>();
        if (e.OrgScoped)
        {
            if (q.OrgId is null) throw ApiError.Bad("orgId is required");
            EnsureOrg(q.OrgId.Value, u);
            where.Add($"{db.Q("OrganizationId")} = ?"); args.Add(q.OrgId.Value);
        }
        else if (e.Key == "organizations" && u.IsCustomer) { where.Add($"{db.Q("Id")} = ?"); args.Add(u.OrgId); }
        if (e.ParentCol != null && q.ParentId != null) { where.Add($"{db.Q(e.ParentCol)} = ?"); args.Add(q.ParentId.Value); }
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var cols = e.SearchCols.Length > 0 ? e.SearchCols : e.ListCols.Where(c => e.Find(c)?.Type is ColType.Str).Take(3).ToArray();
            if (cols.Length > 0)
            {
                where.Add("(" + string.Join(" OR ", cols.Select(c => db.Like(db.Q(c)))) + ")");
                foreach (var _ in cols) args.Add($"%{q.Q.Trim()}%");
            }
        }
        if (q.Filters != null)
            foreach (var (k, v) in q.Filters)
            {
                var col = e.Find(k);
                if (col == null || string.IsNullOrEmpty(v)) continue;
                where.Add($"{db.Q(col.Name)} = ?"); args.Add(Coerce(col, v));
            }
        var w = string.Join(" AND ", where) + NotDeleted(e);
        var total = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q(e.Table)} WHERE {w}", args.ToArray());
        var sortCol = q.Sort != null && e.Find(q.Sort) is { } sc ? sc.Name : (q.Sort is "createdAt" or "updatedAt" or "id" ? Json.PascalKey(q.Sort) : "Id");
        var cols2 = string.Join(", ", new[] { "Id" }.Concat(DdlGenerator.StandardCols(e).Where(c => c.Type != ColType.Text || e.ListCols.Contains(c.Name)).Select(c => c.Name)).Distinct().Select(db.Q));
        var sql = db.Page($"SELECT {cols2} FROM {db.Q(e.Table)} WHERE {w} ORDER BY {db.Q(sortCol)} {(q.Desc || q.Sort == null ? "DESC" : "ASC")}", (q.Page - 1) * q.PageSize, q.PageSize);
        var rows = await db.QueryAsync(sql, args.ToArray());
        var canPii = await perms.HasAsync(u, "pii.view");
        return (rows.Select(r => Mask(e, Json.Shape(r, e), canPii)).ToList(), total);
    }

    public async Task<Dictionary<string, object?>> GetRawAsync(EntityDef e, long id)
    {
        var r = await db.FirstAsync($"SELECT * FROM {db.Q(e.Table)} WHERE {db.Q("Id")} = ?{NotDeleted(e)}", id) ?? throw ApiError.NotFound(e.Label);
        EnsureRowOrg(e, r);
        return r;
    }

    public async Task<Dictionary<string, object?>> GetAsync(EntityDef e, CurrentUser u, long id)
    {
        await Require(u, $"{e.Module}.view");
        var r = await GetRawAsync(e, id);
        return Mask(e, Json.Shape(r, e), await perms.HasAsync(u, "pii.view"));
    }

    static Dictionary<string, object?> Mask(EntityDef e, Dictionary<string, object?> row, bool canPii)
    {
        if (canPii) return row;
        foreach (var c in e.Cols.Where(c => c.Sensitive))
        {
            var k = Json.CamelKey(c.Name);
            if (row.TryGetValue(k, out var v) && v is string s && s.Length > 0)
                row[k] = s.Length <= 3 ? "***" : s[..2] + new string('*', Math.Min(8, s.Length - 3)) + s[^1];
        }
        return row;
    }

    public static object? Coerce(Col c, object? raw)
    {
        if (raw is JsonElement je)
        {
            raw = je.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => je.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => je.GetRawText(),
                JsonValueKind.Array => string.Join("; ", je.EnumerateArray().Select(x => x.ToString())),
                _ => je.GetRawText()
            };
        }
        if (raw is string str && str.Length == 0 && c.Type != ColType.Str && c.Type != ColType.Text) raw = null;
        if (raw == null) return c.Type == ColType.Bool ? false : null;
        try
        {
            return c.Type switch
            {
                ColType.Str => Trunc(Convert.ToString(raw, CultureInfo.InvariantCulture)!, c),
                ColType.Text => Convert.ToString(raw, CultureInfo.InvariantCulture),
                ColType.Int => Convert.ToInt32(Convert.ToDecimal(raw, CultureInfo.InvariantCulture)),
                ColType.Long => Convert.ToInt64(Convert.ToDecimal(raw, CultureInfo.InvariantCulture)),
                ColType.Dec => Convert.ToDecimal(raw, CultureInfo.InvariantCulture),
                ColType.Bool => raw is bool b ? b : Db.Bool(raw),
                ColType.Date => DateOnly.FromDateTime(Db.Dt(raw) ?? throw new FormatException()),
                ColType.DateTime => (Db.Dt(raw) ?? throw new FormatException()).ToUniversalTime(),
                _ => raw
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw ApiError.Bad($"Invalid value for {c.DisplayLabel}");
        }
    }

    static string Trunc(string s, Col c)
    {
        var max = c.Len <= 0 ? 200 : c.Len;
        if (s.Length > max) throw ApiError.Bad($"{c.DisplayLabel} must be at most {max} characters");
        if (c.Options != null && !c.Multi && s.Length > 0 && !c.Options.Contains(s)) throw ApiError.Bad($"{c.DisplayLabel}: '{s}' is not an allowed value");
        return s;
    }

    public Dictionary<string, object?> ReadBody(EntityDef e, JsonElement body, bool includeReadOnly = false)
    {
        var vals = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (body.ValueKind != JsonValueKind.Object) throw ApiError.Bad("JSON object expected");
        foreach (var p in body.EnumerateObject())
        {
            var c = e.Find(p.Name);
            if (c == null) continue;
            if (!includeReadOnly && (c.ReadOnly || (c.Hidden && c.Name != e.ParentCol))) continue;
            vals[c.Name] = Coerce(c, p.Value);
        }
        return vals;
    }

    public async Task<long> CreateAsync(EntityDef e, CurrentUser u, long orgId, Dictionary<string, object?> vals, bool system = false)
    {
        if (!system) await Require(u, $"{e.Module}.edit");
        if (e.OrgScoped) EnsureOrg(orgId, u);
        else if (!system && u.IsCustomer) throw new ApiError(403, "Customer accounts cannot create shared or organisation records.");
        foreach (var c in e.Cols.Where(c => c.Required && !c.ReadOnly))
            if (!vals.TryGetValue(c.Name, out var v) || v == null || (v is string s && s.Trim().Length == 0))
                throw ApiError.Bad($"{c.DisplayLabel} is required");
        if (e.ParentCol != null && vals.TryGetValue(e.ParentCol, out var pid) && pid != null)
        {
            var parent = Schema.Get(e.ParentKey!);
            var prow = await GetRawAsync(parent, Db.Long(pid));
            if (parent.OrgScoped && Db.Long(prow["OrganizationId"]) != orgId) throw ApiError.Bad("Parent record belongs to another organization");
        }
        var ctx = new SaveContext { Def = e, User = u, OrgId = orgId, Values = vals };
        if (_hooks.TryGetValue(e.Key, out var h)) await h.BeforeSaveAsync(ctx);
        var row = new Dictionary<string, object?>(vals);
        if (e.OrgScoped) row["OrganizationId"] = orgId;
        row["CreatedAt"] = DateTime.UtcNow; row["CreatedBy"] = u.Email;
        if (e.SoftDelete) row["IsDeleted"] = false;
        var id = await db.InsertAsync(e.Table, row);
        if (h != null) await h.AfterSaveAsync(ctx, id);
        await audit.LogAsync("CREATE", e.Key, id, e.OrgScoped ? orgId : null, Summarize(e, vals), u);
        return id;
    }

    public async Task UpdateAsync(EntityDef e, CurrentUser u, long id, Dictionary<string, object?> vals, bool system = false, string action = "UPDATE")
    {
        if (!system) await Require(u, $"{e.Module}.edit");
        if (!system && u.IsCustomer && !e.OrgScoped && e.Key != "organizations") throw new ApiError(403, "Customer accounts cannot change shared records.");
        var existing = await GetRawAsync(e, id);
        var orgId = e.OrgScoped ? Db.Long(existing["OrganizationId"]) : 0;
        var ctx = new SaveContext { Def = e, User = u, Id = id, OrgId = orgId, Existing = existing, Values = vals };
        if (_hooks.TryGetValue(e.Key, out var h)) await h.BeforeSaveAsync(ctx);
        var changes = new Dictionary<string, object?>();
        foreach (var (k, v) in vals)
        {
            var old = existing.GetValueOrDefault(k);
            if (!SameValue(old, v)) changes[k] = new { from = Db.Str(old), to = v is DateOnly d ? d.ToString("yyyy-MM-dd") : Db.Str(v) };
        }
        if (changes.Count == 0 && action == "UPDATE") return;
        var row = new Dictionary<string, object?>(vals) { ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email };
        await db.UpdateAsync(e.Table, id, row);
        if (h != null) await h.AfterSaveAsync(ctx, id);
        await audit.LogAsync(action, e.Key, id, e.OrgScoped ? orgId : null, changes.Count > 0 ? changes : null, u);
    }

    static bool SameValue(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return (a ?? b) is string s && s.Length == 0 || (a ?? b) is false;
        if (b is DateOnly d) return Db.Dt(a)?.Date == d.ToDateTime(TimeOnly.MinValue);
        if (b is DateTime dt) return Db.Dt(a) is { } x && Math.Abs((x - dt).TotalSeconds) < 1;
        if (b is bool bb) return Db.Bool(a) == bb;
        return string.Equals(Db.Str(a), Db.Str(b), StringComparison.Ordinal);
    }

    public async Task DeleteAsync(EntityDef e, CurrentUser u, long id)
    {
        await Require(u, $"{e.Module}.delete");
        if (u.IsCustomer && !e.OrgScoped) throw new ApiError(403, "Customer accounts cannot delete shared or organisation records.");
        var existing = await GetRawAsync(e, id);
        if (e.SoftDelete) await db.UpdateAsync(e.Table, id, new Dictionary<string, object?> { ["IsDeleted"] = true, ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email });
        else await db.ExecAsync($"DELETE FROM {db.Q(e.Table)} WHERE {db.Q("Id")} = ?", id);
        await audit.LogAsync("DELETE", e.Key, id, e.OrgScoped ? Db.Long(existing["OrganizationId"]) : null, new { title = Db.Str(existing.GetValueOrDefault(e.TitleCol)) }, u);
    }

    static object Summarize(EntityDef e, Dictionary<string, object?> vals) =>
        vals.Where(kv => kv.Value != null && e.Find(kv.Key)?.Type != ColType.Text && e.Find(kv.Key)?.Sensitive != true)
            .Take(12).ToDictionary(kv => kv.Key, kv => kv.Value is DateOnly d ? d.ToString("yyyy-MM-dd") : Db.Str(kv.Value));

    /// <summary>Loads all non-deleted rows of an org-scoped entity (used by reports/dashboards).</summary>
    public async Task<List<Dictionary<string, object?>>> AllAsync(string key, long orgId, string? where = null, params object?[] args)
    {
        var e = Schema.Get(key);
        if (e.OrgScoped) EnsureOrg(orgId);
        var w = e.OrgScoped ? $"{db.Q("OrganizationId")} = ?" : "1=1";
        var a = new List<object?>();
        if (e.OrgScoped) a.Add(orgId);
        if (where != null) { w += " AND " + where; a.AddRange(args); }
        return await db.QueryAsync($"SELECT * FROM {db.Q(e.Table)} WHERE {w}{NotDeleted(e)} ORDER BY {db.Q("Id")}", a.ToArray());
    }
}
