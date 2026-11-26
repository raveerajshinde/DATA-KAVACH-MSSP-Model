using DataKavach.Data;
using DataKavach.Services;

namespace DataKavach.Reports;

public sealed record ReportRequest(long OrgId, string ReportType, long? AssessmentId, DateTime? PeriodFrom, DateTime? PeriodTo, long? EntityId);

/// <summary>Builds organisation-specific, editable report content from live compliance data.</summary>
public sealed class ReportBuilder(Db db, EntityService es, AssessmentService assess, BreachService breach)
{
    public static readonly (string key, string title)[] Types =
    {
        ("executive-summary", "DPDP Executive Summary"), ("gap-assessment", "DPDP Compliance Gap Assessment"), ("data-inventory", "Personal Data Inventory"),
        ("processing-register", "Data Processing Register"), ("data-flow", "Data Flow Mapping"), ("dpia", "Data Protection Impact Assessment Report"),
        ("consent", "Consent Management Assessment"), ("privacy-policy", "Privacy Policy"), ("data-protection-policy", "Data Protection Policy"),
        ("vendor-risk", "Vendor Risk Assessment"), ("security", "Security Safeguards Assessment"), ("rights", "Data Principal Rights Report"),
        ("breach-readiness", "Breach Readiness Assessment"), ("incident", "Incident and Breach Report"), ("roadmap", "Compliance Remediation Roadmap"),
        ("master", "DPDP Master Compliance Report"),
    };

    // ------------------------------------------------------------------ block helpers
    static Block P(string? t) => new() { Type = "p", Text = t };
    static Block H2(string t) => new() { Type = "h2", Text = t };
    static Block Bul(IEnumerable<string> items, string? title = null) => new() { Type = "bullets", Items = items.ToList(), Title = title };
    static Block Tbl(IEnumerable<string> cols, IEnumerable<IEnumerable<string?>> rows, IEnumerable<double>? w = null, string? title = null) =>
        new() { Type = "table", Columns = cols.ToList(), Rows = rows.Select(r => r.Select(x => x ?? "").ToList()).ToList(), Widths = w?.ToList(), Title = title };
    static Block Kv(IEnumerable<(string k, string v)> rows, string? title = null) => new() { Type = "kv", Rows = rows.Select(r => new List<string> { r.k, r.v ?? "-" }).ToList(), Title = title };
    static Block Chart(string type, string title, IEnumerable<(string label, double value)> pts) => new() { Type = "chart", ChartType = type, Title = title, Data = pts.Select(p => new ChartPoint { Label = p.label, Value = p.value }).ToList() };
    static Block Note(string title, string text, string tone = "info") => new() { Type = "callout", Title = title, Text = text, Tone = tone };
    static Section Sec(string id, string heading, params Block[] blocks) => new() { Id = id, Heading = heading, Blocks = blocks.ToList() };
    static Section Sec(string id, string heading, List<Block> blocks, int level = 1) => new() { Id = id, Heading = heading, Blocks = blocks, Level = level };

    static string S(Dictionary<string, object?> r, string k) => Db.Str(r.GetValueOrDefault(k)) ?? "";
    static string D(object? v) => Db.Dt(v)?.ToString("dd MMM yyyy") ?? "";
    static string DT(object? v) => Db.Dt(v) is { } d ? d.ToString("dd MMM yyyy HH:mm") + " UTC" : "";
    static string YN(object? v) => Db.Bool(v) ? "Yes" : "No";
    static string Mask(string s) => s.Length <= 2 ? "**" : s[..1] + new string('*', Math.Min(6, s.Length - 2)) + s[^1];

    // ------------------------------------------------------------------ data loading
    sealed class Ctx
    {
        public required Dictionary<string, object?> Org;
        public long? AssessmentId;
        public List<Dictionary<string, object?>> Items = new();
        public StatusCounts Counts = new(0, 0, 0, 0, 0);
        public DateTime? From, To;
    }

    async Task<Ctx> Load(ReportRequest r)
    {
        var org = await es.GetRawAsync(Schema.Get("organizations"), r.OrgId);
        var aid = r.AssessmentId ?? await assess.LatestAssessmentIdAsync(r.OrgId);
        var items = aid != null ? await assess.ItemsAsync(aid.Value) : new();
        return new Ctx { Org = org, AssessmentId = aid, Items = items, Counts = StatusCounts.From(items), From = r.PeriodFrom, To = r.PeriodTo };
    }

    bool InPeriod(Ctx c, object? v)
    {
        var d = Db.Dt(v);
        if (d == null) return true;
        return (c.From == null || d >= c.From) && (c.To == null || d <= c.To.Value.AddDays(1));
    }

    public async Task<ReportDoc> BuildAsync(ReportRequest r)
    {
        var c = await Load(r);
        var title = Types.FirstOrDefault(t => t.key == r.ReportType).title ?? throw ApiError.Bad("Unknown report type");
        var doc = new ReportDoc { ReportType = r.ReportType, Title = title, Subtitle = $"{S(c.Org, "Name")} - Digital Personal Data Protection Act, 2023 and DPDP Rules, 2025" };
        doc.Sections = r.ReportType switch
        {
            "executive-summary" => await ExecSummary(c),
            "gap-assessment" => Intro(c, "gap").Concat(GapSections(c)).ToList(),
            "data-inventory" => await Inventory(c),
            "processing-register" => await ProcessingRegister(c),
            "data-flow" => await DataFlows(c),
            "dpia" => await Dpia(c, r.EntityId),
            "consent" => await Consent(c),
            "privacy-policy" => await PolicyDoc(c, new[] { "privacy-notice", "customer-privacy-notice" }, "Privacy Policy"),
            "data-protection-policy" => await PolicyDoc(c, new[] { "data-protection-policy" }, "Data Protection Policy"),
            "vendor-risk" => await Vendors(c),
            "security" => await Security(c),
            "rights" => await Rights(c),
            "breach-readiness" => await BreachReadiness(c),
            "incident" => await Incidents(c, r.EntityId),
            "roadmap" => await Roadmap(c),
            "master" => await Master(c),
            _ => throw ApiError.Bad("Unknown report type")
        };
        doc.Sections.Add(new Section { Id = "signoff", Heading = "Sign-off", Blocks = new() { P("The undersigned have reviewed this report. Approval records acceptance of the findings and the remediation plan; it does not constitute a legal opinion or certification of compliance."), new Block { Type = "signature" } } });
        return doc;
    }

