using System.Reflection;
using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;

namespace DataKavach.Services;

public sealed class SeedService(Db db, PermissionService perms)
{
    public static readonly (string key, string domain, string name, string rule, string expectation)[] SecurityControls =
    {
        ("SC-01", "Data protection", "Encryption at rest", "Rule 6(1)(a)", "Databases, file stores, backups and endpoints holding personal data are encrypted (e.g. TDE, BitLocker, storage encryption) with managed keys."),
        ("SC-02", "Data protection", "Encryption in transit", "Rule 6(1)(a)", "TLS 1.2+ for all external and internal transfers of personal data; legacy protocols disabled."),
        ("SC-03", "Data protection", "Masking / tokenisation", "Rule 6(1)(a)", "Personal data is masked or tokenised in non-production, reports and support tools where full values are not needed."),
        ("SC-04", "Access", "Access control", "Rule 6(1)(b)", "Role-based access to systems holding personal data; least privilege; joiner-mover-leaver process; quarterly access reviews."),
        ("SC-05", "Access", "Multi-factor authentication", "Rule 6(1)(b)", "MFA enforced for remote access, administrators, email and applications holding personal data."),
        ("SC-06", "Access", "Privileged access management", "Rule 6(1)(b)", "Privileged accounts vaulted, sessions recorded, just-in-time elevation."),
        ("SC-07", "Access", "Identity management", "Rule 6(1)(b)", "Central directory (AD/Entra ID) with SSO; dormant accounts disabled."),
        ("SC-08", "Endpoint", "Endpoint security", "Rule 6(1)(g)", "Endpoint hardening, device encryption, USB/DLP controls on devices handling personal data."),
        ("SC-09", "Endpoint", "Antivirus and EDR", "Rule 6(1)(g)", "EDR deployed on all servers and endpoints with alerts monitored."),
        ("SC-10", "Monitoring", "SIEM monitoring", "Rule 6(1)(c)", "Logs from personal data systems centrally collected; use cases for unauthorised access; alerts triaged."),
        ("SC-11", "Monitoring", "Audit logging and 1-year retention", "Rule 6(1)(c),(e); Rule 8(3)", "Access and processing logs retained for at least one year (CERT-In also requires 180 days in India)."),
        ("SC-12", "Vulnerability", "Vulnerability management", "Rule 6(1)(g)", "Periodic vulnerability scans and VA/PT with tracked remediation SLAs."),
        ("SC-13", "Vulnerability", "Patch management", "Rule 6(1)(g)", "OS, database and application patches applied within defined SLAs."),
        ("SC-14", "Resilience", "Backup and recovery", "Rule 6(1)(d)", "Backups of personal data systems, protected from tampering, with periodic restore tests."),
        ("SC-15", "Application", "Database security", "Rule 6(1)(a),(b)", "Database hardening, auditing, no shared accounts, encryption of sensitive columns."),
        ("SC-16", "Application", "Application security", "Rule 6(1)(g)", "Secure SDLC, code review/SAST, OWASP testing for applications processing personal data."),
        ("SC-17", "Network", "Network security", "Rule 6(1)(b)", "Firewalls, segmentation of personal data systems, secure remote access, IDS/IPS."),
        ("SC-18", "Response", "Incident response", "Rule 6(1)(g); Rule 7", "Documented incident and breach response plan with DPDP/CERT-In reporting, tested by drills."),
        ("SC-19", "Third party", "Vendor security", "Rule 6(1)(f)", "Processor contracts include security safeguards; vendors assessed before onboarding and periodically."),
    };

    public static readonly string[] DefaultDepartments = { "Information Technology", "Information Security", "Human Resources", "Legal & Compliance", "Finance", "Sales & Marketing", "Operations", "Customer Support" };

