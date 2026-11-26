using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DataKavach.Auth;
using DataKavach.Data;

namespace DataKavach.Services;

public sealed class PolicyService(Db db, EntityService es, AuditService audit)
{
    public sealed record Check(string Topic, string[] AppliesTo, string Pattern, string Reference, string Recommendation, string Reason, string RevisedClause);

    static readonly string[] Notices = { "privacy-notice", "customer-privacy-notice", "employee-privacy-notice", "uploaded" };
    static readonly string[] Any = { "*" };

    /// <summary>Rule-based checks of existing policy text against DPDP requirements.</summary>
    public static readonly Check[] Checks =
    {
        new("Itemised personal data", Notices, @"(personal data (we|that we) (collect|process)|categories of (personal )?data|data we collect|information we collect)", "DPDP Act s.5(1)(i); Rules 3(b)(i)",
            "Add an itemised list of each personal data element processed.", "Rule 3(b) requires an itemised description of personal data in the notice.", "We process the following personal data: [name], [mobile number], [email address], [address], [other items]."),
        new("Specified purposes", Notices, @"(purpose|we use (your|this) (data|information)|why we (collect|process))", "DPDP Act s.5(1)(i); Rules 3(b)(ii)",
            "State each specific purpose with the goods, services or uses it enables.", "Purposes must be specific and linked to the service provided.", "We process this data for the following specified purposes: [purpose] - to provide [service]."),
        new("Withdrawal of consent", new[] { "privacy-notice", "customer-privacy-notice", "uploaded", "consent-procedure", "data-protection-policy" }, @"withdraw(al)?\s+(of\s+)?(your\s+)?consent|withdraw\s+consent", "DPDP Act s.6(4); Rules 3(c)(i)",
            "Explain how consent can be withdrawn, with ease comparable to giving it, and provide the link.", "s.6(4) gives a right to withdraw with comparable ease; Rule 3(c) requires the means in the notice.", "You may withdraw your consent at any time at {{GrievanceUrl}}; withdrawal is as easy as giving consent and does not affect prior lawful processing."),
        new("Data Principal rights", Any, @"(right to (access|correct|erasure|erase)|correction|erasure|access (to )?your (personal )?data)", "DPDP Act s.11-14; Rule 14",
            "Describe rights of access, correction, completion, updating, erasure and nomination, and how to exercise them.", "Notices must explain how rights can be exercised (s.5(1)(ii)); Rule 14(1) requires publishing the means.", "You may request a summary of your personal data, correction, completion, updating or erasure, and nominate another individual, at {{GrievanceUrl}}."),
        new("Right to nominate", new[] { "privacy-notice", "customer-privacy-notice", "uploaded", "rights-procedure" }, @"nominat", "DPDP Act s.14; Rule 14(4)",
            "Add the right to nominate an individual to exercise rights on death or incapacity.", "s.14 is a DPDP-specific right often missing from GDPR-based notices.", "You may nominate one or more individuals to exercise your rights in the event of your death or incapacity."),
        new("Complaint to the Data Protection Board", Notices, @"data protection board", "DPDP Act s.5(1)(iii); Rules 3(c)(iii)",
            "State how the Data Principal may complain to the Data Protection Board of India.", "Required content of every DPDP notice.", "If you are not satisfied with our response, you may file a complaint with the Data Protection Board of India."),
        new("Grievance redressal and response period", Any, @"(grievance|complaint)", "DPDP Act s.8(10), s.13; Rule 14(3)",
            "Name the grievance mechanism and publish a response period not exceeding 90 days.", "Rule 14(3) requires publishing a response period of not more than 90 days.", "Grievances may be raised at {{DpoEmail}}; we respond within {{GrievanceDays}} days of receipt."),
        new("DPO / contact person", Any, @"(data protection officer|\bdpo\b|privacy officer|contact (us|person))", "DPDP Act s.8(9); Rule 9",
            "Publish business contact details of the DPO or a person who can answer questions about processing.", "Rule 9 requires prominent publication and inclusion in every rights response.", "Contact: {{DpoName}}, {{DpoDesignation}} - {{DpoEmail}} / {{DpoPhone}}."),
        new("Language availability", Notices, @"(language|hindi|eighth schedule)", "DPDP Act s.5(3), s.6(3)",
            "Offer the notice in English or Eighth Schedule languages.", "Data Principals must have the option to access the notice in English or an Eighth Schedule language.", "This notice is available in English and [languages]."),
        new("Security safeguards", new[] { "data-protection-policy", "infosec-policy", "uploaded", "vendor-clause" }, @"(encrypt|security safeguard|access control|safeguards)", "DPDP Act s.8(5); Rule 6",
            "Reference the Rule 6 minimum safeguards (encryption/masking, access control, logging, backups, 1-year log retention, contract clauses).", "s.8(5) breaches can attract penalties up to Rs 250 crore.", "We protect personal data using encryption, masking or tokenisation, access controls, logging and monitoring, backups and contractual safeguards with our processors (Rule 6)."),
        new("Breach notification", new[] { "data-protection-policy", "breach-policy", "uploaded", "vendor-clause", "infosec-policy" }, @"(breach)", "DPDP Act s.8(6); Rule 7",
            "Include intimation to affected Data Principals and the Board without delay and the 72-hour detailed report.", "Failure to notify can attract penalties up to Rs 200 crore.", "On becoming aware of a personal data breach we will inform affected Data Principals and the Data Protection Board without delay, and provide the Board a detailed report within 72 hours (Rule 7)."),
        new("Retention and erasure", new[] { "data-protection-policy", "retention-policy", "privacy-notice", "customer-privacy-notice", "uploaded", "erasure-procedure" }, @"(retain|retention|erase|erasure|delet)", "DPDP Act s.8(7); Rule 8",
            "State retention periods, erasure triggers and the one-year minimum log retention.", "s.8(7) requires erasure when purpose is served; Rule 8(3) requires one-year retention of logs.", "We retain personal data only as long as necessary for the specified purpose or as required by law, and retain processing logs for at least one year (Rule 8(3))."),
        new("Children's data", new[] { "data-protection-policy", "privacy-notice", "customer-privacy-notice", "uploaded" }, @"(child|minor|parent|under (the age of )?18)", "DPDP Act s.9; Rule 10",
            "If children's data may be processed, describe verifiable parental consent and the bar on tracking/targeted advertising.", "s.9 obligations attract penalties up to Rs 200 crore.", "We do not knowingly process personal data of children (under 18) without verifiable consent of a parent, and we do not track, behaviourally monitor or target advertising at children."),
    };