    // ------------------------------------------------------------------ common sections
    List<Section> Intro(Ctx c, string kind)
    {
        var o = c.Org;
        var applic = new List<string>();
        if (Db.Bool(o["IsSignificantDataFiduciary"])) applic.Add("Notified as a Significant Data Fiduciary (s.10 and Rule 13 obligations apply)");
        else applic.Add("Not notified as a Significant Data Fiduciary - DPIA and independent audit are good practice but not mandatory under s.10");
        if (Db.Bool(o["ProcessesChildrenData"])) applic.Add("Processes personal data of children - s.9 and Rule 10 apply");
        if (Db.Bool(o["ReliesOnConsent"])) applic.Add("Relies on consent for some processing - notice and consent obligations (s.5, s.6, Rule 3) apply");
        if (Db.Bool(o["UsesProcessors"])) applic.Add("Engages Data Processors - contractual and oversight obligations (s.8(1), s.8(2), Rule 6(1)(f)) apply");
        if (Db.Bool(o["CrossBorderTransfers"])) applic.Add("Transfers personal data outside India - s.16 and Rule 15 apply");
        if (S(o, "ThirdScheduleClass") is { Length: > 0 } ts && ts != "None") applic.Add($"Third Schedule class: {ts} - Rule 8 time-based erasure applies");
        return new()
        {
            Sec("scope", "Scope and organisation profile",
                P($"This report covers {S(o, "Name")}'s processing of digital personal data for the period {Period(c)}. The assessment was performed against the Digital Personal Data Protection Act, 2023 (\"the Act\") and the Digital Personal Data Protection Rules, 2025 notified on 13 November 2025 (\"the Rules\")."),
                Kv(new[] { ("Organisation", S(o, "Name")), ("Registered office", S(o, "RegisteredAddress")), ("CIN", S(o, "Cin")), ("GSTIN", S(o, "Gstin")), ("Industry", S(o, "Industry")),
                    ("Employees / locations", $"{S(o, "EmployeeCount")} / {S(o, "LocationCount")}"), ("Website", S(o, "Website")),
                    ("DPO / privacy contact", $"{S(o, "DpoName")} {S(o, "DpoDesignation")} {S(o, "DpoEmail")}".Trim()), ("Grievance response period", $"{S(o, "GrievanceResponseDays")} days") }),
                Bul(applic, "Applicability determined from the organisation profile")),
            Sec("method", "Methodology and commencement",
                P("Each applicable requirement in the Data Kavach DPDP requirement library was assessed through interviews, document review and evidence inspection and rated Compliant, Partially Compliant, Non-Compliant, Not Assessed or Not Applicable. Gaps are risk-rated High, Medium or Low considering penalty exposure under the Schedule to the Act and likelihood of harm to Data Principals."),
                Tbl(new[] { "Provisions", "In force from" }, new[]
                {
                    new[] { "Rules 1, 2 and 17-21 (Board constitution and procedure)", "13 November 2025" },
                    new[] { "Rule 4 (Consent Managers) and Act s.6(9)", "13 November 2026" },
                    new[] { "Rules 3, 5-16, 22, 23 and remaining substantive obligations of the Act", "13 May 2027" },
                }, new[] { 3.0, 1.2 }),
                Note("Basis of indicators", "Percentages in this report are assessment indicators: (Compliant + 0.5 x Partially Compliant) / assessed applicable requirements. They are not a certification or proof of compliance.", "warning"))
        };
    }

    static string Period(Ctx c)
    {
        var f = Db.Dt(c.From) ?? Db.Dt(c.Org.GetValueOrDefault("PeriodFrom"));
        var t = Db.Dt(c.To) ?? Db.Dt(c.Org.GetValueOrDefault("PeriodTo"));
        return f != null || t != null ? $"{f:dd MMM yyyy} to {t:dd MMM yyyy}" : "as at the assessment date";
    }

    Block StatusChart(StatusCounts s, string title = "Compliance status distribution") =>
        Chart("pie", title, new[] { ("Compliant", (double)s.Compliant), ("Partially Compliant", s.Partial), ("Non-Compliant", s.NonCompliant), ("Not Assessed", s.NotAssessed), ("Not Applicable", s.NotApplicable) });

    Block StatusTable(StatusCounts s) => Tbl(new[] { "Status", "Requirements", "Share of applicable" }, new[]
    {
        new[] { "Compliant", s.Compliant.ToString(), Pct(s.Compliant, s.Applicable) }, new[] { "Partially Compliant", s.Partial.ToString(), Pct(s.Partial, s.Applicable) },
        new[] { "Non-Compliant", s.NonCompliant.ToString(), Pct(s.NonCompliant, s.Applicable) }, new[] { "Not Assessed", s.NotAssessed.ToString(), Pct(s.NotAssessed, s.Applicable) },
        new[] { "Not Applicable", s.NotApplicable.ToString(), "-" }, new[] { "Assessment indicator", s.Indicator is { } ind ? $"{ind:0.0}%" : "n/a", $"Completion {s.Completion:0.0}%" }
    }, new[] { 2.0, 1, 1.4 });

    static string Pct(int a, int b) => b == 0 ? "-" : $"{a * 100.0 / b:0.0}%";

    Block ModuleChart(Ctx c) => new()
    {
        Type = "chart", ChartType = "stacked", Title = "Status by compliance area", Series = new() { "Compliant", "Partially Compliant", "Non-Compliant", "Not Assessed" },
        Data = c.Items.Where(i => S(i, "Status") != "Not Applicable").GroupBy(i => S(i, "Module")).Select(g =>
        {
            var s = StatusCounts.From(g);
            return new ChartPoint { Label = g.Key, Values = new() { s.Compliant, s.Partial, s.NonCompliant, s.NotAssessed } };
        }).ToList()
    };

    Block RiskChart(Ctx c)
    {
        var gaps = c.Items.Where(i => S(i, "Status") is "Non-Compliant" or "Partially Compliant").ToList();
        return Chart("bar", "Open gaps by risk rating", Opt.Risk.Select(r => (r, (double)gaps.Count(g => S(g, "RiskRating") == r))));
    }

