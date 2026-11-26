using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Reports;
using DataKavach.Services;

namespace DataKavach.Endpoints;

public static class EntityEndpoints
{
    public static void MapEntityEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireAuthorization();

        g.MapGet("/meta", async (HttpContext ctx, PermissionService perms, Db db) =>
        {
            var u = ctx.U();
            return Results.Ok(new
            {
                entities = Schema.All.Select(e => new
                {
                    key = e.Key, label = e.Label, module = e.Module, orgScoped = e.OrgScoped, parentKey = e.ParentKey, parentCol = e.ParentCol is { } pc ? Json.CamelKey(pc) : null,
                    listCols = e.ListCols.Select(Json.CamelKey), titleCol = Json.CamelKey(e.TitleCol), genericWrite = e.GenericWrite,
                    cols = e.Cols.Select(c => new { name = Json.CamelKey(c.Name), type = c.Type.ToString().ToLowerInvariant(), label = c.DisplayLabel, len = c.Len, required = c.Required, options = c.Options, hidden = c.Hidden, readOnly = c.ReadOnly, sensitive = c.Sensitive, @ref = c.Ref, group = c.Group, help = c.Help, multi = c.Multi })
                }),
                roles = Roles.All,
                permissions = Roles.AllPermissions(),
                reportTypes = ReportBuilder.Types.Select(t => new { key = t.key, title = t.title }),
                policyTemplates = PolicyTemplates.All.Select(t => new { key = t.Key, title = t.Title }),
                securityTools = Schema.Get("securityControls").Find("ToolsMapped")!.Options,
                commencement = new { core = DashboardService.CoreCommencement.ToString("yyyy-MM-dd"), consentManager = DashboardService.ConsentManagerCommencement.ToString("yyyy-MM-dd") }
            });
        });

        g.MapGet("/e/{key}", async (string key, HttpContext ctx, EntityService es) =>
        {
            var e = Schema.Get(key);
            var q = ctx.Request.Query;
            var filters = q.Where(kv => kv.Key.StartsWith("f.")).ToDictionary(kv => kv.Key[2..], kv => kv.Value.ToString());
            var page = int.TryParse(q["page"], out var p) ? Math.Max(1, p) : 1;
            var size = int.TryParse(q["pageSize"], out var s) ? Math.Clamp(s, 1, 1000) : 50;
            var (rows, total) = await es.ListAsync(e, ctx.U(), new ListQuery(ctx.QLong("orgId"), ctx.QLong("parentId"), q["q"], page, size, q["sort"].FirstOrDefault(), q["desc"] == "true", filters));
            return Results.Ok(new { rows, total, page, pageSize = size });
        });

        g.MapGet("/e/{key}/{id:long}", async (string key, long id, HttpContext ctx, EntityService es) => Results.Ok(await es.GetAsync(Schema.Get(key), ctx.U(), id)));

        g.MapPost("/e/{key}", async (string key, HttpContext ctx, EntityService es) =>
        {
            var e = Schema.Get(key);
            if (!e.GenericWrite) throw ApiError.Bad("Use the dedicated endpoint for this record type.");
            var body = await ctx.Body();
            long orgId = 0;
            if (e.OrgScoped) orgId = ctx.QLong("orgId") ?? body.Long("organizationId") ?? throw ApiError.Bad("orgId is required");
            if (e.Key == "organizations") await es.Require(ctx.U(), "org.edit");
            var id = await es.CreateAsync(e, ctx.U(), orgId, es.ReadBody(e, body));
            return Results.Ok(await es.GetAsync(e, ctx.U(), id));
        });

        g.MapPut("/e/{key}/{id:long}", async (string key, long id, HttpContext ctx, EntityService es) =>
        {
            var e = Schema.Get(key);
            if (!e.GenericWrite) throw ApiError.Bad("Use the dedicated endpoint for this record type.");
            var vals = es.ReadBody(e, await ctx.Body());
            if (e.ParentCol != null) vals.Remove(e.ParentCol);
            await es.UpdateAsync(e, ctx.U(), id, vals);
            return Results.Ok(await es.GetAsync(e, ctx.U(), id));
        });

        g.MapDelete("/e/{key}/{id:long}", async (string key, long id, HttpContext ctx, EntityService es) =>
        {
            var e = Schema.Get(key);
            if (!e.GenericWrite && e.Key != "evidence") throw ApiError.Bad("This record type cannot be deleted here.");
            await es.DeleteAsync(e, ctx.U(), id);
            return Results.Ok();
        });

        g.MapGet("/e/{key}/export", async (string key, HttpContext ctx, ImportExportService io) =>
        {
            var (data, ct, name) = await io.ExportAsync(Schema.Get(key), ctx.U(), ctx.OrgId(), ctx.Q("format") == "xlsx" ? "xlsx" : "csv");
            return Results.File(data, ct, name);
        });

        g.MapGet("/e/{key}/import-template", async (string key, HttpContext ctx, ImportExportService io) =>
        {
            var (data, name) = await io.TemplateAsync(Schema.Get(key), ctx.U());
            return Results.File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
        });

        g.MapPost("/e/{key}/import", async (string key, HttpContext ctx, ImportExportService io) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault() ?? throw ApiError.Bad("No file uploaded");
            return Results.Ok(await io.ImportAsync(Schema.Get(key), ctx.U(), ctx.OrgId(), file));
        }).DisableAntiforgery();
    }
}
