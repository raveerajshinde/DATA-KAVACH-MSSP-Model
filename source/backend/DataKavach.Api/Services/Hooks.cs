using DataKavach.Auth;
using DataKavach.Data;

namespace DataKavach.Services;

public sealed class OrganizationHooks(SeedService seed) : IEntityHooks
{
    public string EntityKey => "organizations";
    public Task BeforeSaveAsync(SaveContext c)
    {
        if (c.Id == null)
        {
            void Def(string k, object v) { if (!c.Values.ContainsKey(k) || c.Values[k] == null || c.Values[k] is "") c.Values[k] = v; }
            Def("PrimaryColor", "#0B3D91"); Def("SecondaryColor", "#F2A900"); Def("FontFamily", "Calibri"); Def("CoverDesign", "Corporate Band");
            Def("LogoPlacement", "Header left"); Def("Confidentiality", "Confidential"); Def("GrievanceResponseDays", 30); Def("ReportVersion", "1.0");
            Def("ReliesOnConsent", true); Def("UsesProcessors", true); Def("CertInApplicable", true); Def("ThirdScheduleClass", "None");
        }
        if (c.Values.TryGetValue("GrievanceResponseDays", out var g) && g is int d && (d < 1 || d > 90))
            throw ApiError.Bad("Grievance response period must be between 1 and 90 days (Rule 14(3)).");
        foreach (var k in new[] { "PrimaryColor", "SecondaryColor" })
            if (c.Values.TryGetValue(k, out var col) && col is string s && s.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(s, "^#[0-9A-Fa-f]{6}$"))
                throw ApiError.Bad($"{k} must be a hex colour like #0B3D91");
        return Task.CompletedTask;
    }
    public Task AfterSaveAsync(SaveContext c, long id) => c.Id == null ? seed.SeedOrganizationAsync(id, c.User) : Task.CompletedTask;
}

public sealed class DpiaRiskHooks(Db db) : IEntityHooks
{
    public string EntityKey => "dpiaRisks";
    public static string Rate(int score) => score >= 15 ? "High" : score >= 8 ? "Medium" : "Low";

    public Task BeforeSaveAsync(SaveContext c)
    {
        int V(string k) => int.TryParse(Db.Str(c.Raw(k)), out var x) ? x : 0;
        int l = V("Likelihood"), i = V("Impact"), rl = V("ResidualLikelihood"), ri = V("ResidualImpact");
        if (l > 0 && i > 0) { c.Values["InherentScore"] = l * i; c.Values["InherentRating"] = Rate(l * i); }
        if (rl > 0 && ri > 0) { c.Values["ResidualScore"] = rl * ri; c.Values["ResidualRating"] = Rate(rl * ri); }
        return Task.CompletedTask;
    }

    public async Task AfterSaveAsync(SaveContext c, long id)
    {
        var dpiaId = Db.Long(c.Raw("DpiaId"));
        var rows = await db.QueryAsync($"SELECT {db.Q("ResidualScore")}, {db.Q("InherentScore")} FROM {db.Q("DpiaRisks")} WHERE {db.Q("DpiaId")} = ? AND {db.Q("IsDeleted")} = 0", dpiaId);
        var max = rows.Select(r => Db.Int(r["ResidualScore"]) > 0 ? Db.Int(r["ResidualScore"]) : Db.Int(r["InherentScore"])).DefaultIfEmpty(0).Max();
        await db.UpdateAsync("Dpias", dpiaId, new Dictionary<string, object?> { ["OverallResidualRisk"] = max == 0 ? null : Rate(max) });
    }
}

public sealed class RightsRequestHooks(Db db, SettingsService settings) : IEntityHooks
{
    public string EntityKey => "rightsRequests";
    static readonly string[] Closed = { "Completed", "Rejected", "Closed" };

