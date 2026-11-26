using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Services;

namespace DataKavach.Endpoints;

/// <summary>Branding of the service provider (MSSP) operating the platform: shown on the login page, app shell and reports.</summary>
public sealed class ProviderBranding
{
    public string Name { get; set; } = "Manlitics";
    public string Tagline { get; set; } = "Managed DPDP Compliance Services";
    public string PortalName { get; set; } = "DPDP Compliance Portal";
    public string PrimaryColor { get; set; } = "#0B2545";
    public string SecondaryColor { get; set; } = "#C9A227";
    public string SupportEmail { get; set; } = "";
    public string SupportPhone { get; set; } = "";
    public string Website { get; set; } = "";
    public string Address { get; set; } = "";
    public string LoginMessage { get; set; } = "Sign in to view your organisation's DPDP compliance status, evidence and reports.";
    public long LogoFileId { get; set; }
    public bool UseOnReports { get; set; } = true;
}

public static class ProviderEndpoints
{
    public const string Key = "provider.branding";

    public static Task<ProviderBranding> GetBranding(SettingsService s) => s.GetJsonAsync(Key, new ProviderBranding());

    public static void MapProviderEndpoints(this WebApplication app)
    {
        // public: needed before sign-in
        app.MapGet("/api/branding", async (SettingsService s) =>
        {
            var b = await GetBranding(s);
            return Results.Ok(new { b.Name, b.Tagline, b.PortalName, b.PrimaryColor, b.SecondaryColor, b.SupportEmail, b.SupportPhone, b.Website, b.LoginMessage, b.Address, b.UseOnReports, hasLogo = b.LogoFileId > 0 });
        });
        app.MapGet("/api/branding/logo", async (SettingsService s, Db db, FileService files) =>
        {
            var b = await GetBranding(s);
            if (b.LogoFileId <= 0) return Results.NotFound();
            var f = await db.FirstAsync($"SELECT * FROM {db.Q("StoredFiles")} WHERE {db.Q("Id")} = ?", b.LogoFileId);
            if (f == null) return Results.NotFound();
            var abs = files.Abs(Db.Str(f["StoragePath"])!);
            return File.Exists(abs) ? Results.File(abs, Db.Str(f["ContentType"]) ?? "image/png") : Results.NotFound();
        });

        var g = app.MapGroup("/api").RequireAuthorization();

        g.MapPut("/branding", async (HttpContext ctx, SettingsService s, EntityService es, AuditService audit) =>
        {
            var u = ctx.U();
            ctx.EnsureProvider();
            await es.Require(u, "admin.settings");
            var cur = await GetBranding(s);
            var b = await JsonSerializer.DeserializeAsync<ProviderBranding>(ctx.Request.Body, Json.Options) ?? throw ApiError.Bad("Invalid body");
            foreach (var c in new[] { b.PrimaryColor, b.SecondaryColor })
                if (!System.Text.RegularExpressions.Regex.IsMatch(c ?? "", "^#[0-9A-Fa-f]{6}$")) throw ApiError.Bad("Colours must be hex values like #0B2545");
            if (string.IsNullOrWhiteSpace(b.Name)) throw ApiError.Bad("Company name is required");
            b.LogoFileId = cur.LogoFileId;
            await s.SetAsync(Key, JsonSerializer.Serialize(b, Json.Options));
            await audit.LogAsync("UPDATE", "providerBranding", null, null, new { b.Name, b.PrimaryColor, b.SecondaryColor }, u);
            return Results.Ok();
        });

        g.MapPost("/branding/logo", async (HttpContext ctx, SettingsService s, EntityService es, FileService files) =>
        {
            var u = ctx.U();
            ctx.EnsureProvider();
            await es.Require(u, "admin.settings");
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault() ?? throw ApiError.Bad("No file uploaded");
            var id = await files.StoreGenericAsync(file, 0, "provider-logo", u, new[] { ".png", ".jpg", ".jpeg" });
            var b = await GetBranding(s);
            b.LogoFileId = id;
            await s.SetAsync(Key, JsonSerializer.Serialize(b, Json.Options));
            return Results.Ok(new { fileId = id });
        }).DisableAntiforgery();

        g.MapDelete("/branding/logo", async (HttpContext ctx, SettingsService s, EntityService es) =>
        {
            ctx.EnsureProvider();
            await es.Require(ctx.U(), "admin.settings");
            var b = await GetBranding(s);
            b.LogoFileId = 0;
            await s.SetAsync(Key, JsonSerializer.Serialize(b, Json.Options));
            return Results.Ok();
        });

        // All-clients overview for provider staff
        g.MapGet("/portfolio", async (HttpContext ctx, Db db, DashboardService dash, EntityService es) =>
        {
            var u = ctx.U();
            ctx.EnsureProvider();
            await es.Require(u, "assessment.view");
            var orgs = await db.QueryAsync($"SELECT * FROM {db.Q("Organizations")} WHERE {db.Q("IsDeleted")} = 0 ORDER BY {db.Q("Name")}");
            var users = (await db.QueryAsync($"SELECT {db.Q("OrganizationId")}, COUNT(*) AS {db.Q("N")} FROM {db.Q("Users")} WHERE {db.Q("IsDeleted")} = 0 AND {db.Q("UserType")} = 'Customer' GROUP BY {db.Q("OrganizationId")}"))
                .ToDictionary(r => Db.Long(r["OrganizationId"]), r => Db.Int(r["N"]));
            var lastActivity = (await db.QueryAsync($"SELECT {db.Q("OrganizationId")}, MAX({db.Q("At")}) AS {db.Q("Last")} FROM {db.Q("AuditLogs")} WHERE {db.Q("OrganizationId")} > 0 GROUP BY {db.Q("OrganizationId")}"))
                .ToDictionary(r => Db.Long(r["OrganizationId"]), r => Db.Dt(r["Last"]));
            var clients = new List<object>();
            foreach (var o in orgs)
            {
                var id = Db.Long(o["Id"]);
                var d = JsonSerializer.SerializeToElement(await dash.BuildAsync(id, null), Json.Options);
                var st = d.GetProperty("status"); var risk = d.GetProperty("risk"); var act = d.GetProperty("actions"); var inc = d.GetProperty("incidents"); var rights = d.GetProperty("rights"); var ven = d.GetProperty("vendors"); var sec = d.GetProperty("security");
                double? ind = st.GetProperty("indicator").ValueKind == JsonValueKind.Number ? st.GetProperty("indicator").GetDouble() : null;
                var high = risk.TryGetProperty("High", out var h) ? h.GetInt32() : 0;
                var overdueNotif = inc.GetProperty("overdueNotifications").GetInt32();
                var overdueActs = act.GetProperty("overdue").GetInt32();
                var hasAssessment = d.GetProperty("assessmentId").ValueKind == JsonValueKind.Number;
                string health = !hasAssessment ? "Not started" : overdueNotif > 0 || high >= 5 || (ind ?? 0) < 40 ? "At risk" : high > 0 || overdueActs > 0 || (ind ?? 0) < 75 ? "Needs attention" : "On track";
                clients.Add(new
                {
                    id, name = Db.Str(o["Name"]), industry = Db.Str(o["Industry"]), city = Db.Str(o["City"]), isSdf = Db.Bool(o["IsSignificantDataFiduciary"]),
                    indicator = ind, completion = st.GetProperty("completion").GetDouble(), compliant = st.GetProperty("compliant").GetInt32(), partial = st.GetProperty("partial").GetInt32(),
                    nonCompliant = st.GetProperty("nonCompliant").GetInt32(), highRisk = high, mediumRisk = risk.TryGetProperty("Medium", out var m) ? m.GetInt32() : 0,
                    openActions = act.GetProperty("open").GetInt32(), overdueActions = overdueActs, openIncidents = inc.GetProperty("open").GetInt32(), overdueNotifications = overdueNotif,
                    openRequests = rights.GetProperty("open").GetInt32(), overdueRequests = rights.GetProperty("overdue").GetInt32(), processorsWithoutDpa = ven.GetProperty("processorsWithoutDpa").GetInt32(),
                    validatedControls = sec.GetProperty("validated").GetInt32(), totalControls = sec.GetProperty("total").GetInt32(), evidencePending = d.GetProperty("evidencePending").GetInt32(),
                    portalUsers = users.GetValueOrDefault(id), lastActivity = lastActivity.GetValueOrDefault(id), health, hasAssessment
                });
            }
            var dynamicClients = clients.Select(c => JsonSerializer.SerializeToElement(c, Json.Options)).ToList();
            int Sum(string k) => dynamicClients.Sum(c => c.GetProperty(k).GetInt32());
            var inds = dynamicClients.Where(c => c.GetProperty("indicator").ValueKind == JsonValueKind.Number).Select(c => c.GetProperty("indicator").GetDouble()).ToList();
            return Results.Ok(new
            {
                totals = new
                {
                    clients = clients.Count, averageIndicator = inds.Count > 0 ? Math.Round(inds.Average(), 1) : (double?)null, highRisk = Sum("highRisk"), openActions = Sum("openActions"),
                    overdueActions = Sum("overdueActions"), openIncidents = Sum("openIncidents"), overdueNotifications = Sum("overdueNotifications"), openRequests = Sum("openRequests"),
                    overdueRequests = Sum("overdueRequests"), portalUsers = Sum("portalUsers"),
                    byHealth = new[] { "On track", "Needs attention", "At risk", "Not started" }.ToDictionary(h => h, h => dynamicClients.Count(c => c.GetProperty("health").GetString() == h))
                },
                daysToCore = (DashboardService.CoreCommencement - DateTime.UtcNow.Date).Days,
                clients
            });
        });

        // One-step client onboarding: organisation + first customer administrator
        g.MapPost("/clients/onboard", async (HttpContext ctx, Db db, EntityService es, SeedService seed, AuditService audit) =>
        {
            var u = ctx.U();
            ctx.EnsureProvider();
            await es.Require(u, "org.edit");
            var b = await ctx.Body();
            var name = b.Str("name") ?? throw ApiError.Bad("Organisation name is required");
            var orgId = await es.CreateAsync(Schema.Get("organizations"), u, 0, new Dictionary<string, object?> { ["Name"] = name, ["Industry"] = b.Str("industry") });
            long? userId = null;
            if (b.Str("adminEmail") is { Length: > 0 } email)
            {
                await es.Require(u, "admin.users");
                userId = await seed.CreateUserAsync(email, b.Str("adminName") ?? email, b.Str("adminPassword") ?? "", Roles.CustomerAdmin, mustChange: true, userType: "Customer", orgId: orgId);
                await audit.LogAsync("CREATE", "users", userId, orgId, new { email, role = Roles.CustomerAdmin, type = "Customer" }, u);
            }
            return Results.Ok(new { orgId, userId });
        });
    }
}
