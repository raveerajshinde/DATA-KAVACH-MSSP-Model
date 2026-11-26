using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Services;

namespace DataKavach.Endpoints;

public static class DomainEndpoints
{
    public static void MapDomainEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireAuthorization();

        // ---------------- organisation branding
        g.MapPost("/organizations/{id:long}/logo", async (long id, HttpContext ctx, EntityService es, FileService files, Db db) =>
        {
            var u = ctx.U();
            await es.Require(u, "org.edit");
            await es.GetRawAsync(Schema.Get("organizations"), id);
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault() ?? throw ApiError.Bad("No file uploaded");
            var kind = ctx.Q("kind") == "consultant" ? "consultant-logo" : "logo";
            var fid = await files.StoreGenericAsync(file, id, kind, u, new[] { ".png", ".jpg", ".jpeg" });
            await es.UpdateAsync(Schema.Get("organizations"), u, id, new Dictionary<string, object?> { [kind == "logo" ? "LogoFileId" : "ConsultantLogoFileId"] = fid }, system: true);
            return Results.Ok(new { fileId = fid });
        }).DisableAntiforgery();

        g.MapDelete("/organizations/{id:long}/logo", async (long id, HttpContext ctx, EntityService es) =>
        {
            var u = ctx.U();
            await es.Require(u, "org.edit");
            await es.UpdateAsync(Schema.Get("organizations"), u, id, new Dictionary<string, object?> { [ctx.Q("kind") == "consultant" ? "ConsultantLogoFileId" : "LogoFileId"] = 0L }, system: true);
            return Results.Ok();
        });

        g.MapGet("/files/stored/{id:long}", async (long id, HttpContext ctx, Db db, FileService files, EntityService es) =>
        {
            await es.Require(ctx.U(), "org.view");
            var f = await db.FirstAsync($"SELECT * FROM {db.Q("StoredFiles")} WHERE {db.Q("Id")} = ?", id) ?? throw ApiError.NotFound("File");
            if (Db.Long(f["OrganizationId"]) > 0) ctx.EnsureOrg(Db.Long(f["OrganizationId"]));
            var abs = files.Abs(Db.Str(f["StoragePath"])!);
            if (!File.Exists(abs)) throw ApiError.NotFound("File");
            return Results.File(abs, Db.Str(f["ContentType"]) ?? "application/octet-stream", Db.Str(f["FileName"]));
        });

        // ---------------- assessments
        g.MapPost("/assessments", async (HttpContext ctx, AssessmentService a, EntityService es) =>
        {
            var e = Schema.Get("assessments");
            var id = await a.CreateAsync(ctx.U(), ctx.OrgId(), es.ReadBody(e, await ctx.Body()));
            return Results.Ok(await es.GetAsync(e, ctx.U(), id));
        });

        g.MapGet("/assessments/{id:long}/items", async (long id, HttpContext ctx, AssessmentService a, EntityService es) =>
        {
            await es.Require(ctx.U(), "assessment.view");
            var row = await es.GetRawAsync(Schema.Get("assessments"), id);
            var items = await a.ItemsAsync(id);
            var def = Schema.Get("assessmentItems");
            var shaped = items.Select(i =>
            {
                var o = Json.Shape(i, def);
                o["evidenceCount"] = Db.Int(i["EvidenceCount"]);
                o["effectiveFrom"] = Db.Dt(i["EffectiveFrom"])?.ToString("yyyy-MM-dd");
                return o;
            }).ToList();
            var counts = StatusCounts.From(items);
            return Results.Ok(new { assessment = Json.Shape(row, Schema.Get("assessments")), items = shaped, counts = new { counts.Compliant, counts.Partial, counts.NonCompliant, counts.NotAssessed, counts.NotApplicable, counts.Indicator, counts.Completion } });
        });

        g.MapGet("/dashboard", async (HttpContext ctx, DashboardService d, EntityService es) =>
        {
            await es.Require(ctx.U(), "assessment.view");
            return Results.Ok(await d.BuildAsync(ctx.OrgId(), ctx.QLong("assessmentId")));
        });

        // ---------------- roadmap
        g.MapPost("/roadmap/generate", async (HttpContext ctx, RoadmapService r) => Results.Ok(await r.GenerateAsync(ctx.U(), ctx.OrgId(), ctx.QLong("assessmentId"))));