    public async Task BeforeSaveAsync(SaveContext c)
    {
        if (c.Id == null)
        {
            var n = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("RightsRequests")} WHERE {db.Q("OrganizationId")} = ?", c.OrgId);
            c.Values["Reference"] = $"DPR-{DateTime.UtcNow:yyyy}-{n + 1:D4}";
            if (!c.Values.ContainsKey("Status") || c.Values["Status"] == null) c.Values["Status"] = "New";
            if (!c.Values.ContainsKey("VerificationStatus") || c.Values["VerificationStatus"] == null) c.Values["VerificationStatus"] = "Pending";
        }
        if (c.Values.ContainsKey("ReceivedAt") || c.Values.ContainsKey("RequestType"))
        {
            var received = Db.Dt(c.Raw("ReceivedAt")) ?? DateTime.UtcNow;
            var type = Db.Str(c.Raw("RequestType")) ?? "";
            int days;
            if (type.StartsWith("Grievance"))
            {
                var org = await db.FirstAsync($"SELECT {db.Q("GrievanceResponseDays")} FROM {db.Q("Organizations")} WHERE {db.Q("Id")} = ?", c.OrgId);
                days = Math.Clamp(Db.Int(org?["GrievanceResponseDays"]) is var g and > 0 ? g : 30, 1, 90);
            }
            else days = int.TryParse(await settings.GetAsync("rights.responseDays", c.OrgId), out var d) ? d : 30;
            c.Values["DueAt"] = received.AddDays(days);
        }
        if (c.Values.TryGetValue("Status", out var st) && st is string s && Closed.Contains(s) && c.Existing?.GetValueOrDefault("ClosedAt") == null)
            c.Values["ClosedAt"] = DateTime.UtcNow;
        if (c.Values.TryGetValue("VerificationStatus", out var vs) && vs is "Verified" && c.Raw("VerifiedAt") == null) c.Values["VerifiedAt"] = DateTime.UtcNow;
    }

    public async Task AfterSaveAsync(SaveContext c, long id)
    {
        var notes = new List<string>();
        if (c.Id == null) notes.Add($"Request received ({c.Raw("RequestType")}). Due by {Db.Dt(c.Values["DueAt"]):yyyy-MM-dd}.");
        else
        {
            foreach (var k in new[] { "Status", "VerificationStatus", "AssignedTo" })
                if (c.Values.TryGetValue(k, out var v) && Db.Str(v) != Db.Str(c.Existing?.GetValueOrDefault(k)))
                    notes.Add($"{Col.Humanize(k)} changed from '{Db.Str(c.Existing?.GetValueOrDefault(k)) ?? "-"}' to '{Db.Str(v)}'");
            if (c.Values.ContainsKey("Response") && Db.Str(c.Values["Response"]) != Db.Str(c.Existing?.GetValueOrDefault("Response"))) notes.Add("Response updated");
            if (c.Values.ContainsKey("ClosureEvidence") && Db.Str(c.Values["ClosureEvidence"]) != Db.Str(c.Existing?.GetValueOrDefault("ClosureEvidence"))) notes.Add("Closure evidence updated");
        }
        foreach (var n in notes)
            await db.InsertAsync("RequestEvents", new Dictionary<string, object?> { ["OrganizationId"] = c.OrgId, ["RequestId"] = id, ["Action"] = c.Id == null ? "Created" : "Updated", ["Notes"] = n, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = c.User.Email });
    }
}

