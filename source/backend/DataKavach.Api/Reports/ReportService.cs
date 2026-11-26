using System.IO.Compression;
using System.Text.Json;
using DataKavach.Auth;
using DataKavach.Data;
using DataKavach.Services;

namespace DataKavach.Reports;

public sealed class TemplateSection
{
    public string Id { get; set; } = "";
    public bool Include { get; set; } = true;
    public int Order { get; set; }
    public string? Heading { get; set; }
    public bool Custom { get; set; }
    public bool PageBreakBefore { get; set; }
    public List<Block>? Blocks { get; set; }
}

public sealed class ReportService(Db db, EntityService es, ReportBuilder builder, PdfConverter pdf, FileService files, AuditService audit, SettingsService settings)
{
    static readonly JsonSerializerOptions J = Json.Options;

    public async Task<long> CreateAsync(CurrentUser u, ReportRequest req, long? templateId)
    {
        await es.Require(u, "report.edit");
        var doc = await builder.BuildAsync(req);
        if (templateId != null) ApplyTemplate(doc, await TemplateAsync(templateId.Value));
        var n = await db.CountAsync($"SELECT COUNT(*) FROM {db.Q("Reports")} WHERE {db.Q("OrganizationId")} = ? AND {db.Q("ReportType")} = ?", req.OrgId, req.ReportType);
        var id = await db.InsertAsync("Reports", new Dictionary<string, object?>
        {
            ["OrganizationId"] = req.OrgId, ["ReportType"] = req.ReportType, ["Title"] = doc.Title, ["Version"] = $"{n + 1}.0", ["Status"] = "Draft", ["TemplateId"] = templateId ?? 0,
            ["AssessmentId"] = req.AssessmentId ?? 0, ["ContentJson"] = JsonSerializer.Serialize(doc, J),
            ["PeriodFrom"] = req.PeriodFrom is { } f ? DateOnly.FromDateTime(f) : null, ["PeriodTo"] = req.PeriodTo is { } t ? DateOnly.FromDateTime(t) : null,
            ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email, ["IsDeleted"] = false
        });
        await audit.LogAsync("GENERATE", "reports", id, req.OrgId, new { req.ReportType, templateId }, u);
        return id;
    }

    public async Task<Dictionary<string, object?>> TemplateAsync(long id)
    {
        var t = await db.FirstAsync($"SELECT * FROM {db.Q("ReportTemplates")} WHERE {db.Q("Id")} = ? AND {db.Q("IsDeleted")} = 0", id) ?? throw ApiError.NotFound("Template");
        if (Db.Long(t["OrganizationId"]) > 0) es.EnsureOrg(Db.Long(t["OrganizationId"]));
        return t;
    }

    public static void ApplyTemplate(ReportDoc doc, Dictionary<string, object?> tpl)
    {
        var cfg = JsonSerializer.Deserialize<List<TemplateSection>>(Db.Str(tpl["SectionsJson"]) ?? "[]", J) ?? new();
        if (cfg.Count == 0) return;
        var byId = cfg.Where(c => !c.Custom).ToDictionary(c => c.Id, c => c);
        var ordered = new List<(int order, Section s)>();
        int fallback = 10000;
        foreach (var s in doc.Sections)
        {
            if (byId.TryGetValue(s.Id, out var c))
            {
                s.Include = c.Include;
                if (!string.IsNullOrWhiteSpace(c.Heading)) s.Heading = c.Heading;
                s.PageBreakBefore = s.PageBreakBefore || c.PageBreakBefore;
                ordered.Add((c.Order, s));
            }
            else ordered.Add((s.Id == "signoff" ? int.MaxValue : fallback++, s));
        }
        foreach (var c in cfg.Where(c => c.Custom))
            ordered.Add((c.Order, new Section { Id = c.Id, Heading = c.Heading ?? "Additional information", Include = c.Include, PageBreakBefore = c.PageBreakBefore, Blocks = c.Blocks ?? new() }));
        doc.Sections = ordered.OrderBy(x => x.order).Select(x => x.s).ToList();
    }

    public async Task<(Dictionary<string, object?> row, ReportDoc doc)> GetAsync(CurrentUser u, long id)
    {
        await es.Require(u, "report.view");
        var row = await es.GetRawAsync(Schema.Get("reports"), id);
        var doc = JsonSerializer.Deserialize<ReportDoc>(Db.Str(row["ContentJson"]) ?? "{}", J) ?? new ReportDoc();
        return (row, doc);
    }