    List<Section> GapSections(Ctx c)
    {
        var list = new List<Section>();
        if (c.AssessmentId == null) { list.Add(Sec("gap-none", "Assessment results", Note("No assessment", "No compliance assessment has been created for this organisation yet.", "warning"))); return list; }
        list.Add(Sec("gap-summary", "Summary of assessment results", P($"{c.Counts.Applicable} requirements were applicable; {c.Counts.Assessed} have been assessed ({c.Counts.Completion:0.0}% complete)."), StatusTable(c.Counts), StatusChart(c.Counts), ModuleChart(c), RiskChart(c)));
        var bymod = new List<Block>();
        foreach (var g in c.Items.GroupBy(i => S(i, "Module")))
        {
            bymod.Add(H2(g.Key));
            bymod.Add(Tbl(new[] { "ID", "Requirement", "Legal reference", "Status", "Risk", "Owner / target" },
                g.Select(i => new[] { S(i, "RequirementCode"), S(i, "Title"), S(i, "LegalReference"), S(i, "Status"), S(i, "Status") is "Not Applicable" or "Compliant" or "Not Assessed" ? "" : S(i, "RiskRating"), $"{S(i, "Owner")} {D(i["TargetDate"])}".Trim() }),
                new[] { 0.8, 3.0, 1.8, 1.3, 0.8, 1.4 }));
        }
        list.Add(Sec("gap-register", "Requirement-wise results", bymod));
        var findings = new List<Block>();
        var gaps = c.Items.Where(i => S(i, "Status") is "Non-Compliant" or "Partially Compliant").OrderBy(i => Array.IndexOf(Opt.Risk, S(i, "RiskRating"))).ToList();
        if (gaps.Count == 0) findings.Add(P("No Non-Compliant or Partially Compliant requirements were recorded."));
        foreach (var i in gaps)
        {
            findings.Add(H2($"{S(i, "RequirementCode")} - {S(i, "Title")}"));
            findings.Add(Kv(new[]
            {
                ("Legal reference", S(i, "LegalReference")), ("Status / risk", $"{S(i, "Status")} / {S(i, "RiskRating")} risk"), ("Requirement", S(i, "Description")),
                ("Observation", S(i, "Response") is { Length: > 0 } x ? x : "-"), ("Gap", S(i, "GapDescription") is { Length: > 0 } g ? g : "Requirement not fully met."),
                ("Recommendation", S(i, "Recommendation") is { Length: > 0 } rr ? rr : S(i, "Guidance")), ("Evidence expected", S(i, "EvidenceRequired")),
                ("Evidence on file", Db.Int(i["EvidenceCount"]) > 0 ? $"{Db.Int(i["EvidenceCount"])} item(s) in evidence repository" : "None"),
                ("Penalty exposure", S(i, "PenaltyReference")), ("Owner / department / target", $"{S(i, "Owner")} / {S(i, "Department")} / {D(i["TargetDate"])}")
            }));
        }
        list.Add(Sec("gap-findings", "Detailed findings and recommendations", findings));
        return list;
    }

    async Task<List<Section>> ExecSummary(Ctx c)
    {
        var o = c.Org;
        var s = c.Counts;
        var gaps = c.Items.Where(i => S(i, "Status") is "Non-Compliant" or "Partially Compliant").ToList();
        var high = gaps.Where(g => S(g, "RiskRating") == "High").ToList();
        var actions = await es.AllAsync("actions", Db.Long(o["Id"]));
        var open = actions.Where(a => S(a, "Status") is not ("Completed" or "Cancelled")).ToList();
        var days = (DashboardService.CoreCommencement - DateTime.UtcNow.Date).Days;
        var overview = s.Assessed == 0
            ? $"The DPDP assessment for {S(o, "Name")} has been initiated; {s.Applicable} requirements are applicable and none has yet been assessed."
            : $"{S(o, "Name")} has assessed {s.Assessed} of {s.Applicable} applicable DPDP requirements. {s.Compliant} are compliant, {s.Partial} partially compliant and {s.NonCompliant} non-compliant, giving an assessment indicator of {s.Indicator:0.0}%. {high.Count} open gap(s) are rated High risk.";
        var list = new List<Section>
        {
            Sec("exec-overview", "Overview", P(overview),
                days > 0 ? Note("Time to commencement", $"The substantive obligations of the Act and Rules 3, 5-16, 22 and 23 come into force on 13 May 2027 - {days} days from the date of this report.", "warning") : Note("Obligations in force", "The substantive DPDP obligations are in force.", "danger"),
                StatusTable(s), StatusChart(s)),
            Sec("exec-risk", "Key risks", RiskChart(c),
                Tbl(new[] { "ID", "Gap", "Risk", "Penalty exposure", "Owner" }, high.Concat(gaps.Where(g => S(g, "RiskRating") != "High")).Take(10).Select(i => new[] { S(i, "RequirementCode"), S(i, "Title"), S(i, "RiskRating"), S(i, "PenaltyReference"), S(i, "Owner") }), new[] { 0.8, 3.2, 0.8, 2.4, 1.2 }, "Top gaps"),
                Note("Priority", "Security safeguards (s.8(5), up to Rs 250 crore) and breach intimation (s.8(6), up to Rs 200 crore) carry the highest penalty exposure and should be remediated first.", "danger")),
            Sec("exec-actions", "Remediation status", ModuleChart(c),
                Tbl(new[] { "Plan", "Open actions", "Overdue" }, new[] { "30-day", "60-day", "90-day", "Custom" }.Select(p => new[] { p, open.Count(a => S(a, "Plan") == p).ToString(), open.Count(a => S(a, "Plan") == p && Db.Dt(a["TargetDate"]) < DateTime.UtcNow.Date).ToString() }), new[] { 2.0, 1, 1 }),
                Bul(new[]
                {
                    "Confirm a single accountable DPDP owner with board visibility.",
                    "Complete the personal data inventory and data-flow map; it underpins notices, rights handling and breach response.",
                    "Close High-risk gaps on security safeguards and breach readiness within 30 days; run a timed breach drill against the 72-hour Board deadline.",
                    "Amend existing HR, IT security, vendor and retention policies with DPDP clauses rather than creating parallel documents.",
                    "Execute DPDP-compliant processing agreements with all Data Processors.",
                }, "Recommended next steps"))
        };
        return list;
    }

