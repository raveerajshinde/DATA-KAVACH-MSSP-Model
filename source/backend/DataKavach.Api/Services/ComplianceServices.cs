using DataKavach.Auth;
using DataKavach.Data;

namespace DataKavach.Services;

public sealed record StatusCounts(int Compliant, int Partial, int NonCompliant, int NotAssessed, int NotApplicable)
{
    public int Applicable => Compliant + Partial + NonCompliant + NotAssessed;
    public int Assessed => Compliant + Partial + NonCompliant;
    /// <summary>Assessment indicator (not a certification): (C + 0.5 P) / assessed applicable items.</summary>
    public double? Indicator => Assessed == 0 ? null : Math.Round((Compliant + 0.5 * Partial) * 100.0 / Assessed, 1);
    public double Completion => Applicable == 0 ? 0 : Math.Round(Assessed * 100.0 / Applicable, 1);

    public static StatusCounts From(IEnumerable<Dictionary<string, object?>> items)
    {
        int c = 0, p = 0, n = 0, na = 0, x = 0;
        foreach (var i in items)
            switch (Db.Str(i.GetValueOrDefault("Status")))
            {
                case "Compliant": c++; break;
                case "Partially Compliant": p++; break;
                case "Non-Compliant": n++; break;
                case "Not Applicable": x++; break;
                default: na++; break;
            }
        return new StatusCounts(c, p, n, na, x);
    }
}

public sealed class AssessmentService(Db db, EntityService es, AuditService audit)
{
    public static bool IsApplicable(string? tag, Dictionary<string, object?> org) => (tag ?? "ALL") switch
    {
        "ALL" => true,
        "CONSENT" => Db.Bool(org.GetValueOrDefault("ReliesOnConsent")),
        "CHILDREN" => Db.Bool(org.GetValueOrDefault("ProcessesChildrenData")),
        "DISABILITY_GUARDIAN" => Db.Bool(org.GetValueOrDefault("ProcessesGuardianData")),
        "PROCESSOR" => Db.Bool(org.GetValueOrDefault("UsesProcessors")),
        "CROSS_BORDER" => Db.Bool(org.GetValueOrDefault("CrossBorderTransfers")),
        "THIRD_SCHEDULE" => (Db.Str(org.GetValueOrDefault("ThirdScheduleClass")) ?? "None") != "None",
        "CONSENT_MANAGER" => Db.Bool(org.GetValueOrDefault("UsesConsentManager")),
        "SDF" => Db.Bool(org.GetValueOrDefault("IsSignificantDataFiduciary")),
        "STATE" => Db.Bool(org.GetValueOrDefault("IsStateInstrumentality")),
        "RESEARCH" => Db.Bool(org.GetValueOrDefault("ResearchExemption")),
        _ => true
    };

    static string Reason(string tag) => tag switch
    {
        "CONSENT" => "Organisation profile: does not rely on consent",
        "CHILDREN" => "Organisation profile: does not process children's data",
        "DISABILITY_GUARDIAN" => "Organisation profile: no processing via lawful guardians",
        "PROCESSOR" => "Organisation profile: no Data Processors used",
        "CROSS_BORDER" => "Organisation profile: no cross-border transfers",
        "THIRD_SCHEDULE" => "Organisation profile: not a Third Schedule class",
        "CONSENT_MANAGER" => "Organisation profile: Consent Managers not used",
        "SDF" => "Organisation profile: not notified as Significant Data Fiduciary",
        "STATE" => "Organisation profile: not a State instrumentality",
        "RESEARCH" => "Organisation profile: research exemption not relied upon",
        _ => ""
    };