    public async Task SaveContentAsync(CurrentUser u, long id, ReportDoc doc)
    {
        await es.Require(u, "report.edit");
        var row = await es.GetRawAsync(Schema.Get("reports"), id);
        var wasApproved = Db.Str(row["Status"]) == "Approved";
        await db.UpdateAsync("Reports", id, new Dictionary<string, object?>
        {
            ["ContentJson"] = JsonSerializer.Serialize(doc, J), ["Title"] = doc.Title, ["Status"] = "Draft", ["ApprovedBy"] = null, ["ApprovedAt"] = null,
            ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email
        });
        await audit.LogAsync("UPDATE", "reports", id, Db.Long(row["OrganizationId"]), new { edited = true, approvalReset = wasApproved }, u);
    }

    public async Task ApproveAsync(CurrentUser u, long id)
    {
        await es.Require(u, "report.approve");
        var row = await es.GetRawAsync(Schema.Get("reports"), id);
        await db.UpdateAsync("Reports", id, new Dictionary<string, object?> { ["Status"] = "Approved", ["ApprovedBy"] = $"{u.Name} ({u.Email})", ["ApprovedAt"] = DateTime.UtcNow, ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email });
        await audit.LogAsync("APPROVE", "reports", id, Db.Long(row["OrganizationId"]), null, u);
    }

    public async Task<Branding> BrandingAsync(long orgId, Dictionary<string, object?>? report, Dictionary<string, object?>? tpl)
    {
        var o = await es.GetRawAsync(Schema.Get("organizations"), orgId);
        string? S(string k) => Db.Str(o.GetValueOrDefault(k)) is { Length: > 0 } s ? s : null;
        string? T(string k) => tpl != null && Db.Str(tpl.GetValueOrDefault(k)) is { Length: > 0 } s ? s : null;
        async Task<(byte[]?, string?)> Img(long id)
        {
            if (id <= 0) return (null, null);
            var f = await db.FirstAsync($"SELECT * FROM {db.Q("StoredFiles")} WHERE {db.Q("Id")} = ?", id);
            if (f == null) return (null, null);
            var abs = files.Abs(Db.Str(f["StoragePath"])!);
            return File.Exists(abs) ? (await File.ReadAllBytesAsync(abs), Path.GetExtension(Db.Str(f["FileName"]))) : (null, null);
        }
        var (logo, logoExt) = await Img(Db.Long(o["LogoFileId"]));
        var (clogo, clogoExt) = await Img(Db.Long(o["ConsultantLogoFileId"]));
        // MSSP: the service provider's branding fills the consultant slot unless the client overrides it
        var provider = await DataKavach.Endpoints.ProviderEndpoints.GetBranding(settings);
        if (provider.UseOnReports && clogo == null && provider.LogoFileId > 0) (clogo, clogoExt) = await Img(provider.LogoFileId);
        var providerName = provider.UseOnReports ? provider.Name : null;
        var status = report != null ? Db.Str(report["Status"]) ?? "Draft" : "Draft";
        var pf = Db.Dt(report?.GetValueOrDefault("PeriodFrom")) ?? Db.Dt(o["PeriodFrom"]);
        var pt = Db.Dt(report?.GetValueOrDefault("PeriodTo")) ?? Db.Dt(o["PeriodTo"]);
        return new Branding
        {
            OrgName = S("Name") ?? "", Address = S("RegisteredAddress"), Logo = logo, LogoExt = logoExt, ConsultantLogo = clogo, ConsultantLogoExt = clogoExt, ConsultantName = S("ConsultantName") ?? providerName,
            Primary = T("PrimaryColor") ?? S("PrimaryColor") ?? "#0B3D91", Secondary = T("SecondaryColor") ?? S("SecondaryColor") ?? "#F2A900", Font = T("FontFamily") ?? S("FontFamily") ?? "Calibri",
            HeaderText = S("HeaderText"), FooterText = S("FooterText"),
            Watermark = T("Watermark") ?? S("Watermark") ?? (status == "Approved" ? null : "DRAFT"),
            Cover = T("CoverDesign") ?? S("CoverDesign") ?? "Corporate Band", LogoPlacement = T("LogoPlacement") ?? S("LogoPlacement") ?? "Header left",
            Confidentiality = S("Confidentiality") ?? "Confidential", Reference = S("ReportReference") is { } rr ? (report != null ? $"{rr}/{Db.Str(report["ReportType"])?.ToUpperInvariant()}" : rr) : null,
            Version = report != null ? Db.Str(report["Version"]) ?? "1.0" : S("ReportVersion") ?? "1.0",
            PreparedBy = S("PreparedBy") ?? providerName, PreparedFor = S("PreparedFor"), ReviewerName = S("ReviewerName"), ReviewerTitle = S("ReviewerTitle"),
            SignatoryName = S("SignatoryName"), SignatoryTitle = S("SignatoryTitle"),
            AssessmentDate = Db.Dt(o["AssessmentDate"])?.ToString("dd MMM yyyy"), Period = pf != null || pt != null ? $"{pf:dd MMM yyyy} - {pt:dd MMM yyyy}" : null,
            Status = status, ApprovedBy = report != null ? Db.Str(report["ApprovedBy"]) : null, ApprovedAt = Db.Dt(report?.GetValueOrDefault("ApprovedAt"))?.ToString("dd MMM yyyy")
        };
    }

