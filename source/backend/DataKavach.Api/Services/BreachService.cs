using DataKavach.Auth;
using DataKavach.Data;

namespace DataKavach.Services;

/// <summary>A configurable notification obligation. Deadlines run from the time the organisation became aware.</summary>
public sealed class NotificationRule
{
    public string Key { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Basis { get; set; } = "";
    /// <summary>Hours from awareness. For "without delay" obligations this is an internal target, flagged as such.</summary>
    public double Hours { get; set; }
    public bool StatutoryDeadline { get; set; }
    /// <summary>"personalDataBreach" | "certIn" | "sectoral" | "always"</summary>
    public string AppliesWhen { get; set; } = "personalDataBreach";
    public bool Enabled { get; set; } = true;
    public string Note { get; set; } = "";
}

public sealed class BreachService(Db db, SettingsService settings)
{
    public static List<NotificationRule> DefaultRules() => new()
    {
        new() { Key = "DPB_INITIAL", Recipient = "Data Protection Board of India - initial intimation", Basis = "DPDP Act s.8(6); DPDP Rules 7(2)(a) - without delay", Hours = 24, StatutoryDeadline = false, AppliesWhen = "personalDataBreach", Note = "Law requires 'without delay'. 24 hours is a configurable internal target, not a statutory limit." },
        new() { Key = "DP_AFFECTED", Recipient = "Affected Data Principals", Basis = "DPDP Act s.8(6); DPDP Rules 7(1) - without delay", Hours = 24, StatutoryDeadline = false, AppliesWhen = "personalDataBreach", Note = "Law requires 'without delay' through user account or registered communication mode. 24 hours is an internal target." },
        new() { Key = "DPB_DETAILED", Recipient = "Data Protection Board of India - detailed report", Basis = "DPDP Rules 7(2)(b) - within 72 hours of becoming aware (or longer period allowed by the Board on written request)", Hours = 72, StatutoryDeadline = true, AppliesWhen = "personalDataBreach" },
        new() { Key = "CERTIN", Recipient = "CERT-In", Basis = "CERT-In Directions under s.70B(6) IT Act, 28 April 2022 - within 6 hours of noticing", Hours = 6, StatutoryDeadline = true, AppliesWhen = "certIn", Note = "Separate obligation for reportable cyber incidents; not a DPDP deadline." },
        new() { Key = "SECTORAL", Recipient = "Sectoral regulator", Basis = "Sector-specific directions (e.g. RBI / SEBI / IRDAI) - configure per regulator", Hours = 6, StatutoryDeadline = true, AppliesWhen = "sectoral", Enabled = true, Note = "Adjust hours to the regulator applicable to the organisation." },
    };

    public Task<List<NotificationRule>> RulesAsync(long orgId) => settings.GetJsonAsync("breach.notificationRules", DefaultRules(), orgId);

    /// <summary>Creates any missing notification obligations for an incident and keeps due dates aligned to the awareness time.</summary>
    public async Task SyncNotificationsAsync(long incidentId, CurrentUser user)
    {
        var inc = await db.FirstAsync($"SELECT * FROM {db.Q("Incidents")} WHERE {db.Q("Id")} = ?", incidentId);
        if (inc == null) return;
        var orgId = Db.Long(inc["OrganizationId"]);
        var aware = Db.Dt(inc["DiscoveredAt"]) ?? DateTime.UtcNow;
        var rules = await RulesAsync(orgId);
        var existing = await db.QueryAsync($"SELECT * FROM {db.Q("IncidentNotifications")} WHERE {db.Q("IncidentId")} = ? AND {db.Q("IsDeleted")} = 0", incidentId);
        foreach (var r in rules.Where(r => r.Enabled))
        {
            bool applies = r.AppliesWhen switch
            {
                "personalDataBreach" => Db.Bool(inc["IsPersonalDataBreach"]),
                "certIn" => Db.Bool(inc["CertInReportable"]),
                "sectoral" => Db.Bool(inc["SectoralReportable"]),
                _ => true
            };
            var due = aware.AddHours(r.Hours);
            var row = existing.FirstOrDefault(x => Db.Str(x["RuleKey"]) == r.Key);
            var recipient = r.Key == "SECTORAL" && Db.Str(inc["SectoralRegulator"]) is { Length: > 0 } reg ? reg : r.Recipient;
            if (row == null && applies)
            {
                await db.InsertAsync("IncidentNotifications", new Dictionary<string, object?>
                {
                    ["OrganizationId"] = orgId, ["IncidentId"] = incidentId, ["RuleKey"] = r.Key, ["RecipientName"] = recipient,
                    ["Basis"] = r.Basis + (r.StatutoryDeadline ? "" : " [internal target]"), ["DueAt"] = due, ["Status"] = "Pending",
                    ["Notes"] = r.Note, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = user.Email, ["IsDeleted"] = false
                });
            }
            else if (row != null && Db.Str(row["Status"]) == "Pending")
            {
                var upd = new Dictionary<string, object?>();
                if (!applies) upd["Status"] = "Not required";
                else if (Db.Dt(row["DueAt"]) is { } d && Math.Abs((d - due).TotalMinutes) > 1) upd["DueAt"] = due;
                if (upd.Count > 0) { upd["UpdatedAt"] = DateTime.UtcNow; upd["UpdatedBy"] = user.Email; await db.UpdateAsync("IncidentNotifications", Db.Long(row["Id"]), upd); }
            }
            else if (row != null && Db.Str(row["Status"]) == "Not required" && applies)
                await db.UpdateAsync("IncidentNotifications", Db.Long(row["Id"]), new Dictionary<string, object?> { ["Status"] = "Pending", ["DueAt"] = due });
        }
    }