public sealed class IncidentHooks(Db db, BreachService breach) : IEntityHooks
{
    public string EntityKey => "incidents";
    public async Task BeforeSaveAsync(SaveContext c)
    {
        if (c.Id == null)
        {
            var n = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Incidents")} WHERE {db.Q("OrganizationId")} = ?", c.OrgId);
            c.Values["Reference"] = $"INC-{DateTime.UtcNow:yyyy}-{n + 1:D4}";
            c.Values.TryAdd("Status", "Open");
            c.Values["LegalApproval"] = "Pending"; c.Values["ManagementApproval"] = "Pending";
        }
        if (c.Values.TryGetValue("Status", out var s) && s is "Closed") c.Values["ClosedAt"] = DateTime.UtcNow;
    }
    public async Task AfterSaveAsync(SaveContext c, long id)
    {
        await breach.SyncNotificationsAsync(id, c.User);
        if (c.Id == null)
            await db.InsertAsync("IncidentEvents", new Dictionary<string, object?> { ["OrganizationId"] = c.OrgId, ["IncidentId"] = id, ["EventAt"] = Db.Dt(c.Raw("DiscoveredAt")) ?? DateTime.UtcNow, ["EventText"] = "Incident recorded; organisation became aware.", ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = c.User.Email, ["IsDeleted"] = false });
        else if (c.Values.TryGetValue("Status", out var s) && Db.Str(s) != Db.Str(c.Existing?.GetValueOrDefault("Status")))
            await db.InsertAsync("IncidentEvents", new Dictionary<string, object?> { ["OrganizationId"] = c.OrgId, ["IncidentId"] = id, ["EventAt"] = DateTime.UtcNow, ["EventText"] = $"Status changed to {s}", ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = c.User.Email, ["IsDeleted"] = false });
    }
}

public sealed class NotificationHooks(Db db) : IEntityHooks
{
    public string EntityKey => "incidentNotifications";
    public Task BeforeSaveAsync(SaveContext c)
    {
        if (c.Values.TryGetValue("SentAt", out var s) && s != null && (c.Raw("Status") is null or "Pending")) c.Values["Status"] = "Sent";
        return Task.CompletedTask;
    }
    public async Task AfterSaveAsync(SaveContext c, long id)
    {
        if (c.Values.TryGetValue("SentAt", out var s) && s != null && c.Existing?.GetValueOrDefault("SentAt") == null)
            await db.InsertAsync("IncidentEvents", new Dictionary<string, object?> { ["OrganizationId"] = c.OrgId, ["IncidentId"] = Db.Long(c.Raw("IncidentId")), ["EventAt"] = Db.Dt(s), ["EventText"] = $"Notification sent to {c.Raw("RecipientName")} ({c.Raw("Method") ?? "method not recorded"}).", ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = c.User.Email, ["IsDeleted"] = false });
    }
}

public sealed class ActionHooks(Db db) : IEntityHooks
{
    public string EntityKey => "actions";
    public async Task BeforeSaveAsync(SaveContext c)
    {
        if (c.Id == null)
        {
            var n = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Actions")} WHERE {db.Q("OrganizationId")} = ?", c.OrgId);
            c.Values["GapId"] = $"GAP-{n + 1:D4}";
            c.Values.TryAdd("Status", "Open");
            c.Values["Approval"] = "Pending";
            if (c.Raw("Plan") == null && c.Raw("TargetDate") is DateOnly td)
            {
                var days = (td.ToDateTime(TimeOnly.MinValue) - DateTime.UtcNow.Date).TotalDays;
                c.Values["Plan"] = days <= 30 ? "30-day" : days <= 60 ? "60-day" : days <= 90 ? "90-day" : "Custom";
            }
        }
        if (c.Values.TryGetValue("Status", out var s) && s is "Completed" && c.Existing?.GetValueOrDefault("CompletedAt") == null) c.Values["CompletedAt"] = DateTime.UtcNow;
    }
}

