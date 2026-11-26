using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Services;

namespace DataKavach.Endpoints;

/// <summary>
/// External integration API (consent capture/withdrawal and rights-request intake) authenticated with an organisation API key
/// sent in the X-Api-Key header. Intended for websites, mobile apps and CRMs.
/// </summary>
public static class IntegrationEndpoints
{
    static async Task<(long orgId, CurrentUser user)> Key(HttpContext ctx, Db db)
    {
        var raw = ctx.Request.Headers["X-Api-Key"].ToString();
        if (raw.Length < 20) throw new ApiError(401, "Missing or invalid X-Api-Key");
        var row = await db.FirstAsync($"SELECT * FROM {db.Q("ApiKeys")} WHERE {db.Q("KeyHash")} = ? AND {db.Q("IsActive")} = 1", Hashing.Sha256Hex(raw)) ?? throw new ApiError(401, "Invalid API key");
        await db.UpdateAsync("ApiKeys", Db.Long(row["Id"]), new Dictionary<string, object?> { ["LastUsedAt"] = DateTime.UtcNow });
        return (Db.Long(row["OrganizationId"]), new CurrentUser(0, $"api:{Db.Str(row["Name"])}", Db.Str(row["Name"]) ?? "API", "API"));
    }

    public static void MapIntegrationEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireRateLimiting("api");

        g.MapPost("/consents", async (HttpContext ctx, Db db, EntityService es) =>
        {
            var (orgId, u) = await Key(ctx, db);
            var b = await ctx.Body();
            var e = Schema.Get("consentRecords");
            var vals = es.ReadBody(e, b);
            vals["Channel"] = vals.GetValueOrDefault("Channel") ?? "API";
            vals["SourceIp"] = b.Str("sourceIp") ?? ctx.Connection.RemoteIpAddress?.ToString();
            if (vals.GetValueOrDefault("NoticeId") is long nid && await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("ConsentNotices")} WHERE {db.Q("Id")} = ? AND {db.Q("OrganizationId")} = ?", nid, orgId) == 0)
                throw ApiError.Bad("noticeId does not belong to this organisation");
            var id = await es.CreateAsync(e, u, orgId, vals, system: true);
            var row = await es.GetRawAsync(e, id);
            return Results.Ok(new { id, status = Db.Str(row["Status"]), givenAt = Db.Dt(row["GivenAt"]), noticeVersion = Db.Str(row["NoticeVersion"]) });
        });

        g.MapPost("/consents/withdraw", async (HttpContext ctx, Db db, EntityService es) =>
        {
            var (orgId, u) = await Key(ctx, db);
            var b = await ctx.Body();
            var principal = b.Str("principalRef") ?? throw ApiError.Bad("principalRef is required");
            var purpose = b.Str("purpose");
            var rows = await es.AllAsync("consentRecords", orgId, $"{db.Q("PrincipalRef")} = ? AND {db.Q("Status")} = 'Given'", principal);
            if (purpose != null) rows = rows.Where(r => Db.Str(r["Purpose"]) == purpose).ToList();
            foreach (var r in rows) await es.UpdateAsync(Schema.Get("consentRecords"), u, Db.Long(r["Id"]), new Dictionary<string, object?> { ["Status"] = "Withdrawn" }, system: true);
            return Results.Ok(new { withdrawn = rows.Count });
        });

        g.MapGet("/consents", async (HttpContext ctx, Db db, EntityService es) =>
        {
            var (orgId, _) = await Key(ctx, db);
            var principal = ctx.Q("principalRef") ?? throw ApiError.Bad("principalRef is required");
            var rows = await es.AllAsync("consentRecords", orgId, $"{db.Q("PrincipalRef")} = ?", principal);
            return Results.Ok(rows.Select(r => new { id = Db.Long(r["Id"]), purpose = Db.Str(r["Purpose"]), status = Db.Str(r["Status"]), noticeVersion = Db.Str(r["NoticeVersion"]), givenAt = Db.Dt(r["GivenAt"]), withdrawnAt = Db.Dt(r["WithdrawnAt"]) }));
        });

        g.MapPost("/rights-requests", async (HttpContext ctx, Db db, EntityService es) =>
        {
            var (orgId, u) = await Key(ctx, db);
            var b = await ctx.Body();
            var e = Schema.Get("rightsRequests");
            var vals = es.ReadBody(e, b);
            vals["Channel"] = "API";
            vals["ReceivedAt"] = DateTime.UtcNow;
            var id = await es.CreateAsync(e, u, orgId, vals, system: true);
            var row = await es.GetRawAsync(e, id);
            return Results.Ok(new { reference = Db.Str(row["Reference"]), dueAt = Db.Dt(row["DueAt"]) });
        });
    }
}