    async Task<List<Section>> Inventory(Ctx c)
    {
        var rows = (await es.AllAsync("inventory", Db.Long(c.Org["Id"]))).ToList();
        var byGround = rows.GroupBy(r => S(r, "LegalGround") is { Length: > 0 } g ? g : "Not recorded").Select(g => (g.Key, (double)g.Count()));
        return new()
        {
            Sec("inv-summary", "Inventory summary",
                P($"{rows.Count} processing activities are recorded. {rows.Count(r => Db.Bool(r["CrossBorder"]))} involve cross-border transfers and {rows.Count(r => Db.Bool(r["IncludesChildrenData"]))} include children's data."),
                Chart("hbar", "Processing activities by permitted ground", byGround),
                rows.Any(r => string.IsNullOrWhiteSpace(S(r, "LegalGround"))) ? Note("Gap", "Some activities have no recorded legal ground (s.4). Record consent or the specific s.7 legitimate use for each.", "warning") : P("Every activity has a recorded permitted ground.")),
            Sec("inv-register", "Personal data inventory",
                Tbl(new[] { "Processing activity", "Data principals", "Personal data", "Purpose", "Ground", "System / location", "Retention" },
                    rows.Select(r => new[] { S(r, "ProcessingActivity"), S(r, "PrincipalCategory"), S(r, "DataCategories"), S(r, "Purpose"), S(r, "LegalGround"), $"{S(r, "SystemName")} {S(r, "StorageLocation")}".Trim(), S(r, "RetentionPeriod") }),
                    new[] { 1.6, 1.1, 1.6, 1.6, 1.2, 1.3, 1.0 })),
            Sec("inv-sharing", "Recipients, processors and transfers",
                Tbl(new[] { "Activity", "Internal recipients", "External processors", "Cross-border", "Safeguards" },
                    rows.Select(r => new[] { S(r, "ProcessingActivity"), S(r, "InternalRecipients"), S(r, "ExternalProcessors"), Db.Bool(r["CrossBorder"]) ? "Yes - " + S(r, "CrossBorderDetails") : "No", S(r, "SecuritySafeguards") }),
                    new[] { 1.5, 1.4, 1.5, 1.1, 2.0 }))
        };
    }

    async Task<List<Section>> ProcessingRegister(Ctx c)
    {
        var rows = await es.AllAsync("inventory", Db.Long(c.Org["Id"]));
        var blocks = new List<Block> { P($"Record of processing for {S(c.Org, "Name")} (Data Fiduciary). Contact: {S(c.Org, "DpoName")} {S(c.Org, "DpoEmail")}.") };
        int n = 0;
        foreach (var r in rows)
        {
            blocks.Add(H2($"{++n}. {S(r, "ProcessingActivity")}"));
            blocks.Add(Kv(new[]
            {
                ("Department / owner", $"{S(r, "Department")} / {S(r, "BusinessOwner")}"), ("Categories of Data Principals", S(r, "PrincipalCategory")), ("Personal data", S(r, "DataCategories")),
                ("Children's data", YN(r["IncludesChildrenData"])), ("Source", S(r, "CollectionSource")), ("Purpose", S(r, "Purpose")), ("Permitted ground", S(r, "LegalGround")),
                ("Systems / storage / cloud", $"{S(r, "SystemName")} / {S(r, "StorageLocation")} / {S(r, "CloudProvider")}"), ("Access roles", S(r, "AccessRoles")),
                ("Recipients and processors", $"{S(r, "InternalRecipients")}; {S(r, "ExternalProcessors")}"), ("Cross-border transfer", Db.Bool(r["CrossBorder"]) ? S(r, "CrossBorderDetails") : "None"),
                ("Retention / erasure", $"{S(r, "RetentionPeriod")} / {S(r, "ErasureMethod")}"), ("Security safeguards", S(r, "SecuritySafeguards"))
            }));
        }
        if (rows.Count == 0) blocks.Add(Note("No records", "No processing activities have been recorded in the data inventory.", "warning"));
        return new() { Sec("ropa", "Register of processing activities", blocks) };
    }

    async Task<List<Section>> DataFlows(Ctx c)
    {
        var flows = await es.AllAsync("dataflows", Db.Long(c.Org["Id"]));
        return new()
        {
            Sec("flow-summary", "Data flow overview",
                P($"{flows.Count} data flows recorded: {flows.Count(f => S(f, "DestinationType") == "Data Processor")} to Data Processors, {flows.Count(f => Db.Bool(f["CrossBorder"]))} cross-border and {flows.Count(f => !Db.Bool(f["Encrypted"]))} not encrypted in transit."),
                Chart("bar", "Flows by destination type", flows.GroupBy(f => S(f, "DestinationType") is { Length: > 0 } t ? t : "Unspecified").Select(g => (g.Key, (double)g.Count()))),
                flows.Any(f => !Db.Bool(f["Encrypted"])) ? Note("Rule 6(1)(a)", "Flows without encryption in transit should be secured or justified.", "warning") : P("All recorded flows are encrypted in transit.")),
            Sec("flow-register", "Data flow register",
                Tbl(new[] { "Flow", "Source", "Destination", "Data", "Method", "Encrypted", "Cross-border" },
                    flows.Select(f => new[] { S(f, "Name"), S(f, "SourceSystem"), $"{S(f, "DestinationSystem")} ({S(f, "DestinationType")})", S(f, "DataCategories"), $"{S(f, "TransferMethod")} / {S(f, "Frequency")}", YN(f["Encrypted"]), Db.Bool(f["CrossBorder"]) ? S(f, "Country") : "No" }),
                    new[] { 1.4, 1.2, 1.5, 1.5, 1.1, 0.8, 0.9 }))
        };
    }

    async Task<List<Section>> Dpia(Ctx c, long? id)
    {
        var orgId = Db.Long(c.Org["Id"]);
        var dpias = await es.AllAsync("dpias", orgId);
        if (id != null) dpias = dpias.Where(d => Db.Long(d["Id"]) == id).ToList();
        var list = new List<Section>
        {
            Sec("dpia-applicability", "Applicability",
                Db.Bool(c.Org["IsSignificantDataFiduciary"])
                    ? Note("Mandatory", "As a notified Significant Data Fiduciary, the organisation must undertake a DPIA and audit once every 12 months and furnish significant observations to the Board (s.10(2)(c), Rule 13(1)-(2)).", "danger")
                    : Note("Voluntary good practice", "The organisation is not notified as a Significant Data Fiduciary. A DPIA is not mandatory under the Act for it; this assessment is performed as good practice for higher-risk processing.", "info"))
        };
        foreach (var d in dpias)
        {
            var risks = await db.QueryAsync($"SELECT * FROM {db.Q("DpiaRisks")} WHERE {db.Q("DpiaId")} = ? AND {db.Q("IsDeleted")} = 0 ORDER BY {db.Q("Id")}", Db.Long(d["Id"]));
            var heat = new List<List<string>>();
            for (int imp = 5; imp >= 1; imp--)
            {
                var row = new List<string> { $"Impact {imp}" };
                for (int lik = 1; lik <= 5; lik++) row.Add(risks.Count(r => Db.Int(r["Impact"]) == imp && Db.Int(r["Likelihood"]) == lik) is var n and > 0 ? n.ToString() : "");
                heat.Add(row);
            }
            list.Add(Sec($"dpia-{d["Id"]}", $"DPIA: {S(d, "ProcessingActivity")}", new List<Block>
            {
                Kv(new[] { ("Trigger", S(d, "Trigger")), ("Purpose", S(d, "Purpose")), ("Rights of Data Principals considered", S(d, "RightsDescription")), ("Personal data", S(d, "DataCategories")), ("Data principals", S(d, "PrincipalCategories")),
                    ("Volume", S(d, "Volume")), ("Systems", S(d, "Systems")), ("Necessity and proportionality", S(d, "Necessity")), ("Owner / approver", $"{S(d, "Owner")} / {S(d, "Approver")}"),
                    ("Status", S(d, "Status") is { Length: > 0 } st ? st : "Draft"), ("Overall residual risk", S(d, "OverallResidualRisk")), ("Approved", S(d, "ApprovedBy") is { Length: > 0 } ab ? $"{ab} on {D(d["ApprovedAt"])}" : "Not approved") }),
                Tbl(new[] { "Risk", "Description", "L x I", "Inherent", "Existing controls", "Residual", "Mitigation / owner / due" },
                    risks.Select(r => new[] { S(r, "RiskType"), S(r, "Description"), $"{S(r, "Likelihood")} x {S(r, "Impact")}", S(r, "InherentRating"), S(r, "ExistingControls"), S(r, "ResidualRating"), $"{S(r, "MitigationPlan")} ({S(r, "Owner")}, {D(r["DueDate"])})" }),
                    new[] { 1.2, 1.8, 0.6, 0.8, 1.6, 0.8, 2.0 }, "Risk register"),
                Tbl(new[] { "", "L1", "L2", "L3", "L4", "L5" }, heat, new[] { 1.2, 1, 1, 1, 1, 1 }, "Inherent risk heat map (count of risks)"),
                Kv(new[] { ("Conclusion", S(d, "Conclusion")), ("Next review", D(d["NextReview"])) })
            }));
        }
        if (dpias.Count == 0) list.Add(Sec("dpia-none", "Assessments", P("No DPIAs have been recorded.")));
        return list;
    }

