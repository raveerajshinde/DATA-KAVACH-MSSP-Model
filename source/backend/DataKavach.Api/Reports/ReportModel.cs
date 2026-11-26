namespace DataKavach.Reports;

/// <summary>Editable report content. The same approved content renders to DOCX and PDF.</summary>
public sealed class ReportDoc
{
    public string ReportType { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public List<Section> Sections { get; set; } = new();
}

public sealed class Section
{
    public string Id { get; set; } = "";
    public string Heading { get; set; } = "";
    public int Level { get; set; } = 1;
    public bool Include { get; set; } = true;
    public bool PageBreakBefore { get; set; }
    public List<Block> Blocks { get; set; } = new();
}

public sealed class Block
{
    /// <summary>p | h2 | bullets | table | kv | chart | callout | pagebreak | signature</summary>
    public string Type { get; set; } = "p";
    public string? Text { get; set; }
    public string? Title { get; set; }
    public string? Tone { get; set; }                       // info | warning | danger | success
    public List<string>? Items { get; set; }
    public List<string>? Columns { get; set; }
    public List<List<string>>? Rows { get; set; }
    public List<double>? Widths { get; set; }               // relative column widths
    public string? ChartType { get; set; }                  // bar | pie | stacked
    public List<ChartPoint>? Data { get; set; }
    public List<string>? Series { get; set; }               // for stacked bars: series names (values in ChartPoint.Values)
}

public sealed class ChartPoint
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
    public List<double>? Values { get; set; }
    public string? Color { get; set; }
}

/// <summary>Organisation branding and document metadata resolved for rendering.</summary>
public sealed class Branding
{
    public string OrgName { get; set; } = "";
    public string? Address { get; set; }
    public byte[]? Logo { get; set; }
    public string? LogoExt { get; set; }
    public byte[]? ConsultantLogo { get; set; }
    public string? ConsultantLogoExt { get; set; }
    public string? ConsultantName { get; set; }
    public string Primary { get; set; } = "0B3D91";
    public string Secondary { get; set; } = "F2A900";
    public string Font { get; set; } = "Calibri";
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public string? Watermark { get; set; }
    public string Cover { get; set; } = "Corporate Band";
    public string LogoPlacement { get; set; } = "Header left";
    public string Confidentiality { get; set; } = "Confidential";
    public string? Reference { get; set; }
    public string Version { get; set; } = "1.0";
    public string? PreparedBy { get; set; }
    public string? PreparedFor { get; set; }
    public string? ReviewerName { get; set; }
    public string? ReviewerTitle { get; set; }
    public string? SignatoryName { get; set; }
    public string? SignatoryTitle { get; set; }
    public string? AssessmentDate { get; set; }
    public string? Period { get; set; }
    public string Status { get; set; } = "Draft";
    public string? ApprovedBy { get; set; }
    public string? ApprovedAt { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