    public async Task<long> CreateAsync(CurrentUser u, long orgId, Dictionary<string, object?> vals)
    {
        var id = await es.CreateAsync(Schema.Get("assessments"), u, orgId, vals);
        vals.TryAdd("Status", "In Progress");
        var org = await es.GetRawAsync(Schema.Get("organizations"), orgId);
        var reqs = await db.QueryAsync($"SELECT * FROM {db.Q("Requirements")} WHERE {db.Q("IsDeleted")} = 0 AND {db.Q("IsActive")} = 1 ORDER BY {db.Q("SortOrder")}, {db.Q("Id")}");
        await using var c = await db.OpenAsync();
        using var tx = c.BeginTransaction();
        foreach (var r in reqs)
        {
            var tag = Db.Str(r["Applicability"]) ?? "ALL";
            var app = IsApplicable(tag, org);
            await db.InsertAsync("AssessmentItems", new Dictionary<string, object?>
            {
                ["OrganizationId"] = orgId, ["AssessmentId"] = id, ["RequirementId"] = Db.Long(r["Id"]), ["RequirementCode"] = Db.Str(r["Code"]),
                ["Status"] = app ? "Not Assessed" : "Not Applicable", ["RiskRating"] = Db.Str(r["DefaultRisk"]), ["IsApplicable"] = app,
                ["ApplicabilityNote"] = app ? null : Reason(tag) + " (auto - override if incorrect)",
                ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email, ["IsDeleted"] = false
            }, c, tx);
        }
        tx.Commit();
        await audit.LogAsync("GENERATE", "assessmentItems", id, orgId, new { items = reqs.Count }, u);
        return id;
    }

    /// <summary>Assessment items joined with their requirement text.</summary>
    public async Task<List<Dictionary<string, object?>>> ItemsAsync(long assessmentId)
    {
        var sql = $@"SELECT i.*, r.{db.Q("Module")} AS {db.Q("Module")}, r.{db.Q("Title")} AS {db.Q("Title")}, r.{db.Q("ActSection")} AS {db.Q("ActSection")}, r.{db.Q("Rule")} AS {db.Q("Rule")},
            r.{db.Q("LegalReference")} AS {db.Q("LegalReference")}, r.{db.Q("Description")} AS {db.Q("Description")}, r.{db.Q("Question")} AS {db.Q("Question")},
            r.{db.Q("Guidance")} AS {db.Q("Guidance")}, r.{db.Q("EvidenceRequired")} AS {db.Q("EvidenceRequired")}, r.{db.Q("Applicability")} AS {db.Q("Applicability")},
            r.{db.Q("EffectiveFrom")} AS {db.Q("EffectiveFrom")}, r.{db.Q("PenaltyReference")} AS {db.Q("PenaltyReference")}, r.{db.Q("SortOrder")} AS {db.Q("SortOrder")}
            FROM {db.Q("AssessmentItems")} i JOIN {db.Q("Requirements")} r ON r.{db.Q("Id")} = i.{db.Q("RequirementId")}
            WHERE i.{db.Q("AssessmentId")} = ? AND i.{db.Q("IsDeleted")} = 0 ORDER BY r.{db.Q("SortOrder")}, r.{db.Q("Id")}";
        var rows = await db.QueryAsync(sql, assessmentId);
        var ev = await db.QueryAsync($"SELECT {db.Q("LinkedId")}, COUNT(*) AS {db.Q("N")} FROM {db.Q("Evidence")} WHERE {db.Q("LinkedEntity")} = 'assessmentItems' AND {db.Q("IsDeleted")} = 0 GROUP BY {db.Q("LinkedId")}");
        var evMap = ev.ToDictionary(r => Db.Long(r["LinkedId"]), r => Db.Int(r["N"]));
        foreach (var r in rows) r["EvidenceCount"] = evMap.GetValueOrDefault(Db.Long(r["Id"]));
        return rows;
    }

    public async Task<long?> LatestAssessmentIdAsync(long orgId) =>
        Db.Long(await db.ScalarAsync(db.Page($"SELECT {db.Q("Id")} FROM {db.Q("Assessments")} WHERE {db.Q("OrganizationId")} = ? AND {db.Q("IsDeleted")} = 0 ORDER BY {db.Q("Id")} DESC", 0, 1), orgId)) is var x and > 0 ? x : null;
}

public sealed record Deadline(string Type, string? Title, DateTime? Due, string Link, bool Critical);

public sealed class DashboardService(Db db, EntityService es, AssessmentService assess)
{
    public static readonly DateTime CoreCommencement = new(2027, 5, 13);
    public static readonly DateTime ConsentManagerCommencement = new(2026, 11, 13);