    List<Block> ItemsFor(Ctx c, params string[] prefixes)
    {
        var items = c.Items.Where(i => prefixes.Any(p => S(i, "RequirementCode").StartsWith(p))).ToList();
        if (items.Count == 0) return new() { P("No assessment results recorded for this area.") };
        return new()
        {
            Tbl(new[] { "ID", "Requirement", "Reference", "Status", "Risk", "Gap / recommendation" },
                items.Select(i => new[] { S(i, "RequirementCode"), S(i, "Title"), S(i, "LegalReference"), S(i, "Status"), S(i, "Status") is "Compliant" or "Not Applicable" or "Not Assessed" ? "" : S(i, "RiskRating"), $"{S(i, "GapDescription")} {S(i, "Recommendation")}".Trim() }),
                new[] { 0.8, 2.2, 1.5, 1.2, 0.7, 2.6 })
        };
    }

    async Task<List<Section>> Consent(Ctx c)
    {
        var orgId = Db.Long(c.Org["Id"]);
        var notices = await es.AllAsync("consentNotices", orgId);
        var recs = (await es.AllAsync("consentRecords", orgId)).Where(r => InPeriod(c, r["GivenAt"])).ToList();
        var purposes = recs.GroupBy(r => S(r, "Purpose")).Select(g => new[] { g.Key, g.Count(x => S(x, "Status") == "Given").ToString(), g.Count(x => S(x, "Status") == "Withdrawn").ToString(), g.Select(x => S(x, "Channel")).Distinct().Aggregate((a, b) => a + ", " + b) });
        return new()
        {
            Sec("consent-results", "Assessment of notice and consent obligations", ItemsFor(c, "NTC", "CON")),
            Sec("consent-notices", "Consent notices", Tbl(new[] { "Notice", "Version", "Language", "Collection point", "Withdrawal link", "Status" },
                notices.Select(n => new[] { S(n, "Title"), S(n, "Version"), S(n, "Language"), S(n, "CollectionPoint"), S(n, "WithdrawalLink"), S(n, "Status") }), new[] { 2.0, 0.7, 1, 1.5, 1.6, 0.9 })),
            Sec("consent-records", "Consent records and withdrawals",
                P($"{recs.Count} consent records in the period: {recs.Count(r => S(r, "Status") == "Given")} active, {recs.Count(r => S(r, "Status") == "Withdrawn")} withdrawn."),
                Chart("bar", "Consent status", new[] { ("Given", (double)recs.Count(r => S(r, "Status") == "Given")), ("Withdrawn", recs.Count(r => S(r, "Status") == "Withdrawn")), ("Expired", recs.Count(r => S(r, "Status") == "Expired")) }),
                Tbl(new[] { "Purpose", "Active", "Withdrawn", "Channels" }, purposes, new[] { 2.5, 0.8, 0.8, 2 }),
                Note("Proof of consent (s.6(10))", "Each record stores the notice version, timestamp, channel and evidence reference, with an immutable event history, so that notice and consent can be proved in proceedings.", "info"))
        };
    }

    async Task<List<Section>> PolicyDoc(Ctx c, string[] keys, string label)
    {
        var pols = (await es.AllAsync("policies", Db.Long(c.Org["Id"]))).Where(p => keys.Contains(S(p, "TemplateKey")) && S(p, "Status") != "Superseded")
            .OrderByDescending(p => S(p, "Status") == "Approved internally").ThenByDescending(p => Db.Long(p["Id"])).ToList();
        var pol = pols.FirstOrDefault();
        if (pol == null) return new() { Sec("policy-none", label, Note("Not available", $"No {label} has been generated or uploaded. Use the Policy Generator to create a draft or upload the existing policy for gap analysis.", "warning")) };
        var approved = S(pol, "Status") == "Approved internally";
        var secs = new List<Section>
        {
            Sec("policy-status", "Document status",
                approved ? Note("Internally approved", $"Approved by {S(pol, "ApprovedBy")} on {D(pol["ApprovedAt"])}. Internal approval does not constitute legal certification.", "success")
                         : Note("Draft - not legally approved", "This document is a draft generated or revised in Data Kavach. It must be reviewed and approved by legal counsel before publication.", "danger"),
                Kv(new[] { ("Title", S(pol, "Title")), ("Version", S(pol, "Version")), ("Source", S(pol, "Source")), ("Status", S(pol, "Status")) }))
        };
        secs.AddRange(MarkupToSections(S(pol, "Content"), "pol"));
        return secs;
    }