    public sealed record Rendered(byte[] Data, string FileName, string ContentType);

    public async Task<(byte[] docx, byte[]? pdf)> RenderAsync(ReportDoc doc, Branding b, bool needPdf)
    {
        var docx = new DocxWriter(doc, b).Build();
        if (!pdf.Available) return (docx, null);
        var res = await pdf.ConvertAsync(docx);
        var docxFinal = new DocxWriter(doc, b, res.TocPages).Build();   // TOC pre-filled with page numbers
        return (docxFinal, res.Pdf);
    }

    public async Task<Rendered> ExportAsync(CurrentUser u, long id, string format)
    {
        await es.Require(u, "report.export");
        var (row, doc) = await GetAsync(u, id);
        var orgId = Db.Long(row["OrganizationId"]);
        var tpl = Db.Long(row["TemplateId"]) > 0 ? await db.FirstAsync($"SELECT * FROM {db.Q("ReportTemplates")} WHERE {db.Q("Id")} = ?", Db.Long(row["TemplateId"])) : null;
        var b = await BrandingAsync(orgId, row, tpl);
        if (format == "pdf" && !pdf.Available) throw new ApiError(503, "PDF export needs LibreOffice on the server. Run repair.ps1 -InstallLibreOffice or install LibreOffice, then retry.");
        var (docx, pdfBytes) = await RenderAsync(doc, b, format == "pdf");
        var data = format == "pdf" ? pdfBytes! : docx;
        var safe = string.Concat((b.OrgName + "_" + doc.Title).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var fileName = $"{safe}_v{Db.Str(row["Version"])}_{DateTime.UtcNow:yyyyMMdd}.{format}";
        var rel = Path.Combine("org" + orgId, "reports", id.ToString(), $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.{format}");
        var abs = files.Abs(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        await File.WriteAllBytesAsync(abs, data);
        var sha = Hashing.Sha256Hex(data);
        await db.InsertAsync("ReportFiles", new Dictionary<string, object?> { ["OrganizationId"] = orgId, ["ReportId"] = id, ["Format"] = format, ["FileName"] = fileName, ["SizeBytes"] = (long)data.Length, ["Sha256"] = sha, ["StoragePath"] = rel, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email });
        await audit.LogAsync("EXPORT", "reports", id, orgId, new { format, fileName, sha256 = sha, status = b.Status }, u);
        return new Rendered(data, fileName, format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    /// <summary>Generates several reports and returns them as one ZIP (complete compliance package).</summary>
    public async Task<Rendered> PackageAsync(CurrentUser u, long orgId, string[] types, string[] formats, long? assessmentId, DateTime? from, DateTime? to, long? templateId)
    {
        await es.Require(u, "report.export");
        using var ms = new MemoryStream();
        var manifest = new List<string>();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var t in types)
            {
                var id = await CreateAsync(u, new ReportRequest(orgId, t, assessmentId, from, to, null), templateId);
                foreach (var f in formats.Distinct())
                {
                    if (f == "pdf" && !pdf.Available) continue;
                    var r = await ExportAsync(u, id, f);
                    var entry = zip.CreateEntry($"{ReportBuilder.Types.ToList().FindIndex(x => x.key == t) + 1:D2}_{r.FileName}", CompressionLevel.Optimal);
                    await using var s = entry.Open();
                    await s.WriteAsync(r.Data);
                    manifest.Add($"{r.FileName}\tSHA-256 {Hashing.Sha256Hex(r.Data)}");
                }
            }
            var m = zip.CreateEntry("MANIFEST.txt");
            await using (var w = new StreamWriter(m.Open())) await w.WriteAsync($"Data Kavach compliance package\nGenerated {DateTime.UtcNow:u} by {u.Email}\n\n" + string.Join("\n", manifest));
        }
        await audit.LogAsync("EXPORT", "reportPackage", null, orgId, new { types, formats }, u);
        return new Rendered(ms.ToArray(), $"DPDP_Compliance_Package_{DateTime.UtcNow:yyyyMMdd}.zip", "application/zip");
    }
}