        g.MapPost("/actions/{id:long}/approve", async (long id, HttpContext ctx, EntityService es) =>
        {
            var u = ctx.U();
            await es.Require(u, "roadmap.approve");
            var b = await ctx.Body();
            var decision = b.Str("decision") == "Rejected" ? "Rejected" : "Approved";
            await es.UpdateAsync(Schema.Get("actions"), u, id, new Dictionary<string, object?> { ["Approval"] = decision, ["ApprovedBy"] = $"{u.Name} ({u.Email})" }, system: true, action: "APPROVE");
            return Results.Ok();
        });

        // ---------------- DPIA approval
        g.MapPost("/dpias/{id:long}/submit", async (long id, HttpContext ctx, EntityService es) =>
        {
            await es.UpdateAsync(Schema.Get("dpias"), ctx.U(), id, new Dictionary<string, object?> { ["Status"] = "In Review" }, system: false);
            return Results.Ok();
        });
        g.MapPost("/dpias/{id:long}/approve", async (long id, HttpContext ctx, EntityService es) =>
        {
            var u = ctx.U();
            await es.Require(u, "dpia.approve");
            var b = await ctx.Body();
            var decision = b.Str("decision") == "Rejected" ? "Rejected" : "Approved";
            var vals = new Dictionary<string, object?> { ["Status"] = decision, ["ApprovedBy"] = $"{u.Name} ({u.Email})", ["ApprovedAt"] = DateTime.UtcNow };
            if (b.Str("conclusion") is { Length: > 0 } c) vals["Conclusion"] = c;
            await es.UpdateAsync(Schema.Get("dpias"), u, id, vals, system: true, action: "APPROVE");
            return Results.Ok();
        });

        // ---------------- incidents & breach notification engine
        g.MapPost("/incidents/{id:long}/approve", async (long id, HttpContext ctx, EntityService es) =>
        {
            var u = ctx.U();
            await es.Require(u, "incident.approve");
            var b = await ctx.Body();
            var col = b.Str("kind") == "legal" ? "LegalApproval" : "ManagementApproval";
            var decision = b.Str("decision") == "Rejected" ? "Rejected" : "Approved";
            await es.UpdateAsync(Schema.Get("incidents"), u, id, new Dictionary<string, object?> { [col] = decision }, system: true, action: "APPROVE");
            await es.CreateAsync(Schema.Get("incidentEvents"), u, Db.Long((await es.GetRawAsync(Schema.Get("incidents"), id))["OrganizationId"]),
                new Dictionary<string, object?> { ["IncidentId"] = id, ["EventAt"] = DateTime.UtcNow, ["EventText"] = $"{(col == "LegalApproval" ? "Legal" : "Management")} approval: {decision} by {u.Name}" }, system: true);
            return Results.Ok();
        });

        g.MapGet("/incidents/{id:long}/template/{ruleKey}", async (long id, string ruleKey, HttpContext ctx, EntityService es) =>
        {
            await es.Require(ctx.U(), "incident.view");
            var inc = await es.GetRawAsync(Schema.Get("incidents"), id);
            var org = await es.GetRawAsync(Schema.Get("organizations"), Db.Long(inc["OrganizationId"]));
            return Results.Ok(new { text = BreachService.Template(ruleKey, inc, org) });
        });

        g.MapPost("/incidents/{id:long}/sync", async (long id, HttpContext ctx, BreachService b, EntityService es) =>
        {
            await es.Require(ctx.U(), "incident.edit");
            await es.GetRawAsync(Schema.Get("incidents"), id);
            await b.SyncNotificationsAsync(id, ctx.U());
            return Results.Ok();
        });

        g.MapGet("/breach/rules", async (HttpContext ctx, BreachService b, EntityService es) =>
        {
            await es.Require(ctx.U(), "incident.view");
            return Results.Ok(await b.RulesAsync(ctx.OrgIdOrZero()));
        });