    /// <summary>Converts policy markup ("# ", "## ", "- ") into report sections.</summary>
    public static List<Section> MarkupToSections(string text, string idPrefix)
    {
        var secs = new List<Section>();
        Section? cur = null;
        var para = new List<string>();
        var bullets = new List<string>();
        void FlushPara() { if (para.Count > 0) { cur!.Blocks.Add(P(string.Join(" ", para))); para.Clear(); } }
        void FlushBul() { if (bullets.Count > 0) { cur!.Blocks.Add(Bul(bullets.ToList())); bullets.Clear(); } }
        int n = 0;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("# ")) continue;   // document title is the report title
            if (line.StartsWith("## ") || cur == null)
            {
                if (cur != null) { FlushPara(); FlushBul(); }
                cur = new Section { Id = $"{idPrefix}-{++n}", Heading = line.StartsWith("## ") ? System.Text.RegularExpressions.Regex.Replace(line[3..], @"^\d+\.\s*", "") : "Introduction" };
                secs.Add(cur);
                if (line.StartsWith("## ")) continue;
            }
            if (line.Length == 0) { FlushPara(); FlushBul(); continue; }
            if (line.StartsWith("- ")) { FlushPara(); bullets.Add(line[2..]); }
            else { FlushBul(); para.Add(line); }
        }
        if (cur != null) { FlushPara(); FlushBul(); }
        return secs.Where(s => s.Blocks.Count > 0).ToList();
    }

    async Task<List<Section>> Vendors(Ctx c)
    {
        var orgId = Db.Long(c.Org["Id"]);
        var vendors = await es.AllAsync("vendors", orgId);
        var assessments = await es.AllAsync("vendorAssessments", orgId);
        var processors = vendors.Where(v => Db.Bool(v["IsDataProcessor"])).ToList();
        string Gap(Dictionary<string, object?> v)
        {
            var g = new List<string>();
            var dpa = S(v, "DpaStatus");
            if (dpa.StartsWith("Not in place") || dpa.Length == 0) g.Add("No DPA (s.8(2))");
            else if (dpa.Contains("needs update")) g.Add("DPA needs DPDP update");
            if (!Db.Bool(v["SecurityClause"])) g.Add("No security safeguard clause (Rule 6(1)(f))");
            if (!Db.Bool(v["BreachNotificationClause"])) g.Add("No breach notification clause");
            else if (Db.Int(v["BreachNotificationHours"]) is var h and > 48) g.Add($"Breach notice window {h}h too long for 72h Board deadline");
            if (!Db.Bool(v["ErasureClause"])) g.Add("No erasure clause (s.8(7)(b))");
            if (!Db.Bool(v["AuditRights"])) g.Add("No audit rights");
            if (Db.Bool(v["CrossBorder"])) g.Add("Cross-border processing - check s.16 restrictions");
            return g.Count == 0 ? "No contractual gaps" : string.Join("\n", g.Select(x => "- " + x));
        }
        return new()
        {
            Sec("vendor-summary", "Vendor and processor overview",
                P($"{vendors.Count} vendors recorded, of which {processors.Count} act as Data Processors. {processors.Count(v => S(v, "DpaStatus").StartsWith("Not in place") || S(v, "DpaStatus").Length == 0)} processors have no data processing agreement."),
                Chart("bar", "Vendors by risk rating", Opt.Risk.Select(r => (r, (double)vendors.Count(v => S(v, "RiskRating") == r)))),
                Tbl(new[] { "Vendor", "Category", "Personal data", "Location", "Certifications", "DPA", "Risk" },
                    vendors.Select(v => new[] { S(v, "Name"), S(v, "Category"), S(v, "PersonalDataAccessed"), S(v, "StorageLocation"), S(v, "SecurityCertifications"), S(v, "DpaStatus"), S(v, "RiskRating") }), new[] { 1.4, 1.1, 1.8, 1.1, 1.2, 1.2, 0.7 })),
            Sec("vendor-contract-gaps", "Contractual gap analysis",
                Tbl(new[] { "Processor", "Contract", "Gaps identified", "Recommended action" },
                    processors.Select(v => new[] { S(v, "Name"), $"{S(v, "ContractStatus")} (expires {D(v["ContractExpiry"])})", Gap(v), Gap(v) == "No contractual gaps" ? "Maintain; review annually" : "Execute DPDP addendum using the Vendor Data Protection Clause" }),
                    new[] { 1.4, 1.4, 2.6, 2.0 })),
            Sec("vendor-history", "Assessment history",
                Tbl(new[] { "Vendor", "Date", "Assessor", "Score", "Risk", "Findings" },
                    assessments.Select(a => new[] { vendors.FirstOrDefault(v => Db.Long(v["Id"]) == Db.Long(a["VendorId"])) is { } vv ? S(vv, "Name") : "", D(a["AssessmentDate"]), S(a, "Assessor"), S(a, "Score"), S(a, "RiskRating"), S(a, "Findings") }),
                    new[] { 1.3, 0.9, 1.1, 0.6, 0.7, 2.8 }))
        };
    }

    async Task<List<Section>> Security(Ctx c)
    {
        var controls = await es.AllAsync("securityControls", Db.Long(c.Org["Id"]));
        var sc = StatusCounts.From(controls);
        return new()
        {
            Sec("sec-summary", "Summary",
                P($"{controls.Count} safeguard controls mapped to Rule 6 were assessed: {sc.Compliant} compliant, {sc.Partial} partially compliant, {sc.NonCompliant} non-compliant and {sc.NotAssessed} not assessed. {controls.Count(x => Db.Bool(x["Validated"]))} controls have been validated with configuration evidence."),
                StatusChart(sc, "Safeguard status"),
                Note("Validation principle", "A control is rated Compliant only when its configuration is evidenced and validated. Installation of a security tool alone is not treated as compliance.", "info")),
            Sec("sec-controls", "Control assessment",
                Tbl(new[] { "Control", "Rule", "Status", "Tools mapped", "Validated", "Configuration evidence", "Gap / recommendation" },
                    controls.Select(x => new[] { $"{S(x, "ControlKey")} {S(x, "ControlName")}", S(x, "RuleReference"), S(x, "Status"), S(x, "ToolsMapped"), Db.Bool(x["Validated"]) ? $"Yes ({D(x["ValidatedAt"])})" : "No", S(x, "ConfigurationEvidence"), $"{S(x, "Gap")} {S(x, "Recommendation")}".Trim() }),
                    new[] { 1.6, 0.9, 1.0, 1.2, 0.8, 1.7, 1.8 })),
            Sec("sec-requirements", "Rule 6 requirement results", ItemsFor(c, "SEC"))
        };
    }

    async Task<List<Section>> Rights(Ctx c)
    {
        var reqs = (await es.AllAsync("rightsRequests", Db.Long(c.Org["Id"]))).Where(r => InPeriod(c, r["ReceivedAt"])).ToList();
        var closed = reqs.Where(r => Db.Dt(r["ClosedAt"]) != null).ToList();
        var onTime = closed.Count(r => Db.Dt(r["ClosedAt"]) <= Db.Dt(r["DueAt"]));
        return new()
        {
            Sec("rights-summary", "Summary",
                P($"{reqs.Count} requests received in {Period(c)}; {closed.Count} closed, {onTime} within the response period ({Pct(onTime, closed.Count)}). {reqs.Count(r => Db.Dt(r["ClosedAt"]) == null && Db.Dt(r["DueAt"]) < DateTime.UtcNow)} open requests are overdue."),
                Chart("hbar", "Requests by type", reqs.GroupBy(r => S(r, "RequestType")).Select(g => (g.Key, (double)g.Count()))),
                Tbl(new[] { "Request type", "Received", "Closed", "On time", "Open" }, reqs.GroupBy(r => S(r, "RequestType")).Select(g => new[] { g.Key, g.Count().ToString(), g.Count(x => Db.Dt(x["ClosedAt"]) != null).ToString(), g.Count(x => Db.Dt(x["ClosedAt"]) != null && Db.Dt(x["ClosedAt"]) <= Db.Dt(x["DueAt"])).ToString(), g.Count(x => Db.Dt(x["ClosedAt"]) == null).ToString() }), new[] { 2.5, 1, 1, 1, 1 })),
            Sec("rights-register", "Request register",
                P("Data Principal names are masked in this report."),
                Tbl(new[] { "Reference", "Type", "Data principal", "Received", "Due", "Closed", "Status" },
                    reqs.Select(r => new[] { S(r, "Reference"), S(r, "RequestType"), Mask(S(r, "PrincipalName")), D(r["ReceivedAt"]), D(r["DueAt"]), D(r["ClosedAt"]), Db.Dt(r["ClosedAt"]) == null && Db.Dt(r["DueAt"]) < DateTime.UtcNow ? "Overdue" : S(r, "Status") }),
                    new[] { 1.1, 1.8, 1.0, 0.9, 0.9, 0.9, 1.0 })),
            Sec("rights-requirements", "Rights and grievance requirement results", ItemsFor(c, "RTS", "GRV"))
        };
    }

    async Task<List<Section>> BreachReadiness(Ctx c)
    {
        var orgId = Db.Long(c.Org["Id"]);
        var rules = await breach.RulesAsync(orgId);
        var incidents = await es.AllAsync("incidents", orgId);
        var drills = incidents.Where(i => Db.Bool(i["IsDrill"])).ToList();
        var policies = await es.AllAsync("policies", orgId);
        var playbook = policies.FirstOrDefault(p => S(p, "TemplateKey") == "breach-policy");
        var drillRows = new List<string[]>();
        foreach (var d in drills)
        {
            var notes = await db.QueryAsync($"SELECT * FROM {db.Q("IncidentNotifications")} WHERE {db.Q("IncidentId")} = ? AND {db.Q("IsDeleted")} = 0", Db.Long(d["Id"]));
            var det = notes.FirstOrDefault(n => S(n, "RuleKey") == "DPB_DETAILED");
            var hrs = det != null && Db.Dt(det["SentAt"]) is { } sent ? (sent - Db.Dt(d["DiscoveredAt"])!.Value).TotalHours : (double?)null;
            drillRows.Add(new[] { S(d, "Reference"), D(d["DiscoveredAt"]), S(d, "Title"), hrs is { } h ? $"{h:0.0} hours" : "Not completed", hrs is { } h2 ? (h2 <= 72 ? "On time" : "Overdue") : "Pending" });
        }
        return new()
        {
            Sec("breach-req", "Breach management requirements", ItemsFor(c, "BRC").Prepend(P("Assessment of readiness to meet DPDP Rule 7 intimation obligations and parallel CERT-In / sectoral reporting.")).ToList()),
            Sec("breach-deadlines", "Configured notification obligations",
                Tbl(new[] { "Recipient", "Legal basis", "Deadline from awareness", "Type" }, rules.Where(r => r.Enabled).Select(r => new[] { r.Recipient, r.Basis, $"{r.Hours:0} hours", r.StatutoryDeadline ? "Statutory deadline" : "Internal target ('without delay')" }), new[] { 2, 3, 1.2, 1.3 }),
                Note("Distinct obligations", "DPDP intimations to Data Principals and the Board, CERT-In reporting and sectoral reporting are separate obligations with different deadlines and content. All are tracked from a single incident record.", "info")),
            Sec("breach-playbook", "Playbook and drills",
                Kv(new[] { ("Breach response playbook", playbook != null ? $"{S(playbook, "Title")} v{S(playbook, "Version")} - {S(playbook, "Status")}" : "Not documented"), ("Mock drills recorded", drills.Count.ToString()) }),
                Tbl(new[] { "Drill", "Date", "Scenario", "Time to detailed Board report", "Result vs 72h" }, drillRows, new[] { 1.1, 1, 2.4, 1.4, 1.1 }))
        };
    }

    async Task<List<Section>> Incidents(Ctx c, long? id)
    {
        var orgId = Db.Long(c.Org["Id"]);
        var incs = (await es.AllAsync("incidents", orgId)).Where(i => id == null ? InPeriod(c, i["DiscoveredAt"]) && !Db.Bool(i["IsDrill"]) : Db.Long(i["Id"]) == id).ToList();
        var list = new List<Section>();
        if (id == null)
            list.Add(Sec("inc-summary", "Incident summary", P($"{incs.Count} incidents recorded in {Period(c)}; {incs.Count(i => Db.Bool(i["IsPersonalDataBreach"]))} were personal data breaches."),
                Tbl(new[] { "ID", "Title", "Became aware", "Severity", "PDB", "Status" }, incs.Select(i => new[] { S(i, "Reference"), S(i, "Title"), DT(i["DiscoveredAt"]), S(i, "Severity"), YN(i["IsPersonalDataBreach"]), S(i, "Status") }), new[] { 1.1, 2.4, 1.4, 0.8, 0.6, 0.9 })));
        foreach (var i in incs)
        {
            var notes = await db.QueryAsync($"SELECT * FROM {db.Q("IncidentNotifications")} WHERE {db.Q("IncidentId")} = ? AND {db.Q("IsDeleted")} = 0 ORDER BY {db.Q("DueAt")}", Db.Long(i["Id"]));
            var events = await db.QueryAsync($"SELECT * FROM {db.Q("IncidentEvents")} WHERE {db.Q("IncidentId")} = ? AND {db.Q("IsDeleted")} = 0 ORDER BY {db.Q("EventAt")}", Db.Long(i["Id"]));
            list.Add(Sec($"inc-{i["Id"]}", $"{S(i, "Reference")}: {S(i, "Title")}", new List<Block>
            {
                Kv(new[] { ("Became aware", DT(i["DiscoveredAt"])), ("Occurred", DT(i["OccurredAt"])), ("Detection source", S(i, "DetectionSource")), ("Severity / status", $"{S(i, "Severity")} / {S(i, "Status")}"),
                    ("Personal data breach", YN(i["IsPersonalDataBreach"])), ("CERT-In reportable", YN(i["CertInReportable"])), ("Description", S(i, "Description")), ("Systems affected", S(i, "SystemsAffected")),
                    ("Data categories affected", S(i, "DataCategoriesAffected")), ("Estimated affected individuals", S(i, "AffectedCount")), ("Likely impact", S(i, "LikelyImpact")), ("Root cause", S(i, "RootCause")),
                    ("Containment", S(i, "ContainmentActions")), ("Remediation", S(i, "Remediation")), ("Prevention of recurrence", S(i, "PreventiveMeasures")), ("Owner", S(i, "Owner")),
                    ("Legal / management approval", $"{S(i, "LegalApproval")} / {S(i, "ManagementApproval")}") }),
                Tbl(new[] { "Recipient", "Basis", "Due", "Sent", "Result", "Reference" }, notes.Select(n => new[] { S(n, "RecipientName"), S(n, "Basis"), DT(n["DueAt"]), DT(n["SentAt"]),
                    S(n, "Status") == "Not required" ? "Not required" : Db.Dt(n["SentAt"]) is { } st ? (st <= Db.Dt(n["DueAt"]) ? "On time" : "Overdue") : Db.Dt(n["DueAt"]) < DateTime.UtcNow ? "Overdue" : "Pending", S(n, "SubmissionReference") }),
                    new[] { 1.6, 2.2, 1.1, 1.1, 0.8, 1.0 }, "Notification history"),
                Tbl(new[] { "Time (UTC)", "Event", "Recorded by" }, events.Select(e => new[] { DT(e["EventAt"]), S(e, "EventText"), S(e, "CreatedBy") }), new[] { 1.4, 4, 1.4 }, "Evidence timeline")
            }));
        }
        if (incs.Count == 0) list.Add(Sec("inc-none", "Incidents", P("No incidents recorded for the selected period.")));
        return list;
    }

    async Task<List<Section>> Roadmap(Ctx c)
    {
        var actions = (await es.AllAsync("actions", Db.Long(c.Org["Id"]))).OrderBy(a => S(a, "Priority")).ThenBy(a => Db.Dt(a["TargetDate"])).ToList();
        Block Plan(string p) => Tbl(new[] { "Gap ID", "Requirement", "Action", "Risk", "Owner / department", "Target", "Status" },
            actions.Where(a => S(a, "Plan") == p).Select(a => new[] { S(a, "GapId"), S(a, "RequirementRef"), S(a, "Title"), S(a, "RiskRating"), $"{S(a, "Owner")} / {S(a, "Department")}", D(a["TargetDate"]), Db.Dt(a["TargetDate"]) < DateTime.UtcNow.Date && S(a, "Status") is "Open" or "In Progress" ? "Overdue" : S(a, "Status") }),
            new[] { 0.8, 1.5, 2.6, 0.7, 1.4, 0.9, 0.9 });
        return new()
        {
            Sec("rm-progress", "Remediation progress",
                P($"{actions.Count} actions: {actions.Count(a => S(a, "Status") == "Completed")} completed, {actions.Count(a => S(a, "Status") is "Open" or "In Progress")} open. Actions are prioritised by penalty exposure - security safeguards and breach handling first."),
                Chart("bar", "Actions by status", Opt.ActionStatus.Select(s => (s, (double)actions.Count(a => S(a, "Status") == s))))),
            Sec("rm-30", "30-day plan", Plan("30-day")),
            Sec("rm-60", "60-day plan", Plan("60-day")),
            Sec("rm-90", "90-day plan", Plan("90-day")),
            Sec("rm-custom", "Custom / longer-term actions", Plan("Custom")),
        };
    }

    async Task<List<Section>> Master(Ctx c)
    {
        var list = new List<Section>();
        list.AddRange((await ExecSummary(c)).Select(s => { s.Id = "m-" + s.Id; return s; }));
        list.AddRange(Intro(c, "master"));
        list.AddRange(GapSections(c));
        var inv = await Inventory(c); inv[0].Heading = "Personal data inventory - summary"; list.Add(inv[0]); list.Add(inv[1]);
        list.AddRange((await DataFlows(c)).Take(1));
        list.AddRange((await Dpia(c, null)).Select(s => { s.Heading = s.Heading.StartsWith("DPIA") ? s.Heading : "DPIA - " + s.Heading; return s; }));
        list.AddRange((await Consent(c)).Select(s => { s.Heading = "Consent - " + s.Heading; return s; }));
        list.AddRange((await Vendors(c)).Take(2).Select(s => { s.Heading = "Vendors - " + s.Heading; return s; }));
        list.AddRange((await Security(c)).Take(2).Select(s => { s.Heading = "Security - " + s.Heading; return s; }));
        list.AddRange((await Rights(c)).Take(1).Select(s => { s.Heading = "Rights - " + s.Heading; return s; }));
        list.AddRange((await BreachReadiness(c)).Select(s => { s.Heading = "Breach readiness - " + s.Heading; return s; }));
        list.AddRange((await Roadmap(c)).Select(s => { s.Heading = "Roadmap - " + s.Heading; return s; }));
        foreach (var s in list) s.PageBreakBefore = s.Id is "m-exec-overview" or "scope" or "gap-summary" or "inv-summary" or "dpia-applicability" or "consent-results" or "vendor-summary" or "sec-summary" or "rights-summary" or "breach-req" or "rm-progress";
        // Evidence references appendix
        var ev = await es.AllAsync("evidence", Db.Long(c.Org["Id"]));
        list.Add(new Section { Id = "appendix-evidence", Heading = "Appendix - Evidence references", PageBreakBefore = true, Blocks = new() { Tbl(new[] { "Evidence", "Category", "Linked to", "Version", "Last updated" }, ev.Select(e => new[] { S(e, "Title"), S(e, "Category"), $"{S(e, "LinkedEntity")} #{S(e, "LinkedId")}", S(e, "CurrentVersion"), D(e["UpdatedAt"] ?? e["CreatedAt"]) }), new[] { 2.5, 1.2, 1.4, 0.7, 1 }) } });
        return list;
    }
}
