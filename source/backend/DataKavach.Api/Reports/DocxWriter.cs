using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace DataKavach.Reports;

/// <summary>
/// Writes genuine, editable WordprocessingML (.docx) as a standard OPC package (explicit content types and relationships).
/// All text and tables remain editable; charts are native Word charts; TOC is a real Word TOC field.
/// </summary>
public sealed class DocxWriter
{
    const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    const string RelBase = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string NsDecl = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:w10=\"urn:schemas-microsoft-com:office:word\"";
    const int ContentWidth = 9638;   // A4 (11906) minus 2 x 1134 twips margins

    readonly ReportDoc _doc;
    readonly Branding _b;
    readonly Dictionary<string, int>? _tocPages;
    readonly StringBuilder _body = new();
    readonly List<(string id, string target, string type, byte[] data, string contentType, string partName)> _docParts = new();
    readonly List<(string text, int level)> _headings = new();
    int _rel = 100, _docPr = 1, _chart = 0;
    string? _logoRelDoc, _cLogoRelDoc;

    public DocxWriter(ReportDoc doc, Branding b, Dictionary<string, int>? tocPages = null)
    {
        _doc = doc; _b = b; _tocPages = tocPages;
    }

    public static string Esc(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var clean = new string(s.Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || (ch >= 0x20 && ch != 0xFFFE && ch != 0xFFFF)).ToArray());
        return SecurityElement.Escape(clean)!;
    }

    string Font => Esc(_b.Font);
    static string Hex(string? c, string fallback) => c != null && Regex.IsMatch(c.TrimStart('#'), "^[0-9A-Fa-f]{6}$") ? c.TrimStart('#').ToUpperInvariant() : fallback;
    string P1 => Hex(_b.Primary, "0B3D91");
    string P2 => Hex(_b.Secondary, "F2A900");

    // ------------------------------------------------------------------ runs & paragraphs
    string Runs(string? text, string rPr = "")
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder();
        // **bold** inline markup
        var parts = Regex.Split(text, @"(\*\*[^*]+\*\*)");
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            var bold = part.StartsWith("**") && part.EndsWith("**") && part.Length > 4;
            var t = bold ? part[2..^2] : part;
            var lines = t.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append("<w:r><w:br/></w:r>");
                if (lines[i].Length == 0) continue;
                var segs = lines[i].Split('\t');
                for (int j = 0; j < segs.Length; j++)
                {
                    if (j > 0) sb.Append("<w:r><w:tab/></w:r>");
                    if (segs[j].Length == 0) continue;
                    sb.Append("<w:r>");
                    if (bold || rPr.Length > 0) sb.Append("<w:rPr>").Append(bold && !rPr.StartsWith("<w:b/>") ? "<w:b/>" : "").Append(rPr).Append("</w:rPr>");
                    sb.Append("<w:t xml:space=\"preserve\">").Append(Esc(segs[j])).Append("</w:t></w:r>");
                }
            }
        }
        return sb.ToString();
    }

    string Para(string? text, string? style = null, string pPrExtra = "", string rPr = "") =>
        $"<w:p><w:pPr>{(style != null ? $"<w:pStyle w:val=\"{style}\"/>" : "")}{pPrExtra}</w:pPr>{Runs(text, rPr)}</w:p>";

    static string PageBreak => "<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>";

    // ------------------------------------------------------------------ images
    public static (int w, int h) ImageSize(byte[] d)
    {
        try
        {
            if (d.Length > 24 && d[0] == 0x89 && d[1] == 0x50)
                return ((d[16] << 24) | (d[17] << 16) | (d[18] << 8) | d[19], (d[20] << 24) | (d[21] << 16) | (d[22] << 8) | d[23]);
            if (d.Length > 4 && d[0] == 0xFF && d[1] == 0xD8)
            {
                int i = 2;
                while (i + 9 < d.Length)
                {
                    if (d[i] != 0xFF) { i++; continue; }
                    var m = d[i + 1];
                    var len = (d[i + 2] << 8) | d[i + 3];
                    if (m is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                        return ((d[i + 7] << 8) | d[i + 8], (d[i + 5] << 8) | d[i + 6]);
                    i += 2 + len;
                }
            }
            if (d.Length > 10 && d[0] == 'G' && d[1] == 'I') return (d[6] | (d[7] << 8), d[8] | (d[9] << 8));
        }
        catch { }
        return (400, 150);
    }

    string Inline(string relId, int cx, int cy, string name)
    {
        var id = _docPr++;
        return $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"{id}\" name=\"{Esc(name)} {id}\"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"{id}\" name=\"{Esc(name)}\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"{relId}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
    }

    static (int cx, int cy) Fit(byte[] img, double maxWcm, double maxHcm)
    {
        var (w, h) = ImageSize(img);
        if (w <= 0 || h <= 0) (w, h) = (400, 150);
        const double emuPerCm = 360000;
        double scale = Math.Min(maxWcm * emuPerCm / w, maxHcm * emuPerCm / h);
        return ((int)(w * scale), (int)(h * scale));
    }

    static string ImgContentType(string? ext) => ext?.ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", _ => "image/png" };

    string AddDocImage(byte[] data, string? ext, string name)
    {
        var id = $"rId{_rel++}";
        var e = (ext ?? ".png").ToLowerInvariant();
        _docParts.Add((id, $"media/{name}{e}", RelBase + "/image", data, ImgContentType(ext), $"/word/media/{name}{e}"));
        return id;
    }

    // ------------------------------------------------------------------ tables
    static readonly Dictionary<string, (string fill, string color)> Semantic = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Compliant"] = ("C6EFCE", "006100"), ["Partially Compliant"] = ("FFEB9C", "9C5700"), ["Non-Compliant"] = ("FFC7CE", "9C0006"),
        ["Not Applicable"] = ("EDEDED", "595959"), ["Not Assessed"] = ("F2F2F2", "595959"),
        ["High"] = ("FFC7CE", "9C0006"), ["Medium"] = ("FFEB9C", "9C5700"), ["Low"] = ("C6EFCE", "006100"), ["Critical"] = ("FF9999", "7F0000"),
        ["Yes"] = ("C6EFCE", "006100"), ["No"] = ("FFC7CE", "9C0006"), ["Overdue"] = ("FFC7CE", "9C0006"), ["On time"] = ("C6EFCE", "006100"),
        ["Approved"] = ("C6EFCE", "006100"), ["Pending"] = ("FFEB9C", "9C5700"), ["Completed"] = ("C6EFCE", "006100"), ["Open"] = ("FFEB9C", "9C5700"),
    };

    string Cell(string? text, int width, bool header = false, string? fill = null, bool bold = false, string? color = null, int gridSpan = 1, string? size = null)
    {
        string? semFill = null, semColor = null;
        if (!header && text != null && Semantic.TryGetValue(text.Trim(), out var sem)) { semFill = sem.fill; semColor = sem.color; }
        var f = header ? P1 : fill ?? semFill;
        var col = header ? "FFFFFF" : color ?? semColor;
        var rPr = (header || bold ? "<w:b/>" : "") + (col != null ? $"<w:color w:val=\"{col}\"/>" : "") + (size != null ? $"<w:sz w:val=\"{size}\"/><w:szCs w:val=\"{size}\"/>" : "");
        var sb = new StringBuilder($"<w:tc><w:tcPr><w:tcW w:w=\"{width}\" w:type=\"dxa\"/>{(gridSpan > 1 ? $"<w:gridSpan w:val=\"{gridSpan}\"/>" : "")}{(f != null ? $"<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{f}\"/>" : "")}</w:tcPr>");
        var lines = (text ?? "").Replace("\r\n", "\n").Split("\n");
        foreach (var line in lines)
        {
            var isBullet = line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("• ");
            var t = isBullet ? "• " + line.TrimStart()[2..] : line;
            sb.Append($"<w:p><w:pPr><w:pStyle w:val=\"TableText\"/></w:pPr>{Runs(t, rPr)}</w:p>");
        }
        sb.Append("</w:tc>");
        return sb.ToString();
    }

    int[] ColWidths(int n, List<double>? rel)
    {
        var weights = rel != null && rel.Count == n ? rel.ToArray() : Enumerable.Repeat(1.0, n).ToArray();
        var sum = weights.Sum();
        var w = weights.Select(x => (int)(ContentWidth * x / sum)).ToArray();
        return w;
    }

    string Table(List<string>? columns, List<List<string>>? rows, List<double>? widths, bool firstColBold = false)
    {
        var n = columns?.Count ?? rows?.FirstOrDefault()?.Count ?? 1;
        if (n == 0) return "";
        var w = ColWidths(n, widths);
        var sb = new StringBuilder("<w:tbl><w:tblPr><w:tblStyle w:val=\"DkGrid\"/><w:tblW w:w=\"" + ContentWidth + "\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblLook w:val=\"04A0\" w:firstRow=\"1\" w:lastRow=\"0\" w:firstColumn=\"0\" w:lastColumn=\"0\" w:noHBand=\"0\" w:noVBand=\"1\"/></w:tblPr><w:tblGrid>");
        foreach (var x in w) sb.Append($"<w:gridCol w:w=\"{x}\"/>");
        sb.Append("</w:tblGrid>");
        if (columns != null && columns.Count > 0)
        {
            sb.Append("<w:tr><w:trPr><w:tblHeader/><w:cantSplit/></w:trPr>");
            for (int i = 0; i < n; i++) sb.Append(Cell(i < columns.Count ? columns[i] : "", w[i], header: true));
            sb.Append("</w:tr>");
        }
        var zebra = false;
        foreach (var r in rows ?? new())
        {
            sb.Append("<w:tr><w:trPr><w:cantSplit/></w:trPr>");
            for (int i = 0; i < n; i++)
                sb.Append(Cell(i < r.Count ? r[i] : "", w[i], fill: zebra && !(i < r.Count && Semantic.ContainsKey(r[i] ?? "")) ? "F5F7FB" : null, bold: firstColBold && i == 0));
            sb.Append("</w:tr>");
            zebra = !zebra;
        }
        if (rows == null || rows.Count == 0)
            sb.Append("<w:tr>").Append(Cell("No records.", ContentWidth, gridSpan: n, color: "7F7F7F")).Append("</w:tr>");
        sb.Append("</w:tbl>");
        sb.Append(Para("", "Spacer"));
        return sb.ToString();
    }

    string KvTable(List<List<string>>? rows)
    {
        var w = new[] { (int)(ContentWidth * 0.32), ContentWidth - (int)(ContentWidth * 0.32) };
        var sb = new StringBuilder($"<w:tbl><w:tblPr><w:tblStyle w:val=\"DkGrid\"/><w:tblW w:w=\"{ContentWidth}\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"{w[0]}\"/><w:gridCol w:w=\"{w[1]}\"/></w:tblGrid>");
        foreach (var r in rows ?? new())
            sb.Append("<w:tr><w:trPr><w:cantSplit/></w:trPr>").Append(Cell(r.ElementAtOrDefault(0), w[0], fill: "EAF0FA", bold: true, color: P1)).Append(Cell(r.ElementAtOrDefault(1), w[1])).Append("</w:tr>");
        sb.Append("</w:tbl>").Append(Para("", "Spacer"));
        return sb.ToString();
    }

    string Callout(string? title, string? text, string? tone)
    {
        var (fill, border) = tone switch
        {
            "danger" => ("FDECEC", "C00000"), "warning" => ("FFF6E0", "C68A00"), "success" => ("EAF6EC", "2E7D32"), _ => ("EAF0FA", P1)
        };
        var sb = new StringBuilder($"<w:tbl><w:tblPr><w:tblW w:w=\"{ContentWidth}\" w:type=\"dxa\"/><w:tblBorders><w:left w:val=\"single\" w:sz=\"36\" w:space=\"0\" w:color=\"{border}\"/></w:tblBorders><w:tblCellMar><w:top w:w=\"100\" w:type=\"dxa\"/><w:left w:w=\"180\" w:type=\"dxa\"/><w:bottom w:w=\"100\" w:type=\"dxa\"/><w:right w:w=\"180\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid><w:gridCol w:w=\"{ContentWidth}\"/></w:tblGrid><w:tr><w:tc><w:tcPr><w:tcW w:w=\"{ContentWidth}\" w:type=\"dxa\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{fill}\"/></w:tcPr>");
        if (!string.IsNullOrEmpty(title)) sb.Append(Para(title, "TableText", "", $"<w:b/><w:color w:val=\"{border}\"/>"));
        sb.Append(Para(text, "TableText"));
        sb.Append("</w:tc></w:tr></w:tbl>").Append(Para("", "Spacer"));
        return sb.ToString();
    }

    // ------------------------------------------------------------------ charts
    static readonly string[] Palette = { "0B3D91", "F2A900", "2E7D32", "C62828", "6A1B9A", "00838F", "EF6C00", "5D4037", "546E7A", "AD1457" };
    static readonly Dictionary<string, string> SemanticChart = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Compliant"] = "2E7D32", ["Partially Compliant"] = "F9A825", ["Non-Compliant"] = "C62828", ["Not Assessed"] = "9E9E9E", ["Not Applicable"] = "CFD8DC",
        ["High"] = "C62828", ["Medium"] = "F9A825", ["Low"] = "2E7D32", ["Open"] = "F9A825", ["In Progress"] = "1565C0", ["Completed"] = "2E7D32", ["Deferred"] = "9E9E9E", ["Cancelled"] = "CFD8DC"
    };

    string ColorFor(ChartPoint p, int i) => Hex(p.Color, SemanticChart.TryGetValue(p.Label, out var s) ? s : i == 0 ? P1 : Palette[i % Palette.Length]);

    string ChartXml(Block b)
    {
        var data = b.Data ?? new();
        var type = b.ChartType ?? "bar";
        var inv = CultureInfo.InvariantCulture;
        string StrCache(IEnumerable<string> labels)
        {
            var l = labels.ToList();
            var sb = new StringBuilder($"<c:strCache><c:ptCount val=\"{l.Count}\"/>");
            for (int i = 0; i < l.Count; i++) sb.Append($"<c:pt idx=\"{i}\"><c:v>{Esc(l[i])}</c:v></c:pt>");
            return sb.Append("</c:strCache>").ToString();
        }
        string NumCache(IEnumerable<double> vals)
        {
            var v = vals.ToList();
            var sb = new StringBuilder($"<c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"{v.Count}\"/>");
            for (int i = 0; i < v.Count; i++) sb.Append($"<c:pt idx=\"{i}\"><c:v>{v[i].ToString(inv)}</c:v></c:pt>");
            return sb.Append("</c:numCache>").ToString();
        }
        string Col(int i) => ((char)('B' + i)).ToString();
        var n = data.Count;
        string Cat() => $"<c:cat><c:strRef><c:f>Sheet1!$A$2:$A${n + 1}</c:f>{StrCache(data.Select(d => d.Label))}</c:strRef></c:cat>";
        string Val(int s, IEnumerable<double> v) => $"<c:val><c:numRef><c:f>Sheet1!${Col(s)}$2:${Col(s)}${n + 1}</c:f>{NumCache(v)}</c:numRef></c:val>";
        string Tx(int s, string name) => $"<c:tx><c:strRef><c:f>Sheet1!${Col(s)}$1</c:f><c:strCache><c:ptCount val=\"1\"/><c:pt idx=\"0\"><c:v>{Esc(name)}</c:v></c:pt></c:strCache></c:strRef></c:tx>";
        string Fill(string c) => $"<c:spPr><a:solidFill><a:srgbClr val=\"{c}\"/></a:solidFill></c:spPr>";
        const string Labels = "<c:dLbls><c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr><c:showLegendKey val=\"0\"/><c:showVal val=\"1\"/><c:showCatName val=\"0\"/><c:showSerName val=\"0\"/><c:showPercent val=\"0\"/><c:showBubbleSize val=\"0\"/></c:dLbls>";
        string plot, legend = "";
        if (type == "pie")
        {
            var dpts = string.Concat(data.Select((d, i) => $"<c:dPt><c:idx val=\"{i}\"/><c:bubble3D val=\"0\"/>{Fill(ColorFor(d, i))}</c:dPt>"));
            plot = $"<c:doughnutChart><c:varyColors val=\"1\"/><c:ser><c:idx val=\"0\"/><c:order val=\"0\"/>{Tx(0, b.Title ?? "Series")}{dpts}{Labels}{Cat()}{Val(0, data.Select(d => d.Value))}</c:ser><c:firstSliceAng val=\"0\"/><c:holeSize val=\"55\"/></c:doughnutChart>";
            legend = "<c:legend><c:legendPos val=\"r\"/><c:overlay val=\"0\"/></c:legend>";
        }
        else
        {
            var stacked = type == "stacked" && b.Series != null;
            var series = new StringBuilder();
            if (stacked)
            {
                for (int s = 0; s < b.Series!.Count; s++)
                {
                    var color = SemanticChart.TryGetValue(b.Series[s], out var sc) ? sc : Palette[s % Palette.Length];
                    series.Append($"<c:ser><c:idx val=\"{s}\"/><c:order val=\"{s}\"/>{Tx(s, b.Series[s])}{Fill(color)}<c:invertIfNegative val=\"0\"/>{Cat()}{Val(s, data.Select(d => d.Values?.ElementAtOrDefault(s) ?? 0))}</c:ser>");
                }
                legend = "<c:legend><c:legendPos val=\"b\"/><c:overlay val=\"0\"/></c:legend>";
            }
            else
            {
                var dpts = string.Concat(data.Select((d, i) => $"<c:dPt><c:idx val=\"{i}\"/><c:invertIfNegative val=\"0\"/><c:bubble3D val=\"0\"/>{Fill(ColorFor(d, i))}</c:dPt>"));
                series.Append($"<c:ser><c:idx val=\"0\"/><c:order val=\"0\"/>{Tx(0, b.Title ?? "Value")}{Fill(P1)}<c:invertIfNegative val=\"0\"/>{dpts}{Labels}{Cat()}{Val(0, data.Select(d => d.Value))}</c:ser>");
            }
            var dir = type == "hbar" ? "bar" : "col";
            plot = $"<c:barChart><c:barDir val=\"{dir}\"/><c:grouping val=\"{(stacked ? "stacked" : "clustered")}\"/><c:varyColors val=\"0\"/>{series}<c:gapWidth val=\"70\"/>{(stacked ? "<c:overlap val=\"100\"/>" : "")}<c:axId val=\"50010\"/><c:axId val=\"50020\"/></c:barChart>"
                + $"<c:catAx><c:axId val=\"50010\"/><c:scaling><c:orientation val=\"{(dir == "bar" ? "maxMin" : "minMax")}\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"{(dir == "bar" ? "l" : "b")}\"/><c:numFmt formatCode=\"General\" sourceLinked=\"0\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:crossAx val=\"50020\"/><c:crosses val=\"autoZero\"/><c:auto val=\"1\"/><c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/></c:catAx>"
                + $"<c:valAx><c:axId val=\"50020\"/><c:scaling><c:orientation val=\"minMax\"/><c:min val=\"0\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"{(dir == "bar" ? "b" : "l")}\"/><c:majorGridlines><c:spPr><a:ln w=\"6350\"><a:solidFill><a:srgbClr val=\"E0E0E0\"/></a:solidFill></a:ln></c:spPr></c:majorGridlines><c:numFmt formatCode=\"General\" sourceLinked=\"0\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:crossAx val=\"50010\"/><c:crosses val=\"{(dir == "bar" ? "max" : "autoZero")}\"/><c:crossBetween val=\"between\"/></c:valAx>";
        }
        var title = string.IsNullOrEmpty(b.Title) ? "<c:autoTitleDeleted val=\"1\"/>"
            : $"<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"1100\" b=\"1\"/></a:pPr><a:r><a:rPr lang=\"en-IN\" sz=\"1100\" b=\"1\"><a:solidFill><a:srgbClr val=\"{P1}\"/></a:solidFill></a:rPr><a:t>{Esc(b.Title)}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title><c:autoTitleDeleted val=\"0\"/>";
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><c:date1904 val=\"0\"/><c:lang val=\"en-IN\"/><c:roundedCorners val=\"0\"/><c:chart>{title}<c:plotArea><c:layout/>{plot}</c:plotArea>{legend}<c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart><c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr><c:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"900\"><a:latin typeface=\"{Font}\"/></a:defRPr></a:pPr><a:endParaRPr lang=\"en-IN\"/></a:p></c:txPr></c:chartSpace>";
    }

    string Chart(Block b)
    {
        if (b.Data == null || b.Data.Count == 0) return Para("No data available for chart.", "Caption");
        if (b.Data.All(d => d.Value == 0 && (d.Values == null || d.Values.All(v => v == 0)))) return Para($"{b.Title}: no data recorded yet.", "Caption");
        _chart++;
        var id = $"rId{_rel++}";
        var xml = ChartXml(b);
        _docParts.Add((id, $"charts/chart{_chart}.xml", RelBase + "/chart", Encoding.UTF8.GetBytes(xml), "application/vnd.openxmlformats-officedocument.drawingml.chart+xml", $"/word/charts/chart{_chart}.xml"));
        var cx = 5760000; var cy = b.ChartType == "pie" ? 2700000 : Math.Max(2500000, Math.Min(4200000, 1600000 + (b.ChartType == "hbar" ? b.Data.Count * 260000 : 0)));
        var dp = _docPr++;
        return $"<w:p><w:pPr><w:keepNext/><w:jc w:val=\"center\"/></w:pPr><w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"{dp}\" name=\"Chart {dp}\"/><wp:cNvGraphicFramePr/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><c:chart r:id=\"{id}\"/></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>"
            + Para(b.Text ?? (b.Title != null ? $"Figure: {b.Title}" : null), "Caption");
    }

    // ------------------------------------------------------------------ blocks
    void RenderBlock(Block b)
    {
        switch (b.Type)
        {
            case "p": _body.Append(Para(b.Text)); break;
            case "h2":
                _headings.Add((b.Text ?? "", 2));
                _body.Append(Para(b.Text, "Heading2")); break;
            case "h3": _body.Append(Para(b.Text, "Heading3")); break;
            case "bullets":
                if (!string.IsNullOrEmpty(b.Title)) _body.Append(Para(b.Title, null, "<w:keepNext/>", "<w:b/>"));
                foreach (var i in b.Items ?? new()) _body.Append(Para(i, "ListBullet"));
                break;
            case "table":
                if (!string.IsNullOrEmpty(b.Title)) _body.Append(Para(b.Title, "TableTitle"));
                _body.Append(Table(b.Columns, b.Rows, b.Widths)); break;
            case "kv":
                if (!string.IsNullOrEmpty(b.Title)) _body.Append(Para(b.Title, "TableTitle"));
                _body.Append(KvTable(b.Rows)); break;
            case "chart": _body.Append(Chart(b)); break;
            case "callout": _body.Append(Callout(b.Title, b.Text, b.Tone)); break;
            case "pagebreak": _body.Append(PageBreak); break;
            case "signature": _body.Append(SignatureBlock()); break;
            default: _body.Append(Para(b.Text)); break;
        }
    }

    string SignatureBlock()
    {
        var rows = new List<List<string>>
        {
            new() { "Prepared by", _b.PreparedBy ?? "", "", "", "" },
            new() { "Reviewed by", _b.ReviewerName ?? "", _b.ReviewerTitle ?? "", "", "" },
            new() { "Approved by", _b.SignatoryName ?? _b.ApprovedBy ?? "", _b.SignatoryTitle ?? "", "", _b.ApprovedAt ?? "" },
        };
        var w = ColWidths(5, new() { 1.2, 1.6, 1.6, 1.6, 1.0 });
        var sb = new StringBuilder($"<w:tbl><w:tblPr><w:tblStyle w:val=\"DkGrid\"/><w:tblW w:w=\"{ContentWidth}\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/></w:tblPr><w:tblGrid>");
        foreach (var x in w) sb.Append($"<w:gridCol w:w=\"{x}\"/>");
        sb.Append("</w:tblGrid><w:tr><w:trPr><w:tblHeader/></w:trPr>");
        var hdr = new[] { "Role", "Name", "Designation", "Signature", "Date" };
        for (int i = 0; i < 5; i++) sb.Append(Cell(hdr[i], w[i], header: true));
        sb.Append("</w:tr>");
        foreach (var r in rows)
        {
            sb.Append("<w:tr><w:trPr><w:cantSplit/><w:trHeight w:val=\"720\" w:hRule=\"atLeast\"/></w:trPr>");
            for (int i = 0; i < 5; i++) sb.Append(Cell(r[i], w[i], bold: i == 0));
            sb.Append("</w:tr>");
        }
        return sb.Append("</w:tbl>").Append(Para("", "Spacer")).ToString();
    }

    // ------------------------------------------------------------------ cover designs
    string MetaRows() => KvTable(new List<List<string>>
    {
        new() { "Organisation", _b.OrgName }, new() { "Prepared for", _b.PreparedFor ?? _b.OrgName }, new() { "Prepared by", _b.PreparedBy ?? "" },
        new() { "Assessment date", _b.AssessmentDate ?? _b.GeneratedAt.ToString("dd MMM yyyy") }, new() { "Assessment period", _b.Period ?? "" },
        new() { "Reference", _b.Reference ?? "" }, new() { "Version / status", $"{_b.Version} - {_b.Status}" }, new() { "Classification", _b.Confidentiality }
    }.Where(r => !string.IsNullOrWhiteSpace(r[1])).ToList());

    string LogosLine(string align = "left", double hCm = 1.6)
    {
        var sb = new StringBuilder($"<w:p><w:pPr><w:spacing w:after=\"240\"/><w:jc w:val=\"{align}\"/></w:pPr>");
        if (_logoRelDoc != null) { var (cx, cy) = Fit(_b.Logo!, 6, hCm); sb.Append(Inline(_logoRelDoc, cx, cy, "Organisation logo")); }
        if (_cLogoRelDoc != null) { sb.Append("<w:r><w:t xml:space=\"preserve\">     </w:t></w:r>"); var (cx, cy) = Fit(_b.ConsultantLogo!, 5, hCm * 0.85); sb.Append(Inline(_cLogoRelDoc, cx, cy, "Consultant logo")); }
        return sb.Append("</w:p>").ToString();
    }

    string BandCell(string fill, int height, string inner, int width = ContentWidth, string? vAlign = "center") =>
        $"<w:tbl><w:tblPr><w:tblW w:w=\"{width}\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblCellMar><w:left w:w=\"360\" w:type=\"dxa\"/><w:right w:w=\"360\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid><w:gridCol w:w=\"{width}\"/></w:tblGrid><w:tr><w:trPr><w:trHeight w:val=\"{height}\" w:hRule=\"exact\"/></w:trPr><w:tc><w:tcPr><w:tcW w:w=\"{width}\" w:type=\"dxa\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{fill}\"/><w:vAlign w:val=\"{vAlign}\"/></w:tcPr>{(inner.Length > 0 ? inner : "<w:p/>")}</w:tc></w:tr></w:tbl>";

    string CoverText(string text, int sizeHalfPts, string color, bool bold = true, string align = "left", int after = 120) =>
        $"<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"{after}\" w:line=\"240\" w:lineRule=\"auto\"/><w:jc w:val=\"{align}\"/></w:pPr>{Runs(text, $"{(bold ? "<w:b/>" : "")}<w:color w:val=\"{color}\"/><w:sz w:val=\"{sizeHalfPts}\"/><w:szCs w:val=\"{sizeHalfPts}\"/>")}</w:p>";

    string Cover()
    {
        var title = _doc.Title;
        var sub = _doc.Subtitle ?? "Digital Personal Data Protection Act, 2023 and DPDP Rules, 2025";
        var sb = new StringBuilder();
        switch (_b.Cover)
        {
            case "Executive Minimal":
                sb.Append(LogosLine("left"));
                sb.Append(Para("", null, "<w:spacing w:before=\"2400\"/>"));
                sb.Append(CoverText(_b.OrgName.ToUpperInvariant(), 22, "7F7F7F", true, "left", 240));
                sb.Append(CoverText(title, 60, P1, true, "left", 200));
                sb.Append($"<w:p><w:pPr><w:pBdr><w:bottom w:val=\"single\" w:sz=\"24\" w:space=\"1\" w:color=\"{P2}\"/></w:pBdr><w:spacing w:after=\"240\"/></w:pPr></w:p>");
                sb.Append(CoverText(sub, 26, "404040", false, "left", 1800));
                sb.Append(MetaRows());
                break;
            case "Bold Block":
                sb.Append(BandCell(P1, 6200, LogosLineOnDark() + CoverText(title, 64, "FFFFFF", true, "left", 200) + CoverText(sub, 26, "FFFFFF", false) + CoverText(_b.OrgName, 32, P2, true)));
                sb.Append(BandCell(P2, 240, ""));
                sb.Append(Para("", null, "<w:spacing w:before=\"480\"/>"));
                sb.Append(MetaRows());
                break;
            case "Split Panel":
            {
                int left = 3000, right = ContentWidth - 3000;
                var leftInner = Para("", null, "<w:spacing w:before=\"1200\"/>") + CoverText(_b.OrgName, 36, "FFFFFF", true) + CoverText(_b.Confidentiality.ToUpperInvariant(), 20, P2, true) + CoverText($"Version {_b.Version}", 20, "FFFFFF", false);
                var rightInner = LogosLine("right") + Para("", null, "<w:spacing w:before=\"2400\"/>") + CoverText(title, 52, P1, true, "left", 200) + CoverText(sub, 24, "404040", false, "left", 600) + MetaRows();
                sb.Append($"<w:tbl><w:tblPr><w:tblW w:w=\"{ContentWidth}\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblCellMar><w:left w:w=\"240\" w:type=\"dxa\"/><w:right w:w=\"240\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid><w:gridCol w:w=\"{left}\"/><w:gridCol w:w=\"{right}\"/></w:tblGrid><w:tr><w:trPr><w:trHeight w:val=\"13600\" w:hRule=\"exact\"/></w:trPr>");
                sb.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{left}\" w:type=\"dxa\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{P1}\"/></w:tcPr>{leftInner}</w:tc>");
                sb.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{right}\" w:type=\"dxa\"/></w:tcPr>{rightInner}</w:tc></w:tr></w:tbl>");
                break;
            }
            case "Framed Classic":
                sb.Append(Para("", null, "<w:spacing w:before=\"600\"/>"));
                sb.Append(LogosLine("center", 2.2));
                sb.Append(Para("", null, "<w:spacing w:before=\"1200\"/>"));
                sb.Append(CoverText(_b.OrgName, 32, "404040", true, "center", 360));
                sb.Append(CoverText(title, 56, P1, true, "center", 240));
                sb.Append($"<w:p><w:pPr><w:pBdr><w:bottom w:val=\"double\" w:sz=\"6\" w:space=\"1\" w:color=\"{P2}\"/></w:pBdr><w:ind w:left=\"2400\" w:right=\"2400\"/><w:jc w:val=\"center\"/></w:pPr></w:p>");
                sb.Append(CoverText(sub, 24, "595959", false, "center", 1600));
                sb.Append(MetaRows());
                break;
            default: // Corporate Band
                sb.Append(LogosLine("left"));
                sb.Append(Para("", null, "<w:spacing w:before=\"1400\"/>"));
                sb.Append(BandCell(P1, 3600, CoverText(title, 56, "FFFFFF", true, "left", 160) + CoverText(sub, 24, "FFFFFF", false)));
                sb.Append(BandCell(P2, 160, ""));
                sb.Append(Para("", null, "<w:spacing w:before=\"360\"/>"));
                sb.Append(CoverText(_b.OrgName, 36, P1, true, "left", 480));
                sb.Append(MetaRows());
                break;
        }
        sb.Append(PageBreak);
        return sb.ToString();
    }

    string LogosLineOnDark() => _logoRelDoc != null || _cLogoRelDoc != null ? LogosLine("left", 1.4) : "";

    // ------------------------------------------------------------------ front matter
    string DocumentControl()
    {
        var sb = new StringBuilder();
        sb.Append(Para("Document control", "TocHeading"));
        sb.Append(KvTable(new()
        {
            new() { "Document title", _doc.Title }, new() { "Organisation", _b.OrgName }, new() { "Reference", _b.Reference ?? "-" },
            new() { "Version", _b.Version }, new() { "Status", _b.Status }, new() { "Classification", _b.Confidentiality },
            new() { "Prepared by", _b.PreparedBy ?? "-" }, new() { "Prepared for", _b.PreparedFor ?? _b.OrgName },
            new() { "Assessment period", _b.Period ?? "-" }, new() { "Generated", _b.GeneratedAt.ToString("dd MMM yyyy HH:mm") + " UTC" },
            new() { "Approved by", _b.ApprovedBy is { Length: > 0 } a ? $"{a} on {_b.ApprovedAt}" : "Not yet approved" },
        }));
        sb.Append(Para("Revision history", "TableTitle"));
        sb.Append(Table(new() { "Version", "Date", "Author", "Description" }, new() { new() { _b.Version, _b.GeneratedAt.ToString("dd MMM yyyy"), _b.PreparedBy ?? "", _b.Status == "Approved" ? "Approved release" : "Draft for review" } }, new() { 1, 1.3, 2, 3 }));
        sb.Append(Callout("Important notice", "This report records the results of a self-assessment of the organisation's processing against the Digital Personal Data Protection Act, 2023 and the Digital Personal Data Protection Rules, 2025, based on information and evidence provided. Indicators and percentages reflect assessment status only. This report is not a legal opinion, certification or proof of compliance, and generated policy templates are not legally approved policies until reviewed and approved by the organisation's legal counsel.", "warning"));
        sb.Append(PageBreak);
        return sb.ToString();
    }

    string Toc()
    {
        var sb = new StringBuilder();
        sb.Append(Para("Table of contents", "TocHeading"));
        var entries = _headings.Count > 0 ? _headings : new List<(string text, int level)> { ("Update field to build the table of contents", 1) };
        for (int i = 0; i < entries.Count; i++)
        {
            var (text, level) = entries[i];
            var page = _tocPages != null && _tocPages.TryGetValue(Norm(text), out var pg) ? pg.ToString() : "";
            sb.Append($"<w:p><w:pPr><w:pStyle w:val=\"TOC{level}\"/><w:tabs><w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"{ContentWidth}\"/></w:tabs></w:pPr>");
            if (i == 0) sb.Append("<w:r><w:fldChar w:fldCharType=\"begin\" w:dirty=\"true\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> TOC \\o \"1-2\" \\h \\z \\u </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>");
            sb.Append(Runs(text)).Append("<w:r><w:tab/></w:r>").Append(Runs(page));
            if (i == entries.Count - 1) sb.Append("<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>");
            sb.Append("</w:p>");
        }
        sb.Append(PageBreak);
        return sb.ToString();
    }

    public static string Norm(string s) => Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant();

    // ------------------------------------------------------------------ header / footer
    string HeaderXml(string? logoRel, string? watermark)
    {
        var sb = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:hdr {NsDecl}>");
        int lw = 3200, rw = ContentWidth - 3200;
        var logoRun = "";
        if (logoRel != null && _b.Logo != null) { var (cx, cy) = Fit(_b.Logo, 4.2, 0.9); logoRun = Inline(logoRel, cx, cy, "Header logo"); }
        var right = _b.LogoPlacement == "Header right";
        var textCell = $"<w:p><w:pPr><w:pStyle w:val=\"Header\"/><w:jc w:val=\"{(right ? "left" : "right")}\"/></w:pPr>{Runs(_b.HeaderText ?? _b.OrgName, $"<w:b/><w:color w:val=\"{P1}\"/>")}</w:p><w:p><w:pPr><w:pStyle w:val=\"Header\"/><w:jc w:val=\"{(right ? "left" : "right")}\"/></w:pPr>{Runs(_doc.Title, "<w:color w:val=\"7F7F7F\"/>")}</w:p>";
        var logoCell = $"<w:p><w:pPr><w:pStyle w:val=\"Header\"/><w:jc w:val=\"{(right ? "right" : "left")}\"/></w:pPr>{logoRun}</w:p>";
        sb.Append($"<w:tbl><w:tblPr><w:tblW w:w=\"{ContentWidth}\" w:type=\"dxa\"/><w:tblBorders><w:bottom w:val=\"single\" w:sz=\"8\" w:space=\"0\" w:color=\"{P1}\"/></w:tblBorders><w:tblLayout w:type=\"fixed\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"{(right ? rw : lw)}\"/><w:gridCol w:w=\"{(right ? lw : rw)}\"/></w:tblGrid><w:tr>");
        sb.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{(right ? rw : lw)}\" w:type=\"dxa\"/><w:vAlign w:val=\"bottom\"/></w:tcPr>{(right ? textCell : logoCell)}</w:tc>");
        sb.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{(right ? lw : rw)}\" w:type=\"dxa\"/><w:vAlign w:val=\"bottom\"/></w:tcPr>{(right ? logoCell : textCell)}</w:tc></w:tr></w:tbl>");
        sb.Append("<w:p><w:pPr><w:pStyle w:val=\"Header\"/></w:pPr>");
        if (!string.IsNullOrWhiteSpace(watermark)) sb.Append(Watermark(watermark));
        sb.Append("</w:p></w:hdr>");
        return sb.ToString();
    }

    static string Watermark(string text) =>
        "<w:r><w:rPr><w:noProof/></w:rPr><w:pict><v:shapetype id=\"_x0000_t136\" coordsize=\"21600,21600\" o:spt=\"136\" adj=\"10800\" path=\"m@7,l@8,m@5,21600l@6,21600e\"><v:formulas><v:f eqn=\"sum #0 0 10800\"/><v:f eqn=\"prod #0 2 1\"/><v:f eqn=\"sum 21600 0 @1\"/><v:f eqn=\"sum 0 0 @2\"/><v:f eqn=\"sum 21600 0 @3\"/><v:f eqn=\"if @0 @3 0\"/><v:f eqn=\"if @0 21600 @1\"/><v:f eqn=\"if @0 0 @2\"/><v:f eqn=\"if @0 @4 21600\"/><v:f eqn=\"mid @5 @6\"/><v:f eqn=\"mid @8 @5\"/><v:f eqn=\"mid @7 @8\"/><v:f eqn=\"mid @6 @7\"/><v:f eqn=\"sum @6 0 @5\"/></v:formulas><v:path textpathok=\"t\" o:connecttype=\"custom\" o:connectlocs=\"@9,0;@10,10800;@11,21600;@12,10800\" o:connectangles=\"270,180,90,0\"/><v:textpath on=\"t\" fitshape=\"t\"/><v:handles><v:h position=\"#0,bottomRight\" xrange=\"6629,14971\"/></v:handles><o:lock v:ext=\"edit\" text=\"t\" shapetype=\"t\"/></v:shapetype>"
        + $"<v:shape id=\"PowerPlusWaterMarkObject1\" o:spid=\"_x0000_s2049\" type=\"#_x0000_t136\" style=\"position:absolute;margin-left:0;margin-top:0;width:440pt;height:110pt;rotation:315;z-index:-251654144;mso-position-horizontal:center;mso-position-horizontal-relative:margin;mso-position-vertical:center;mso-position-vertical-relative:margin\" o:allowincell=\"f\" fillcolor=\"#d8d8d8\" stroked=\"f\"><v:fill opacity=\".45\"/><v:textpath style=\"font-family:&quot;Calibri&quot;;font-size:1pt\" string=\"{Esc(text)}\"/><w10:wrap anchorx=\"margin\" anchory=\"margin\"/></v:shape></w:pict></w:r>";

    string FooterXml()
    {
        int a = 4200, b2 = 2600, c = ContentWidth - a - b2;
        string Fld(string instr) => $"<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> {instr} </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:ftr {NsDecl}>"
            + $"<w:tbl><w:tblPr><w:tblW w:w=\"{ContentWidth}\" w:type=\"dxa\"/><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"BFBFBF\"/></w:tblBorders><w:tblLayout w:type=\"fixed\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"{a}\"/><w:gridCol w:w=\"{b2}\"/><w:gridCol w:w=\"{c}\"/></w:tblGrid><w:tr>"
            + $"<w:tc><w:tcPr><w:tcW w:w=\"{a}\" w:type=\"dxa\"/></w:tcPr><w:p><w:pPr><w:pStyle w:val=\"Footer\"/></w:pPr>{Runs(_b.FooterText ?? $"{_b.OrgName} | Ref: {_b.Reference ?? "-"} | v{_b.Version}")}</w:p></w:tc>"
            + $"<w:tc><w:tcPr><w:tcW w:w=\"{b2}\" w:type=\"dxa\"/></w:tcPr><w:p><w:pPr><w:pStyle w:val=\"Footer\"/><w:jc w:val=\"center\"/></w:pPr>{Runs(_b.Confidentiality.ToUpperInvariant(), "<w:b/><w:color w:val=\"C00000\"/>")}</w:p></w:tc>"
            + $"<w:tc><w:tcPr><w:tcW w:w=\"{c}\" w:type=\"dxa\"/></w:tcPr><w:p><w:pPr><w:pStyle w:val=\"Footer\"/><w:jc w:val=\"right\"/></w:pPr><w:r><w:t xml:space=\"preserve\">Page </w:t></w:r>{Fld("PAGE")}<w:r><w:t xml:space=\"preserve\"> of </w:t></w:r>{Fld("NUMPAGES")}</w:p></w:tc>"
            + "</w:tr></w:tbl><w:p><w:pPr><w:pStyle w:val=\"Footer\"/><w:spacing w:after=\"0\"/></w:pPr></w:p></w:ftr>";
    }

    // ------------------------------------------------------------------ styles etc.
    string StylesXml()
    {
        string F(string size, bool b = false, string? color = null) => $"<w:rPr><w:rFonts w:ascii=\"{Font}\" w:hAnsi=\"{Font}\" w:cs=\"{Font}\" w:eastAsia=\"{Font}\"/>{(b ? "<w:b/><w:bCs/>" : "")}{(color != null ? $"<w:color w:val=\"{color}\"/>" : "")}<w:sz w:val=\"{size}\"/><w:szCs w:val=\"{size}\"/></w:rPr>";
        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:styles xmlns:w="{W}"><w:docDefaults><w:rPrDefault>{F("21")}</w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="120" w:line="276" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>"""
            + $"""<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/><w:pPr><w:jc w:val="both"/></w:pPr>{F("21", false, "262626")}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:keepLines/><w:pBdr><w:bottom w:val="single" w:sz="12" w:space="4" w:color="{P2}"/></w:pBdr><w:spacing w:before="360" w:after="200"/><w:jc w:val="left"/><w:outlineLvl w:val="0"/></w:pPr>{F("32", true, P1)}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:keepLines/><w:spacing w:before="280" w:after="120"/><w:jc w:val="left"/><w:outlineLvl w:val="1"/></w:pPr>{F("26", true, P1)}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="Heading3"><w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="200" w:after="80"/><w:jc w:val="left"/><w:outlineLvl w:val="2"/></w:pPr>{F("22", true, "404040")}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="TocHeading"><w:name w:val="TOC Heading"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:spacing w:before="0" w:after="240"/><w:jc w:val="left"/></w:pPr>{F("32", true, P1)}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="TOC1"><w:name w:val="toc 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:spacing w:before="120" w:after="40"/><w:jc w:val="left"/></w:pPr>{F("21", true)}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="TOC2"><w:name w:val="toc 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:spacing w:after="40"/><w:ind w:left="360"/><w:jc w:val="left"/></w:pPr>{F("20")}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="ListBullet"><w:name w:val="List Bullet"/><w:basedOn w:val="Normal"/><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr><w:spacing w:after="60"/><w:jc w:val="left"/></w:pPr></w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="TableText"><w:name w:val="Table Text"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:before="20" w:after="20" w:line="240" w:lineRule="auto"/><w:jc w:val="left"/></w:pPr>{F("17")}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="TableTitle"><w:name w:val="Table Title"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="120" w:after="60"/><w:jc w:val="left"/></w:pPr>{F("20", true, P1)}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="Caption"><w:name w:val="caption"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="200"/><w:jc w:val="center"/></w:pPr><w:rPr><w:i/><w:color w:val="7F7F7F"/><w:sz w:val="17"/></w:rPr></w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="Spacer"><w:name w:val="Spacer"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:before="0" w:after="0" w:line="120" w:lineRule="exact"/></w:pPr><w:rPr><w:sz w:val="8"/></w:rPr></w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="Header"><w:name w:val="header"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/><w:jc w:val="left"/></w:pPr>{F("16")}</w:style>"""
            + $"""<w:style w:type="paragraph" w:styleId="Footer"><w:name w:val="footer"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/><w:jc w:val="left"/></w:pPr>{F("16", false, "595959")}</w:style>"""
            + $"""<w:style w:type="table" w:default="1" w:styleId="TableNormal"><w:name w:val="Normal Table"/><w:tblPr><w:tblInd w:w="0" w:type="dxa"/><w:tblCellMar><w:top w:w="0" w:type="dxa"/><w:left w:w="108" w:type="dxa"/><w:bottom w:w="0" w:type="dxa"/><w:right w:w="108" w:type="dxa"/></w:tblCellMar></w:tblPr></w:style>"""
            + $"""<w:style w:type="table" w:styleId="DkGrid"><w:name w:val="Data Kavach Grid"/><w:basedOn w:val="TableNormal"/><w:tblPr><w:tblBorders><w:top w:val="single" w:sz="4" w:space="0" w:color="BFC9D9"/><w:left w:val="single" w:sz="4" w:space="0" w:color="BFC9D9"/><w:bottom w:val="single" w:sz="4" w:space="0" w:color="BFC9D9"/><w:right w:val="single" w:sz="4" w:space="0" w:color="BFC9D9"/><w:insideH w:val="single" w:sz="4" w:space="0" w:color="BFC9D9"/><w:insideV w:val="single" w:sz="4" w:space="0" w:color="BFC9D9"/></w:tblBorders><w:tblCellMar><w:top w:w="40" w:type="dxa"/><w:left w:w="90" w:type="dxa"/><w:bottom w:w="40" w:type="dxa"/><w:right w:w="90" w:type="dxa"/></w:tblCellMar></w:tblPr></w:style>"""
            + "</w:styles>";
    }

    static string NumberingXml() => $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:numbering xmlns:w="{W}"><w:abstractNum w:abstractNumId="0"><w:multiLevelType w:val="hybridMultilevel"/><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="&#8226;"/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="360" w:hanging="270"/></w:pPr><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/></w:rPr></w:lvl></w:abstractNum><w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num></w:numbering>""";

    static string SettingsXml() => $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:settings xmlns:w="{W}"><w:zoom w:percent="100"/><w:defaultTabStop w:val="720"/><w:characterSpacingControl w:val="doNotCompress"/><w:updateFields w:val="true"/><w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/></w:compat></w:settings>""";

    // ------------------------------------------------------------------ build
    public byte[] Build()
    {
        if (_b.Logo != null && _b.LogoPlacement != "none") _logoRelDoc = AddDocImage(_b.Logo, _b.LogoExt, "logo");
        if (_b.ConsultantLogo != null) _cLogoRelDoc = AddDocImage(_b.ConsultantLogo, _b.ConsultantLogoExt, "consultant_logo");

        // body sections first (collect headings for the TOC)
        int n1 = 0, n2 = 0;
        var sections = _doc.Sections.Where(s => s.Include).ToList();
        foreach (var s in sections)
        {
            if (s.PageBreakBefore && _body.Length > 0) _body.Append(PageBreak);
            string heading;
            if (s.Level <= 1) { n1++; n2 = 0; heading = $"{n1}. {s.Heading}"; _headings.Add((heading, 1)); _body.Append(Para(heading, "Heading1")); }
            else { n2++; heading = $"{n1}.{n2} {s.Heading}"; _headings.Add((heading, 2)); _body.Append(Para(heading, "Heading2")); }
            foreach (var b in s.Blocks) RenderBlock(b);
        }
        var bodyXml = _body.ToString();
        _body.Clear();
        var front = Cover() + DocumentControl() + Toc();

        var sect = $"<w:sectPr><w:headerReference w:type=\"default\" r:id=\"rIdHdr1\"/><w:headerReference w:type=\"first\" r:id=\"rIdHdr2\"/><w:footerReference w:type=\"default\" r:id=\"rIdFtr1\"/><w:footerReference w:type=\"first\" r:id=\"rIdFtr2\"/><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1418\" w:right=\"1134\" w:bottom=\"1134\" w:left=\"1134\" w:header=\"567\" w:footer=\"454\" w:gutter=\"0\"/>"
            + (_b.Cover == "Framed Classic" ? $"<w:pgBorders w:display=\"firstPage\" w:offsetFrom=\"page\"><w:top w:val=\"double\" w:sz=\"12\" w:space=\"24\" w:color=\"{P1}\"/><w:left w:val=\"double\" w:sz=\"12\" w:space=\"24\" w:color=\"{P1}\"/><w:bottom w:val=\"double\" w:sz=\"12\" w:space=\"24\" w:color=\"{P1}\"/><w:right w:val=\"double\" w:sz=\"12\" w:space=\"24\" w:color=\"{P1}\"/></w:pgBorders>" : "")
            + "<w:cols w:space=\"708\"/><w:titlePg/><w:docGrid w:linePitch=\"360\"/></w:sectPr>";
        var documentXml = $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document {NsDecl}><w:body>{front}{bodyXml}{sect}</w:body></w:document>";

        // ---- OPC package (explicit content types and relationships, as written by Word)
        var parts = new List<(string path, string ct, byte[] data)>();
        var rels = new Dictionary<string, List<(string id, string type, string target)>>();
        void AddPart(string path, string ct, byte[] data) => parts.Add((path, ct, data));
        void AddXml(string path, string ct, string xml) => AddPart(path, ct, Encoding.UTF8.GetBytes(xml));
        void Rel(string source, string id, string type, string target) { if (!rels.TryGetValue(source, out var l)) rels[source] = l = new(); l.Add((id, type, target)); }
        const string Ct = "application/vnd.openxmlformats-officedocument.wordprocessingml.";

        AddXml("word/document.xml", Ct + "document.main+xml", documentXml);
        Rel("", "rId1", RelBase + "/officeDocument", "word/document.xml");
        Rel("", "rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
        Rel("", "rId3", RelBase + "/extended-properties", "docProps/app.xml");
        AddXml("word/styles.xml", Ct + "styles+xml", StylesXml()); Rel("word/document.xml", "rIdStyles", RelBase + "/styles", "styles.xml");
        AddXml("word/numbering.xml", Ct + "numbering+xml", NumberingXml()); Rel("word/document.xml", "rIdNum", RelBase + "/numbering", "numbering.xml");
        AddXml("word/settings.xml", Ct + "settings+xml", SettingsXml()); Rel("word/document.xml", "rIdSettings", RelBase + "/settings", "settings.xml");

        var headerLogo = _b.Logo != null && _b.LogoPlacement != "Cover only";
        AddXml("word/header1.xml", Ct + "header+xml", HeaderXml(headerLogo ? "rIdHLogo" : null, _b.Watermark));
        Rel("word/document.xml", "rIdHdr1", RelBase + "/header", "header1.xml");
        if (headerLogo)
        {
            var ext = (_b.LogoExt ?? ".png").ToLowerInvariant();
            AddPart($"word/media/header_logo{ext}", ImgContentType(ext), _b.Logo!);
            Rel("word/header1.xml", "rIdHLogo", RelBase + "/image", $"media/header_logo{ext}");
        }
        var wmFirst = string.IsNullOrWhiteSpace(_b.Watermark) ? "<w:p><w:pPr><w:pStyle w:val=\"Header\"/></w:pPr></w:p>" : $"<w:p><w:pPr><w:pStyle w:val=\"Header\"/></w:pPr>{Watermark(_b.Watermark)}</w:p>";
        AddXml("word/header2.xml", Ct + "header+xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:hdr {NsDecl}>{wmFirst}</w:hdr>");
        Rel("word/document.xml", "rIdHdr2", RelBase + "/header", "header2.xml");
        AddXml("word/footer1.xml", Ct + "footer+xml", FooterXml());
        Rel("word/document.xml", "rIdFtr1", RelBase + "/footer", "footer1.xml");
        AddXml("word/footer2.xml", Ct + "footer+xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:ftr {NsDecl}><w:p><w:pPr><w:pStyle w:val=\"Footer\"/><w:jc w:val=\"center\"/></w:pPr>{Runs(_b.Confidentiality.ToUpperInvariant(), "<w:b/><w:color w:val=\"C00000\"/>")}</w:p></w:ftr>");
        Rel("word/document.xml", "rIdFtr2", RelBase + "/footer", "footer2.xml");
        foreach (var (id, target, type, data, ct, partName) in _docParts)
        {
            AddPart(partName.TrimStart('/'), ct, data);
            Rel("word/document.xml", id, type, target);
        }
        string Iso(DateTime d) => d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        AddXml("docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
            + $"<dc:title>{Esc(_doc.Title)}</dc:title><dc:subject>DPDP compliance - {Esc(_b.OrgName)}</dc:subject><dc:creator>{Esc(_b.PreparedBy ?? "Data Kavach")}</dc:creator><cp:keywords>DPDP; {Esc(_b.Confidentiality)}</cp:keywords><cp:category>{Esc(_b.Confidentiality)}</cp:category><cp:version>{Esc(_b.Version)}</cp:version>"
            + $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{Iso(_b.GeneratedAt)}</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">{Iso(DateTime.UtcNow)}</dcterms:modified></cp:coreProperties>");
        AddXml("docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml",
            $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>Data Kavach</Application><Company>{Esc(_b.OrgName)}</Company><DocSecurity>0</DocSecurity></Properties>");

        var types = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        foreach (var ext in parts.Where(p => p.ct.StartsWith("image/")).Select(p => (Path.GetExtension(p.path).TrimStart('.'), p.ct)).Distinct())
            types.Append($"<Default Extension=\"{ext.Item1}\" ContentType=\"{ext.ct}\"/>");
        foreach (var p in parts.Where(p => !p.ct.StartsWith("image/")))
            types.Append($"<Override PartName=\"/{p.path}\" ContentType=\"{p.ct}\"/>");
        types.Append("</Types>");

        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            void Write(string path, byte[] data)
            {
                var e = zip.CreateEntry(path, System.IO.Compression.CompressionLevel.Optimal);
                using var st = e.Open();
                st.Write(data);
            }
            Write("[Content_Types].xml", Encoding.UTF8.GetBytes(types.ToString()));
            foreach (var (source, list) in rels)
            {
                var rp = source.Length == 0 ? "_rels/.rels" : $"{Path.GetDirectoryName(source)!.Replace('\\', '/')}/_rels/{Path.GetFileName(source)}.rels";
                var x = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                foreach (var (id, type, target) in list) x.Append($"<Relationship Id=\"{id}\" Type=\"{type}\" Target=\"{Esc(target)}\"/>");
                Write(rp, Encoding.UTF8.GetBytes(x.Append("</Relationships>").ToString()));
            }
            foreach (var p in parts) Write(p.path, p.data);
        }
        return ms.ToArray();
    }
}