        g.MapPut("/breach/rules", async (HttpContext ctx, SettingsService s, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "incident.approve");
            var rules = await JsonSerializer.DeserializeAsync<List<NotificationRule>>(ctx.Request.Body, Json.Options) ?? new();
            if (rules.Any(r => r.Hours <= 0 || string.IsNullOrWhiteSpace(r.Key))) throw ApiError.Bad("Every rule needs a key and positive hours.");
            var det = rules.FirstOrDefault(r => r.Key == "DPB_DETAILED");
            if (det != null && det.Hours > 72) throw ApiError.Bad("The detailed report to the Board is due within 72 hours (Rule 7(2)(b)) unless the Board allows a longer period - record an extension on the incident instead.");
            await s.SetAsync("breach.notificationRules", JsonSerializer.Serialize(rules, Json.Options), ctx.OrgIdOrZero());
            await audit.LogAsync("UPDATE", "breachRules", null, ctx.OrgIdOrZero(), rules, u);
            return Results.Ok();
        });

        // ---------------- policies
        g.MapPost("/policies/generate", async (HttpContext ctx, PolicyService p, EntityService es) =>
        {
            var b = await ctx.Body();
            var id = await p.GenerateAsync(ctx.U(), ctx.OrgId(), b.Str("templateKey") ?? "");
            return Results.Ok(new { id });
        });
        g.MapPost("/policies/upload", async (HttpContext ctx, PolicyService p) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault() ?? throw ApiError.Bad("No file uploaded");
            var id = await p.UploadAsync(ctx.U(), ctx.OrgId(), file, form["title"], form["policyType"]);
            return Results.Ok(new { id });
        }).DisableAntiforgery();
        g.MapPost("/policies/{id:long}/analyze", async (long id, HttpContext ctx, PolicyService p) => Results.Ok(await p.AnalyzeAsync(ctx.U(), id)));
        g.MapPost("/policies/{id:long}/apply", async (long id, HttpContext ctx, PolicyService p) => Results.Ok(new { id = await p.ApplyApprovedAsync(ctx.U(), id) }));
        g.MapPost("/policies/{id:long}/approve", async (long id, HttpContext ctx, PolicyService p) =>
        {
            var b = await ctx.Body();
            await p.ApprovePolicyAsync(ctx.U(), id, b.Str("notes"));
            return Results.Ok();
        });
        g.MapPost("/policyRecommendations/{id:long}/decide", async (long id, HttpContext ctx, EntityService es) =>
        {
            var u = ctx.U();
            await es.Require(u, "policy.approve");
            var b = await ctx.Body();
            var status = b.Str("status") is "Approved" or "Rejected" or "Pending" ? b.Str("status") : throw ApiError.Bad("status must be Approved, Rejected or Pending");
            var vals = new Dictionary<string, object?> { ["ApprovalStatus"] = status, ["ReviewedBy"] = $"{u.Name} ({u.Email})" };
            if (b.Str("revisedClause") is { } rc) vals["RevisedClause"] = rc;
            await es.UpdateAsync(Schema.Get("policyRecommendations"), u, id, vals, system: true, action: "APPROVE");
            return Results.Ok();
        });

        // ---------------- evidence
        g.MapPost("/evidence", async (HttpContext ctx, FileService files) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault() ?? throw ApiError.Bad("No file uploaded");
            var id = await files.CreateEvidenceAsync(ctx.U(), ctx.OrgId(), form["title"].ToString(), form["description"], form["category"], form["linkedEntity"], long.TryParse(form["linkedId"], out var l) ? l : null, file, form["comment"]);
            return Results.Ok(new { id });
        }).DisableAntiforgery();
        g.MapPost("/evidence/{id:long}/versions", async (long id, HttpContext ctx, FileService files) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault() ?? throw ApiError.Bad("No file uploaded");
            return Results.Ok(new { id = await files.AddVersionAsync(ctx.U(), id, file, form["comment"]) });
        }).DisableAntiforgery();
        g.MapGet("/evidence/{id:long}/verify", async (long id, HttpContext ctx, FileService files) => Results.Ok(await files.VerifyEvidenceAsync(ctx.U(), id)));
        g.MapGet("/files/evidence/{versionId:long}", async (long versionId, HttpContext ctx, Db db, FileService files, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "evidence.view");
            var v = await db.FirstAsync($"SELECT * FROM {db.Q("EvidenceVersions")} WHERE {db.Q("Id")} = ?", versionId) ?? throw ApiError.NotFound("File");
            ctx.EnsureOrg(Db.Long(v["OrganizationId"]));
            var abs = files.Abs(Db.Str(v["StoragePath"])!);
            if (!File.Exists(abs)) throw ApiError.NotFound("File");
            await audit.LogAsync("DOWNLOAD", "evidenceVersions", versionId, Db.Long(v["OrganizationId"]), new { file = Db.Str(v["FileName"]) }, u);
            return Results.File(abs, Db.Str(v["ContentType"]) ?? "application/octet-stream", Db.Str(v["FileName"]));
        });
    }
}