    public async Task SeedSystemAsync()
    {
        // role permissions (only if role has none - preserves admin customisations)
        foreach (var role in Roles.All)
        {
            var has = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("RolePermissions")} WHERE {db.Q("Role")} = ?", role);
            if (has > 0) continue;
            foreach (var p in Roles.Defaults(role).Distinct())
                await db.InsertAsync("RolePermissions", new Dictionary<string, object?> { ["Role"] = role, ["Permission"] = p, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = "system" });
        }
        perms.Invalidate();

        // requirement library (insert new codes only - never overwrites edits)
        var existing = (await db.QueryAsync($"SELECT {db.Q("Code")} FROM {db.Q("Requirements")}")).Select(r => Db.Str(r["Code"])).ToHashSet();
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(asm.GetManifestResourceNames().First(n => n.EndsWith("requirements.json")))!;
        var reqs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(s)!;
        var def = Schema.Get("requirements");
        int order = 0;
        foreach (var r in reqs)
        {
            order += 10;
            if (existing.Contains(r["code"].GetString())) continue;
            var vals = new Dictionary<string, object?>();
            foreach (var (k, v) in r) if (def.Find(k) is { } c) vals[c.Name] = EntityService.Coerce(c, v);
            vals["IsActive"] = true; vals["SortOrder"] = order; vals["CreatedAt"] = DateTime.UtcNow; vals["CreatedBy"] = "system"; vals["IsDeleted"] = false;
            await db.InsertAsync("Requirements", vals);
        }

        // system report template
        if (await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("ReportTemplates")} WHERE {db.Q("IsSystem")} = 1") == 0)
        {
            foreach (var (name, cover) in new[] { ("Standard (Corporate Band)", "Corporate Band"), ("Executive Minimal", "Executive Minimal"), ("Bold Block", "Bold Block"), ("Split Panel", "Split Panel"), ("Framed Classic", "Framed Classic") })
                await db.InsertAsync("ReportTemplates", new Dictionary<string, object?>
                {
                    ["OrganizationId"] = 0, ["Name"] = name, ["ReportType"] = "*", ["CoverDesign"] = cover, ["LogoPlacement"] = "Header left",
                    ["SectionsJson"] = "[]", ["IsDefault"] = name.StartsWith("Standard"), ["IsSystem"] = true, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = "system", ["IsDeleted"] = false
                });
        }
    }

    public async Task SeedOrganizationAsync(long orgId, CurrentUser user)
    {
        foreach (var d in DefaultDepartments)
            await db.InsertAsync("Departments", new Dictionary<string, object?> { ["OrganizationId"] = orgId, ["Name"] = d, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = user.Email, ["IsDeleted"] = false });
        foreach (var c in SecurityControls)
            await db.InsertAsync("SecurityControls", new Dictionary<string, object?>
            {
                ["OrganizationId"] = orgId, ["ControlKey"] = c.key, ["Domain"] = c.domain, ["ControlName"] = c.name, ["RuleReference"] = c.rule, ["Expectation"] = c.expectation,
                ["Status"] = "Not Assessed", ["Validated"] = false, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = user.Email, ["IsDeleted"] = false
            });
    }

    public async Task<long> CreateUserAsync(string email, string name, string password, string role, bool mustChange = false, string userType = "Provider", long? orgId = null)
    {
        var err = Passwords.Validate(password);
        if (err != null) throw ApiError.Bad(err);
        if (!Roles.All.Contains(role)) throw ApiError.Bad("Unknown role");
        email = email.Trim().ToLowerInvariant();
        if (await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Users")} WHERE {db.Q("Email")} = ?", email) > 0) throw ApiError.Bad("A user with this email already exists");
        return await db.InsertAsync("Users", new Dictionary<string, object?>
        {
            ["Email"] = email, ["FullName"] = name, ["PasswordHash"] = Passwords.Hash(password), ["Role"] = role, ["UserType"] = userType, ["OrganizationId"] = orgId, ["IsActive"] = true, ["MfaEnabled"] = false,
            ["FailedAttempts"] = 0, ["MustChangePassword"] = mustChange, ["PasswordChangedAt"] = DateTime.UtcNow, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = "system", ["IsDeleted"] = false
        });
    }
}
