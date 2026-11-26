using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Reports;
using DataKavach.Services;

namespace DataKavach.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireAuthorization();

        g.MapGet("/reports/status", (PdfConverter pdf) => Results.Ok(new { pdfAvailable = pdf.Available, engine = pdf.Available ? "LibreOffice" : null }));

        g.MapPost("/reports", async (HttpContext ctx, ReportService rs) =>
        {
            var b = await ctx.Body();
            var rOrg = b.Long("orgId") ?? ctx.OrgId();
            ctx.EnsureOrg(rOrg);
            var req = new ReportRequest(rOrg, b.Str("reportType") ?? "", b.Long("assessmentId"), b.Date("periodFrom"), b.Date("periodTo"), b.Long("entityId"));
            return Results.Ok(new { id = await rs.CreateAsync(ctx.U(), req, b.Long("templateId")) });
        });

        g.MapGet("/reports/{id:long}", async (long id, HttpContext ctx, ReportService rs) =>
        {
            var (row, doc) = await rs.GetAsync(ctx.U(), id);
            var shaped = Json.Shape(row, Schema.Get("reports"));
            shaped.Remove("contentJson");
            return Results.Ok(new { report = shaped, content = doc });
        });

        g.MapPut("/reports/{id:long}/content", async (long id, HttpContext ctx, ReportService rs) =>
        {
            var doc = await JsonSerializer.DeserializeAsync<ReportDoc>(ctx.Request.Body, Json.Options) ?? throw ApiError.Bad("Invalid content");
            await rs.SaveContentAsync(ctx.U(), id, doc);
            return Results.Ok();
        });

        g.MapPost("/reports/{id:long}/approve", async (long id, HttpContext ctx, ReportService rs) => { await rs.ApproveAsync(ctx.U(), id); return Results.Ok(); });

        g.MapGet("/reports/{id:long}/export", async (long id, HttpContext ctx, ReportService rs) =>
        {
            var r = await rs.ExportAsync(ctx.U(), id, ctx.Q("format") == "pdf" ? "pdf" : "docx");
            return Results.File(r.Data, r.ContentType, r.FileName);
        });

        g.MapGet("/reports/{id:long}/preview.pdf", async (long id, HttpContext ctx, ReportService rs) =>
        {
            var r = await rs.ExportAsync(ctx.U(), id, "pdf");
            return Results.File(r.Data, "application/pdf");
        });

        g.MapGet("/files/report/{fileId:long}", async (long fileId, HttpContext ctx, Db db, FileService files, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "report.view");
            var f = await db.FirstAsync($"SELECT * FROM {db.Q("ReportFiles")} WHERE {db.Q("Id")} = ?", fileId) ?? throw ApiError.NotFound("File");
            ctx.EnsureOrg(Db.Long(f["OrganizationId"]));
            var abs = files.Abs(Db.Str(f["StoragePath"])!);
            if (!File.Exists(abs)) throw ApiError.NotFound("File");
            await audit.LogAsync("DOWNLOAD", "reportFiles", fileId, Db.Long(f["OrganizationId"]), null, u);
            return Results.File(abs, Db.Str(f["Format"]) == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Db.Str(f["FileName"]));
        });

        g.MapPost("/reports/package", async (HttpContext ctx, ReportService rs) =>
        {
            var b = await ctx.Body();
            var types = b.TryGetProperty("types", out var t) ? t.EnumerateArray().Select(x => x.GetString()!).ToArray() : ReportBuilder.Types.Select(x => x.key).ToArray();
            var formats = b.TryGetProperty("formats", out var f) ? f.EnumerateArray().Select(x => x.GetString()!).Where(x => x is "docx" or "pdf").ToArray() : new[] { "docx", "pdf" };
            var pOrg = b.Long("orgId") ?? ctx.OrgId();
            ctx.EnsureOrg(pOrg);
            var r = await rs.PackageAsync(ctx.U(), pOrg, types, formats, b.Long("assessmentId"), b.Date("periodFrom"), b.Date("periodTo"), b.Long("templateId"));
            return Results.File(r.Data, r.ContentType, r.FileName);
        });

        // Section outline for the template designer
        g.MapGet("/reports/outline", async (HttpContext ctx, ReportBuilder builder, EntityService es) =>
        {
            await es.Require(ctx.U(), "template.view");
            var doc = await builder.BuildAsync(new ReportRequest(ctx.OrgId(), ctx.Q("type") ?? "master", null, null, null, null));
            return Results.Ok(doc.Sections.Select(s => new { id = s.Id, heading = s.Heading, level = s.Level }));
        });

        // Branding preview: renders a short sample document with the organisation's branding (+ optional template)
        g.MapGet("/reports/branding-preview", async (HttpContext ctx, ReportService rs, EntityService es, PdfConverter pdf) =>
        {
            await es.Require(ctx.U(), "org.view");
            var orgId = ctx.OrgId();
            var tpl = ctx.QLong("templateId") is { } tid ? await rs.TemplateAsync(tid) : null;
            var b = await rs.BrandingAsync(orgId, null, tpl);
            if (ctx.Q("cover") is { Length: > 0 } cv) b.Cover = cv;
            var doc = new ReportDoc
            {
                Title = "Branding Preview", Subtitle = "Sample report page",
                Sections = new()
                {
                    new Section { Id = "s1", Heading = "Sample section", Blocks = new() { new Block { Type = "p", Text = "This preview shows the cover page, header, footer, colours and fonts that will be applied to generated reports." },
                        new Block { Type = "table", Columns = new() { "Requirement", "Status", "Risk" }, Rows = new() { new() { "Notice before consent", "Compliant", "Low" }, new() { "72-hour Board report readiness", "Partially Compliant", "High" }, new() { "Processor contracts", "Non-Compliant", "High" } } },
                        new Block { Type = "chart", ChartType = "pie", Title = "Sample distribution", Data = new() { new ChartPoint { Label = "Compliant", Value = 5 }, new ChartPoint { Label = "Partially Compliant", Value = 3 }, new ChartPoint { Label = "Non-Compliant", Value = 2 } } } } }
                }
            };
            if (!pdf.Available) throw new ApiError(503, "Live PDF preview needs LibreOffice on the server.");
            var (_, pdfBytes) = await rs.RenderAsync(doc, b, true);
            return Results.File(pdfBytes!, "application/pdf");
        });

        // ---------------- report templates
        g.MapGet("/templates", async (HttpContext ctx, Db db, EntityService es) =>
        {
            await es.Require(ctx.U(), "template.view");
            var orgId = ctx.OrgIdOrZero();
            var rows = await db.QueryAsync($"SELECT * FROM {db.Q("ReportTemplates")} WHERE {db.Q("IsDeleted")} = 0 AND ({db.Q("OrganizationId")} = 0 OR {db.Q("OrganizationId")} = ?) ORDER BY {db.Q("IsSystem")} DESC, {db.Q("Name")}", orgId);
            return Results.Ok(rows.Select(r => Json.Shape(r, Schema.Get("reportTemplates"))));
        });

        static Dictionary<string, object?> TplValues(JsonElement b) => new()
        {
            ["Name"] = b.Str("name") ?? "Custom template", ["ReportType"] = b.Str("reportType") ?? "*", ["CoverDesign"] = b.Str("coverDesign"), ["LogoPlacement"] = b.Str("logoPlacement"),
            ["Watermark"] = b.Str("watermark"), ["FontFamily"] = b.Str("fontFamily"), ["PrimaryColor"] = b.Str("primaryColor"), ["SecondaryColor"] = b.Str("secondaryColor"),
            ["SectionsJson"] = b.TryGetProperty("sectionsJson", out var s) ? (s.ValueKind == JsonValueKind.String ? s.GetString() : s.GetRawText()) : "[]",
            ["IsDefault"] = b.Bool("isDefault")
        };

        g.MapPost("/templates", async (HttpContext ctx, Db db, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "template.edit");
            var vals = TplValues(await ctx.Body());
            vals["OrganizationId"] = ctx.OrgId(); vals["IsSystem"] = false; vals["CreatedAt"] = DateTime.UtcNow; vals["CreatedBy"] = u.Email; vals["IsDeleted"] = false;
            var id = await db.InsertAsync("ReportTemplates", vals);
            await audit.LogAsync("CREATE", "reportTemplates", id, ctx.OrgId(), new { name = vals["Name"] }, u);
            return Results.Ok(new { id });
        });

        g.MapPut("/templates/{id:long}", async (long id, HttpContext ctx, Db db, EntityService es, ReportService rs, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "template.edit");
            var t = await rs.TemplateAsync(id);
            if (Db.Bool(t["IsSystem"])) throw ApiError.Bad("Default templates cannot be edited. Duplicate it to customise.");
            var vals = TplValues(await ctx.Body());
            vals["UpdatedAt"] = DateTime.UtcNow; vals["UpdatedBy"] = u.Email;
            await db.UpdateAsync("ReportTemplates", id, vals);
            await audit.LogAsync("UPDATE", "reportTemplates", id, Db.Long(t["OrganizationId"]), new { name = vals["Name"] }, u);
            return Results.Ok();
        });

        g.MapPost("/templates/{id:long}/duplicate", async (long id, HttpContext ctx, Db db, EntityService es, ReportService rs, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "template.edit");
            var t = await rs.TemplateAsync(id);
            var vals = t.Where(kv => kv.Key is not ("Id" or "UpdatedAt" or "UpdatedBy")).ToDictionary(kv => kv.Key, kv => kv.Value);
            vals["Name"] = $"{Db.Str(t["Name"])} (copy)"; vals["IsSystem"] = false; vals["IsDefault"] = false; vals["OrganizationId"] = ctx.OrgIdOrZero() is var dOrg and > 0 ? dOrg : Db.Long(t["OrganizationId"]);
            vals["CreatedAt"] = DateTime.UtcNow; vals["CreatedBy"] = u.Email;
            var nid = await db.InsertAsync("ReportTemplates", vals);
            await audit.LogAsync("CREATE", "reportTemplates", nid, Db.Long(vals["OrganizationId"]), new { duplicatedFrom = id }, u);
            return Results.Ok(new { id = nid });
        });

        g.MapDelete("/templates/{id:long}", async (long id, HttpContext ctx, Db db, EntityService es, ReportService rs, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "template.delete");
            var t = await rs.TemplateAsync(id);
            if (Db.Bool(t["IsSystem"])) throw ApiError.Bad("Default templates cannot be deleted.");
            await db.UpdateAsync("ReportTemplates", id, new Dictionary<string, object?> { ["IsDeleted"] = true, ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email });
            await audit.LogAsync("DELETE", "reportTemplates", id, Db.Long(t["OrganizationId"]), null, u);
            return Results.Ok();
        });

        g.MapPost("/templates/restore-defaults", async (HttpContext ctx, Db db, EntityService es, SeedService seed, AuditService audit) =>
        {
            var u = ctx.U();
            await es.Require(u, "template.edit");
            ctx.EnsureProvider();
            await db.ExecAsync($"UPDATE {db.Q("ReportTemplates")} SET {db.Q("IsDeleted")} = 1 WHERE {db.Q("IsSystem")} = 1");
            await db.ExecAsync($"DELETE FROM {db.Q("ReportTemplates")} WHERE {db.Q("IsSystem")} = 1");
            await seed.SeedSystemAsync();
            await audit.LogAsync("RESTORE", "reportTemplates", null, null, null, u);
            return Results.Ok();
        });
    }
}