    /// <summary>Notification templates pre-filled from the incident and organisation (editable before sending).</summary>
    public static string Template(string ruleKey, Dictionary<string, object?> inc, Dictionary<string, object?> org)
    {
        string S(string k) => Db.Str(inc.GetValueOrDefault(k)) ?? "[to be completed]";
        string O(string k) => Db.Str(org.GetValueOrDefault(k)) ?? "[to be completed]";
        var contact = $"{O("DpoName")}, {O("DpoDesignation")} - {O("DpoEmail")} / {O("DpoPhone")}";
        return ruleKey switch
        {
            "DP_AFFECTED" => $"""
                Subject: Important - personal data breach affecting your information

                Dear Customer,

                We are writing to inform you of a personal data breach at {O("Name")}.

                What happened: {S("Description")}
                When: occurred {S("OccurredAt")}; we became aware on {S("DiscoveredAt")} (UTC).
                Nature and extent: {S("DataCategoriesAffected")}
                Likely consequences for you: {S("LikelyImpact")}
                What we have done and are doing: {S("ContainmentActions")} {S("Remediation")}
                What you can do to protect yourself: [e.g. change your password, be alert to phishing messages, monitor your accounts]

                For questions, contact: {contact}

                (DPDP Rules 2025, Rule 7(1))
                """,
            "DPB_INITIAL" => $"""
                Intimation of personal data breach - DPDP Rules 2025, Rule 7(2)(a)

                Data Fiduciary: {O("Name")} ({O("RegisteredAddress")})
                Incident reference: {S("Reference")}
                Description and nature of breach: {S("Description")}
                Extent (data categories / approx. individuals): {S("DataCategoriesAffected")} / {S("AffectedCount")}
                Timing: occurred {S("OccurredAt")}; became aware {S("DiscoveredAt")} (UTC)
                Location of occurrence: {S("Location")}
                Likely impact: {S("LikelyImpact")}
                Contact: {contact}
                """,
            "DPB_DETAILED" => $"""
                Detailed report on personal data breach - DPDP Rules 2025, Rule 7(2)(b)

                Data Fiduciary: {O("Name")}   Incident reference: {S("Reference")}
                (i) Updated and detailed description: {S("Description")}
                (ii) Broad facts - events, circumstances and reasons: {S("RootCause")}
                (iii) Measures implemented or proposed to mitigate risk: {S("ContainmentActions")} {S("Remediation")}
                (iv) Findings regarding the person who caused the breach: {S("PersonResponsible")}
                (v) Remedial measures to prevent recurrence: {S("PreventiveMeasures")}
                (vi) Report on intimations given to affected Data Principals: [summary of notifications, dates, channels, count]
                Contact: {contact}
                """,
            "CERTIN" => $"""
                Cyber security incident report - CERT-In (incident@cert-in.org.in)

                Organisation: {O("Name")}   Contact: {contact}
                Incident reference: {S("Reference")}   Time of noticing: {S("DiscoveredAt")} (UTC)
                Type / description: {S("Description")}
                Systems affected: {S("SystemsAffected")}
                Detection source: {S("DetectionSource")}
                Actions taken: {S("ContainmentActions")}
                """,
            _ => $"Incident {S("Reference")} - {S("Title")}\n\n{S("Description")}\n\nContact: {contact}"
        };
    }
}