    public async Task<object> BuildAsync(long orgId, long? assessmentId)
    {
        assessmentId ??= await assess.LatestAssessmentIdAsync(orgId);
        var items = assessmentId != null ? await assess.ItemsAsync(assessmentId.Value) : new();
        var counts = StatusCounts.From(items);
        var gaps = items.Where(i => Db.Str(i["Status"]) is "Non-Compliant" or "Partially Compliant").ToList();
        var risk = Opt.Risk.ToDictionary(r => r, r => gaps.Count(g => Db.Str(g["RiskRating"]) == r));
        var byModule = items.GroupBy(i => Db.Str(i["Module"]) ?? "Other").Select(g => { var sc = StatusCounts.From(g); return new { name = g.Key, indicator = sc.Indicator, compliant = sc.Compliant, partial = sc.Partial, nonCompliant = sc.NonCompliant, notAssessed = sc.NotAssessed, notApplicable = sc.NotApplicable }; }).ToList();
        var byDept = items.Where(i => Db.Str(i["Status"]) != "Not Applicable").GroupBy(i => Db.Str(i["Department"]) is { Length: > 0 } d ? d : "Unassigned")
            .Select(g => { var sc = StatusCounts.From(g); return new { name = g.Key, indicator = sc.Indicator, total = g.Count(), assessed = sc.Assessed }; }).OrderBy(x => x.name).ToList();
        var evidencePending = items.Count(i => Db.Str(i["Status"]) is "Compliant" or "Partially Compliant" && Db.Int(i["EvidenceCount"]) == 0);

        var actions = await es.AllAsync("actions", orgId);
        var today = DateTime.UtcNow.Date;
        bool Open(Dictionary<string, object?> a) => Db.Str(a["Status"]) is not ("Completed" or "Cancelled");
        var openActions = actions.Where(Open).ToList();
        var overdue = openActions.Where(a => Db.Dt(a["TargetDate"]) is { } d && d.Date < today).ToList();
        var remediation = Opt.ActionStatus.ToDictionary(s => s, s => actions.Count(a => Db.Str(a["Status"]) == s));

        var vendors = await es.AllAsync("vendors", orgId);
        var vendorRisk = Opt.Risk.ToDictionary(r => r, r => vendors.Count(v => Db.Str(v["RiskRating"]) == r));
        var noDpa = vendors.Count(v => Db.Bool(v["IsDataProcessor"]) && (Db.Str(v["DpaStatus"]) ?? "Not in place").StartsWith("Not in place"));

        var controls = await es.AllAsync("securityControls", orgId);
        var sec = StatusCounts.From(controls);

        var notifs = await db.QueryAsync($@"SELECT n.*, i.{db.Q("Reference")} AS {db.Q("IncidentRef")}, i.{db.Q("Title")} AS {db.Q("IncidentTitle")} FROM {db.Q("IncidentNotifications")} n
            JOIN {db.Q("Incidents")} i ON i.{db.Q("Id")} = n.{db.Q("IncidentId")} WHERE n.{db.Q("OrganizationId")} = ? AND n.{db.Q("IsDeleted")} = 0 AND n.{db.Q("Status")} = 'Pending' AND i.{db.Q("IsDeleted")} = 0 AND (i.{db.Q("IsDrill")} = 0 OR i.{db.Q("IsDrill")} IS NULL)", orgId);
        var requests = await es.AllAsync("rightsRequests", orgId);
        var openReq = requests.Where(r => Db.Str(r["Status"]) is not ("Completed" or "Rejected" or "Closed")).ToList();
        var incidents = await es.AllAsync("incidents", orgId);
        var consents = await es.AllAsync("consentRecords", orgId);
        var dpias = await es.AllAsync("dpias", orgId);

        var deadlines = new List<Deadline>();
        foreach (var n in notifs)
            deadlines.Add(new Deadline("Breach notification", $"{Db.Str(n["IncidentRef"])}: {Db.Str(n["RecipientName"])}", Db.Dt(n["DueAt"]), $"/incidents/{Db.Long(n["IncidentId"])}", true));
        foreach (var r in openReq.Where(r => Db.Dt(r["DueAt"]) is { } d && d <= today.AddDays(14)))
            deadlines.Add(new Deadline("Data principal request", $"{Db.Str(r["Reference"])} - {Db.Str(r["RequestType"])}", Db.Dt(r["DueAt"]), $"/rights/{Db.Long(r["Id"])}", false));
        foreach (var a in openActions.Where(a => Db.Dt(a["TargetDate"]) is { } d && d <= today.AddDays(14)))
            deadlines.Add(new Deadline("Remediation action", $"{Db.Str(a["GapId"])} - {Db.Str(a["Title"])}", Db.Dt(a["TargetDate"]), "/roadmap", false));
        foreach (var v in vendors.Where(v => Db.Dt(v["NextReview"]) is { } d && d <= today.AddDays(30)))
            deadlines.Add(new Deadline("Vendor review", Db.Str(v["Name"]), Db.Dt(v["NextReview"]), $"/vendors/{Db.Long(v["Id"])}", false));
        foreach (var d in dpias.Where(d => Db.Dt(d["NextReview"]) is { } x && x <= today.AddDays(30)))
            deadlines.Add(new Deadline("DPIA review", Db.Str(d["ProcessingActivity"]), Db.Dt(d["NextReview"]), $"/dpia/{Db.Long(d["Id"])}", false));
        if (CoreCommencement > today)
            deadlines.Add(new Deadline("Statutory", "DPDP core obligations in force (Rules 3, 5-16, 22, 23)", (DateTime?)CoreCommencement, "/assessments", true));

        return new
        {
            assessmentId,
            disclaimer = "Indicators reflect self-assessment status only. They are not a legal opinion, certification or proof of compliance.",
            commencement = new { core = CoreCommencement.ToString("yyyy-MM-dd"), daysToCore = (CoreCommencement - today).Days, consentManager = ConsentManagerCommencement.ToString("yyyy-MM-dd") },
            status = new { compliant = counts.Compliant, partial = counts.Partial, nonCompliant = counts.NonCompliant, notAssessed = counts.NotAssessed, notApplicable = counts.NotApplicable, applicable = counts.Applicable, assessed = counts.Assessed, indicator = counts.Indicator, completion = counts.Completion },
            risk,
            byModule,
            byDepartment = byDept,
            actions = new { open = openActions.Count, overdue = overdue.Count, total = actions.Count, byStatus = remediation, byPlan = new[] { "30-day", "60-day", "90-day", "Custom" }.ToDictionary(p => p, p => openActions.Count(a => Db.Str(a["Plan"]) == p)) },
            evidencePending,
            vendors = new { total = vendors.Count, byRisk = vendorRisk, processorsWithoutDpa = noDpa },
            security = new { compliant = sec.Compliant, partial = sec.Partial, nonCompliant = sec.NonCompliant, notAssessed = sec.NotAssessed, validated = controls.Count(c => Db.Bool(c["Validated"])), total = controls.Count },
            rights = new { open = openReq.Count, overdue = openReq.Count(r => Db.Dt(r["DueAt"]) is { } d && d < DateTime.UtcNow), total = requests.Count },
            incidents = new { open = incidents.Count(i => Db.Str(i["Status"]) != "Closed"), breaches = incidents.Count(i => Db.Bool(i["IsPersonalDataBreach"]) && !Db.Bool(i["IsDrill"])), overdueNotifications = notifs.Count(n => Db.Dt(n["DueAt"]) is { } d && d < DateTime.UtcNow) },
            consent = new { given = consents.Count(c => Db.Str(c["Status"]) == "Given"), withdrawn = consents.Count(c => Db.Str(c["Status"]) == "Withdrawn") },
            dpia = new { total = dpias.Count, approved = dpias.Count(d => Db.Str(d["Status"]) == "Approved") },
            deadlines = deadlines.OrderBy(d => d.Due).Take(25).ToList()
        };
    }
}

public sealed class RoadmapService(Db db, EntityService es, AssessmentService assess)
{
    static (string priority, string plan, int days) Prioritise(string? risk, string? module)
    {
        // Penalty exposure first: security safeguards and breach handling (Schedule items 1 and 2) are prioritised.
        var critical = module is "Security Safeguards" or "Breach Management";
        return risk switch
        {
            "High" when critical => ("P1 - Critical", "30-day", 30),
            "High" => ("P2 - High", "30-day", 30),
            "Medium" when critical => ("P2 - High", "60-day", 60),
            "Medium" => ("P3 - Medium", "60-day", 60),
            _ => ("P4 - Low", "90-day", 90)
        };
    }