public sealed class SecurityControlHooks : IEntityHooks
{
    public string EntityKey => "securityControls";
    public Task BeforeSaveAsync(SaveContext c)
    {
        var validated = Db.Bool(c.Raw("Validated"));
        var evidence = Db.Str(c.Raw("ConfigurationEvidence"));
        if (validated && string.IsNullOrWhiteSpace(evidence))
            throw ApiError.Bad("A control can only be marked validated with configuration evidence recorded.");
        if (c.Values.TryGetValue("Validated", out var v) && Db.Bool(v) && !Db.Bool(c.Existing?.GetValueOrDefault("Validated")))
        { c.Values["ValidatedBy"] = c.User.Email; c.Values["ValidatedAt"] = DateTime.UtcNow; }
        if (c.Values.TryGetValue("Validated", out var v2) && !Db.Bool(v2)) { c.Values["ValidatedBy"] = null; c.Values["ValidatedAt"] = null; }
        if (Db.Str(c.Raw("Status")) == "Compliant" && !validated)
            throw ApiError.Bad("A safeguard cannot be marked Compliant until it is validated with configuration evidence. Installing a tool is not sufficient.");
        return Task.CompletedTask;
    }
}

public sealed class ConsentRecordHooks(Db db) : IEntityHooks
{
    public string EntityKey => "consentRecords";
    public async Task BeforeSaveAsync(SaveContext c)
    {
        if (c.Id == null)
        {
            c.Values.TryAdd("Status", "Given");
            if (c.Raw("GivenAt") == null) c.Values["GivenAt"] = DateTime.UtcNow;
            if (c.Raw("NoticeId") is long nid && c.Raw("NoticeVersion") == null)
                c.Values["NoticeVersion"] = Db.Str(await db.ScalarAsync($"SELECT {db.Q("Version")} FROM {db.Q("ConsentNotices")} WHERE {db.Q("Id")} = ?", nid));
        }
        if (c.Values.TryGetValue("Status", out var s) && s is "Withdrawn" && c.Raw("WithdrawnAt") == null) c.Values["WithdrawnAt"] = DateTime.UtcNow;
    }
    public async Task AfterSaveAsync(SaveContext c, long id)
    {
        string? ev = c.Id == null ? "Consent given" : c.Values.TryGetValue("Status", out var s) && Db.Str(s) != Db.Str(c.Existing?.GetValueOrDefault("Status")) ? $"Status changed to {s}" : "Record updated";
        await db.InsertAsync("ConsentEvents", new Dictionary<string, object?> { ["OrganizationId"] = c.OrgId, ["ConsentRecordId"] = id, ["EventType"] = ev, ["Details"] = $"Purpose: {c.Raw("Purpose")}; notice v{c.Raw("NoticeVersion")}; channel {c.Raw("Channel")}", ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = c.User.Email });
    }
}

public sealed class AssessmentItemHooks : IEntityHooks
{
    public string EntityKey => "assessmentItems";
    public Task BeforeSaveAsync(SaveContext c)
    {
        if (c.Values.TryGetValue("Status", out var s))
        {
            c.Values["AssessedBy"] = c.User.Email; c.Values["AssessedAt"] = DateTime.UtcNow;
            if (s is "Not Applicable") c.Values["IsApplicable"] = false;
            else if (c.Existing != null && !Db.Bool(c.Existing.GetValueOrDefault("IsApplicable"))) c.Values["IsApplicable"] = true;
        }
        if (c.Values.TryGetValue("IsApplicable", out var a) && a is false) c.Values["Status"] = "Not Applicable";
        return Task.CompletedTask;
    }
}

public sealed class VendorHooks : IEntityHooks
{
    public string EntityKey => "vendors";
    public Task BeforeSaveAsync(SaveContext c)
    {
        if (c.Raw("RiskRating") == null)
        {
            var processor = Db.Bool(c.Raw("IsDataProcessor"));
            var dpa = Db.Str(c.Raw("DpaStatus")) ?? "";
            var hasData = !string.IsNullOrWhiteSpace(Db.Str(c.Raw("PersonalDataAccessed")));
            c.Values["RiskRating"] = processor && (dpa.StartsWith("Not in place") || dpa.Length == 0) ? "High"
                : processor && (dpa.Contains("needs update") || !Db.Bool(c.Raw("SecurityClause")) || !Db.Bool(c.Raw("BreachNotificationClause"))) ? "Medium"
                : hasData ? "Medium" : "Low";
        }
        return Task.CompletedTask;
    }
}