    static readonly (string pattern, string topic, string advice)[] ForeignTerms =
    {
        (@"\bGDPR\b|General Data Protection Regulation", "Foreign framework reference", "Replace GDPR references with the DPDP Act, 2023 and DPDP Rules, 2025."),
        (@"legitimate interests?", "Legitimate interests ground", "DPDP has no 'legitimate interests' ground. Map each purpose to consent (s.6) or a specific legitimate use under s.7."),
        (@"Article\s+\d+", "Foreign article references", "Replace GDPR article references with DPDP sections and Rules."),
        (@"supervisory authority", "Supervisory authority", "Refer to the Data Protection Board of India."),
        (@"data controller", "Controller terminology", "Use DPDP terminology: Data Fiduciary (and Data Processor, Data Principal)."),
        (@"\bHIPAA\b|\bCCPA\b", "Foreign framework reference", "Replace with references to Indian law (DPDP Act and Rules, and sectoral rules)."),
    };

    public static string ExtractText(byte[] data, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".txt" or ".md") return Encoding.UTF8.GetString(data).TrimStart('﻿');
        if (ext == ".docx")
        {
            using var z = new ZipArchive(new MemoryStream(data));
            var doc = XDocument.Load(z.GetEntry("word/document.xml")!.Open());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var sb = new StringBuilder();
            foreach (var p in doc.Descendants(w + "p"))
            {
                var style = p.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val")?.Value ?? "";
                var text = string.Concat(p.Descendants().Where(e => e.Name == w + "t" || e.Name == w + "tab").Select(e => e.Name == w + "tab" ? "\t" : e.Value)).Trim();
                if (text.Length == 0) { sb.AppendLine(); continue; }
                var isList = p.Element(w + "pPr")?.Element(w + "numPr") != null;
                if (style.StartsWith("Heading1") || style == "Title") sb.AppendLine("# " + text);
                else if (style.StartsWith("Heading")) sb.AppendLine("## " + text);
                else if (isList) sb.AppendLine("- " + text);
                else sb.AppendLine(text).AppendLine();
            }
            return sb.ToString();
        }
        throw ApiError.Bad("Upload the existing policy as .docx, .txt or .md for analysis.");
    }

    public async Task<long> GenerateAsync(CurrentUser u, long orgId, string templateKey)
    {
        var tpl = PolicyTemplates.All.FirstOrDefault(t => t.Key == templateKey) ?? throw ApiError.Bad("Unknown template");
        var org = await es.GetRawAsync(Schema.Get("organizations"), orgId);
        var id = await es.CreateAsync(Schema.Get("policies"), u, orgId, new Dictionary<string, object?>
        {
            ["Title"] = tpl.Title, ["TemplateKey"] = tpl.Key, ["Version"] = "0.1", ["Content"] = PolicyTemplates.Fill(tpl.Body, org)
        });
        await db.UpdateAsync("Policies", id, new Dictionary<string, object?> { ["Source"] = "Generated from template", ["Status"] = "Draft - not legally approved" });
        return id;
    }

    public async Task<long> UploadAsync(CurrentUser u, long orgId, IFormFile file, string? title, string? policyType)
    {
        await es.Require(u, "policy.edit");
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var text = ExtractText(ms.ToArray(), file.FileName);
        var id = await es.CreateAsync(Schema.Get("policies"), u, orgId, new Dictionary<string, object?>
        {
            ["Title"] = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title, ["TemplateKey"] = string.IsNullOrWhiteSpace(policyType) ? "uploaded" : policyType,
            ["Version"] = "existing", ["Content"] = text
        });
        await db.UpdateAsync("Policies", id, new Dictionary<string, object?> { ["Source"] = "Uploaded existing policy", ["Status"] = "Under Review", ["OriginalFileName"] = FileService.SafeName(file.FileName) });
        await AnalyzeAsync(u, id);
        return id;
    }

    static List<string> Paragraphs(string text) => Regex.Split(text.Replace("\r\n", "\n"), @"\n\s*\n|\n(?=#|- )").Select(p => p.Trim()).Where(p => p.Length > 0).ToList();

    public async Task<object> AnalyzeAsync(CurrentUser u, long policyId)
    {
        await es.Require(u, "policy.edit");
        var pol = await es.GetRawAsync(Schema.Get("policies"), policyId);
        var orgId = Db.Long(pol["OrganizationId"]);
        var org = await es.GetRawAsync(Schema.Get("organizations"), orgId);
        var text = Db.Str(pol["Content"]) ?? "";
        var type = Db.Str(pol["TemplateKey"]) ?? "uploaded";
        var paras = Paragraphs(text);
        // remove previous pending recommendations (keep approved/rejected history)
        await db.ExecAsync($"UPDATE {db.Q("PolicyRecommendations")} SET {db.Q("IsDeleted")} = 1 WHERE {db.Q("PolicyId")} = ? AND {db.Q("ApprovalStatus")} = 'Pending'", policyId);
        int n = 0;
        async Task Add(string topic, string existing, string rec, string reason, string legal, string revised)
        {
            await db.InsertAsync("PolicyRecommendations", new Dictionary<string, object?>
            {
                ["OrganizationId"] = orgId, ["PolicyId"] = policyId, ["Topic"] = topic, ["ExistingClause"] = existing, ["RecommendedModification"] = rec, ["Reason"] = reason,
                ["LegalReference"] = legal, ["RevisedClause"] = PolicyTemplates.Fill(revised, org), ["ApprovalStatus"] = "Pending", ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email, ["IsDeleted"] = false
            });
            n++;
        }
        foreach (var c in Checks.Where(c => c.AppliesTo.Contains("*") || c.AppliesTo.Contains(type)))
        {
            var rx = new Regex(c.Pattern, RegexOptions.IgnoreCase);
            var hit = paras.FirstOrDefault(p => rx.IsMatch(p));
            if (hit == null)
                await Add(c.Topic, "Not found in existing document.", c.Recommendation, c.Reason, c.Reference, c.RevisedClause);
            else if (c.Topic == "Grievance redressal and response period" && !Regex.IsMatch(hit, @"\d+\s*(days|working days)", RegexOptions.IgnoreCase))
                await Add(c.Topic, Trim(hit), "Add a specific response period of not more than 90 days.", "Rule 14(3) requires a published response period not exceeding 90 days.", c.Reference, hit + " " + c.RevisedClause);
            else if (c.Topic == "Breach notification" && !Regex.IsMatch(hit + text, @"72\s*hours|seventy-two", RegexOptions.IgnoreCase))
                await Add(c.Topic, Trim(hit), "Add the 72-hour detailed report to the Data Protection Board and 'without delay' intimation to Data Principals.", c.Reason, c.Reference, c.RevisedClause);
        }
        foreach (var (pattern, topic, advice) in ForeignTerms)
        {
            var rx = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (var p in paras.Where(p => rx.IsMatch(p)).Take(3))
                await Add(topic, Trim(p), advice, "The DPDP Act is Indian law with its own grounds, rights, timelines and penalties; foreign-framework wording can mislead Data Principals and auditors.", "DPDP Act s.4, s.7", rx.Replace(p, m => Replacement(m.Value)));
        }
        await audit.LogAsync("ANALYZE", "policies", policyId, orgId, new { recommendations = n }, u);
        return new { recommendations = n };
    }

    static string Trim(string s) => s.Length > 1500 ? s[..1500] + "..." : s;
    static string Replacement(string v) => v.ToLowerInvariant() switch
    {
        var x when x.Contains("gdpr") || x.Contains("general data protection") => "the Digital Personal Data Protection Act, 2023",
        var x when x.Contains("legitimate interest") => "[consent / specific legitimate use under s.7]",
        var x when x.StartsWith("article") => "[DPDP section]",
        "supervisory authority" => "Data Protection Board of India",
        "data controller" => "Data Fiduciary",
        _ => "the DPDP Act, 2023"
    };

    /// <summary>Creates a new policy version applying only approved recommendations. The original is kept unchanged.</summary>
    public async Task<long> ApplyApprovedAsync(CurrentUser u, long policyId)
    {
        await es.Require(u, "policy.edit");
        var pol = await es.GetRawAsync(Schema.Get("policies"), policyId);
        var recs = await db.QueryAsync($"SELECT * FROM {db.Q("PolicyRecommendations")} WHERE {db.Q("PolicyId")} = ? AND {db.Q("IsDeleted")} = 0 AND {db.Q("ApprovalStatus")} = 'Approved' ORDER BY {db.Q("Id")}", policyId);
        if (recs.Count == 0) throw ApiError.Bad("No approved recommendations to apply.");
        var text = Db.Str(pol["Content"]) ?? "";
        var additions = new List<string>();
        foreach (var r in recs)
        {
            var existing = Db.Str(r["ExistingClause"]) ?? "";
            var revised = Db.Str(r["RevisedClause"]) ?? "";
            if (existing.Length > 0 && !existing.StartsWith("Not found") && !existing.EndsWith("...") && text.Contains(existing)) text = text.Replace(existing, revised);
            else additions.Add($"## {Db.Str(r["Topic"])}\n{revised}");
        }
        if (additions.Count > 0) text = text.TrimEnd() + "\n\n## DPDP amendments\n" + string.Join("\n\n", additions);
        var orgId = Db.Long(pol["OrganizationId"]);
        var ver = Db.Str(pol["Version"]) is { } v && decimal.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dv) ? (dv + 0.1m).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "1.1";
        var id = await es.CreateAsync(Schema.Get("policies"), u, orgId, new Dictionary<string, object?>
        {
            ["Title"] = Db.Str(pol["Title"]), ["TemplateKey"] = Db.Str(pol["TemplateKey"]), ["Version"] = ver, ["Content"] = text, ["BasedOnPolicyId"] = policyId,
            ["ReviewNotes"] = $"Revised from {recs.Count} approved recommendation(s) on {DateTime.UtcNow:dd MMM yyyy}. Original version retained."
        }, system: true);
        await db.UpdateAsync("Policies", id, new Dictionary<string, object?> { ["Source"] = "Revised from recommendations", ["Status"] = "Draft - not legally approved", ["BasedOnPolicyId"] = policyId });
        return id;
    }

    public async Task ApprovePolicyAsync(CurrentUser u, long policyId, string? notes)
    {
        await es.Require(u, "policy.approve");
        var e = Schema.Get("policies");
        await es.UpdateAsync(e, u, policyId, new Dictionary<string, object?> { ["Status"] = "Approved internally", ["ApprovedBy"] = $"{u.Name} ({u.Email})", ["ApprovedAt"] = DateTime.UtcNow, ["ReviewNotes"] = notes }, system: true, action: "APPROVE");
    }
}