    public async Task<object> GenerateAsync(CurrentUser u, long orgId, long? assessmentId)
    {
        var def = Schema.Get("actions");
        await es.Require(u, "roadmap.edit");
        var existing = (await es.AllAsync("actions", orgId)).Select(a => $"{Db.Str(a["SourceType"])}:{Db.Long(a["SourceId"])}").ToHashSet();
        int created = 0;
        async Task Add(string srcType, long srcId, string reqRef, string title, string? details, string? risk, string? module, string? dept, string? owner, DateTime? target)
        {
            if (existing.Contains($"{srcType}:{srcId}")) return;
            var (prio, plan, days) = Prioritise(risk, module);
            var t = target ?? DateTime.UtcNow.Date.AddDays(days);
            await es.CreateAsync(def, u, orgId, new Dictionary<string, object?>
            {
                ["SourceType"] = srcType, ["SourceId"] = srcId, ["RequirementRef"] = reqRef, ["Title"] = title.Length > 300 ? title[..297] + "..." : title, ["Details"] = details,
                ["RiskRating"] = risk ?? "Medium", ["Priority"] = prio, ["Plan"] = plan, ["Department"] = dept, ["Owner"] = owner, ["TargetDate"] = DateOnly.FromDateTime(t), ["Status"] = "Open"
            }, system: true);
            created++;
        }

        assessmentId ??= await assess.LatestAssessmentIdAsync(orgId);
        if (assessmentId != null)
            foreach (var i in (await assess.ItemsAsync(assessmentId.Value)).Where(i => Db.Str(i["Status"]) is "Non-Compliant" or "Partially Compliant"))
            {
                var rec = Db.Str(i["Recommendation"]) is { Length: > 0 } r ? r : Db.Str(i["Guidance"]) ?? "";
                await Add("assessmentItem", Db.Long(i["Id"]), $"{Db.Str(i["RequirementCode"])} ({Db.Str(i["LegalReference"])})", $"{Db.Str(i["Title"])}: {FirstSentence(rec)}",
                    $"Gap: {Db.Str(i["GapDescription"]) ?? Db.Str(i["Status"])}\nRecommendation: {rec}", Db.Str(i["RiskRating"]), Db.Str(i["Module"]), Db.Str(i["Department"]), Db.Str(i["Owner"]), Db.Dt(i["TargetDate"]));
            }
        foreach (var c in (await es.AllAsync("securityControls", orgId)).Where(c => Db.Str(c["Status"]) is "Non-Compliant" or "Partially Compliant"))
            await Add("securityControl", Db.Long(c["Id"]), $"{Db.Str(c["ControlKey"])} ({Db.Str(c["RuleReference"])})", $"Strengthen and validate: {Db.Str(c["ControlName"])}",
                Db.Str(c["Recommendation"]) ?? Db.Str(c["Expectation"]), Db.Str(c["RiskRating"]) ?? "High", "Security Safeguards", "Information Security", Db.Str(c["Owner"]), null);
        foreach (var v in (await es.AllAsync("vendors", orgId)).Where(v => Db.Bool(v["IsDataProcessor"]) && ((Db.Str(v["DpaStatus"]) ?? "Not in place").StartsWith("Not in place") || (Db.Str(v["DpaStatus"]) ?? "").Contains("needs update") || !Db.Bool(v["SecurityClause"]))))
            await Add("vendor", Db.Long(v["Id"]), "VEN-01 / SEC-06 (s.8(2), Rule 6(1)(f))", $"Execute DPDP-compliant DPA with {Db.Str(v["Name"])}",
                "Include security safeguards, breach notification to the Fiduciary, erasure on instruction, sub-processor control and audit rights.", "High", "Processors & Vendors", "Legal & Compliance", null, null);
        foreach (var r in await db.QueryAsync($@"SELECT r.*, d.{db.Q("ProcessingActivity")} AS {db.Q("Activity")} FROM {db.Q("DpiaRisks")} r JOIN {db.Q("Dpias")} d ON d.{db.Q("Id")} = r.{db.Q("DpiaId")}
                WHERE r.{db.Q("OrganizationId")} = ? AND r.{db.Q("IsDeleted")} = 0 AND d.{db.Q("IsDeleted")} = 0 AND (r.{db.Q("ResidualRating")} = 'High' OR (r.{db.Q("ResidualRating")} IS NULL AND r.{db.Q("InherentRating")} = 'High'))", orgId))
            await Add("dpiaRisk", Db.Long(r["Id"]), $"DPIA: {Db.Str(r["Activity"])}", $"Mitigate {Db.Str(r["RiskType"])} risk - {Db.Str(r["Activity"])}", Db.Str(r["MitigationPlan"]), "High", "DPIA", null, Db.Str(r["Owner"]), Db.Dt(r["DueDate"]));
        return new { created };
    }

    static string FirstSentence(string s)
    {
        var i = s.IndexOf(". ", StringComparison.Ordinal);
        return i > 0 && i < 200 ? s[..i] : s.Length > 200 ? s[..200] : s;
    }
}
