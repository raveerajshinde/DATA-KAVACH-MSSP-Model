using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;
using DataKavach.Auth;
using DataKavach.Data;

namespace DataKavach.Services;

/// <summary>CSV and XLSX import/export for register-type entities (inventory, data flows, vendors...).</summary>
public sealed class ImportExportService(EntityService es, AuditService audit)
{
    static List<Col> IoCols(EntityDef e) => e.Cols.Where(c => !c.Hidden && !c.ReadOnly).ToList();

    public async Task<(byte[] data, string contentType, string fileName)> ExportAsync(EntityDef e, CurrentUser u, long orgId, string format)
    {
        await es.Require(u, $"{e.Module}.export");
        var rows = await es.AllAsync(e.Key, orgId);
        var cols = IoCols(e);
        var table = new List<string[]> { cols.Select(c => c.DisplayLabel).ToArray() };
        table.AddRange(rows.Select(r => cols.Select(c => Cell(r.GetValueOrDefault(c.Name), c)).ToArray()));
        await audit.LogAsync("EXPORT", e.Key, null, orgId, new { format, rows = rows.Count }, u);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd");
        return format == "xlsx"
            ? (Xlsx.Write(e.Label, table), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{e.Label.Replace(' ', '_')}_{stamp}.xlsx")
            : (Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv.Write(table))).ToArray(), "text/csv", $"{e.Label.Replace(' ', '_')}_{stamp}.csv");
    }

    public async Task<(byte[] data, string fileName)> TemplateAsync(EntityDef e, CurrentUser u)
    {
        await es.Require(u, $"{e.Module}.view");
        var cols = IoCols(e);
        var rows = new List<string[]> { cols.Select(c => c.DisplayLabel).ToArray(), cols.Select(c => c.Options != null ? string.Join(" | ", c.Options.Take(4)) : c.Type.ToString()).ToArray() };
        return (Xlsx.Write("Template", rows), $"{e.Label.Replace(' ', '_')}_import_template.xlsx");
    }

    static string Cell(object? v, Col c) => c.Type switch
    {
        ColType.Bool => Db.Bool(v) ? "Yes" : "No",
        ColType.Date => Db.Dt(v)?.ToString("yyyy-MM-dd") ?? "",
        ColType.DateTime => Db.Dt(v)?.ToString("yyyy-MM-dd HH:mm") ?? "",
        _ => Db.Str(v) ?? ""
    };

    public async Task<object> ImportAsync(EntityDef e, CurrentUser u, long orgId, IFormFile file)
    {
        await es.Require(u, $"{e.Module}.edit");
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var table = ext == ".xlsx" ? Xlsx.Read(ms.ToArray()) : ext == ".csv" ? Csv.Read(Encoding.UTF8.GetString(ms.ToArray()).TrimStart('﻿')) : throw ApiError.Bad("Upload a .csv or .xlsx file");
        if (table.Count < 2) throw ApiError.Bad("No data rows found");
        var cols = IoCols(e);
        var header = table[0].Select(h => h.Trim()).ToArray();
        var map = new Dictionary<int, Col>();
        for (int i = 0; i < header.Length; i++)
        {
            var c = cols.FirstOrDefault(c => c.DisplayLabel.Equals(header[i], StringComparison.OrdinalIgnoreCase) || c.Name.Equals(header[i], StringComparison.OrdinalIgnoreCase));
            if (c != null) map[i] = c;
        }
        if (map.Count == 0) throw ApiError.Bad("No recognised column headers. Download the import template.");
        int ok = 0; var errors = new List<string>();
        for (int r = 1; r < table.Count; r++)
        {
            var row = table[r];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            try
            {
                var vals = new Dictionary<string, object?>();
                foreach (var (i, c) in map)
                {
                    var raw = i < row.Length ? row[i]?.Trim() : null;
                    if (c.Type is ColType.Date or ColType.DateTime && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var oa) && oa > 20000 && oa < 80000)
                        raw = DateTime.FromOADate(oa).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    if (c.Type == ColType.Bool) raw = raw?.ToLowerInvariant() is "yes" or "y" or "true" or "1" ? "true" : "false";
                    if (c.Options != null && !c.Multi && !string.IsNullOrEmpty(raw))
                        raw = c.Options.FirstOrDefault(o => o.Equals(raw, StringComparison.OrdinalIgnoreCase)) ?? raw;
                    vals[c.Name] = EntityService.Coerce(c, raw);
                }
                await es.CreateAsync(e, u, orgId, vals);
                ok++;
            }
            catch (ApiError ex) { errors.Add($"Row {r + 1}: {ex.Message}"); }
        }
        await audit.LogAsync("IMPORT", e.Key, null, orgId, new { file = file.FileName, imported = ok, errors = errors.Count }, u);
        return new { imported = ok, errors };
    }
}

public static class Csv
{
    public static string Write(List<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var r in rows) sb.Append(string.Join(",", r.Select(Esc))).Append("\r\n");
        return sb.ToString();
        static string Esc(string s)
        {
            if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;          // formula-injection guard
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }

    public static List<string[]> Read(string text)
    {
        var rows = new List<string[]>(); var cur = new List<string>(); var sb = new StringBuilder(); bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (q)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') q = false;
                else sb.Append(ch);
            }
            else if (ch == '"') q = true;
            else if (ch == ',') { cur.Add(sb.ToString()); sb.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                cur.Add(sb.ToString()); sb.Clear(); rows.Add(cur.ToArray()); cur.Clear();
            }
            else sb.Append(ch);
        }
        if (sb.Length > 0 || cur.Count > 0) { cur.Add(sb.ToString()); rows.Add(cur.ToArray()); }
        return rows;
    }
}

/// <summary>Minimal SpreadsheetML (xlsx) reader/writer - inline strings, one sheet.</summary>
public static class Xlsx
{
    static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    static string ColName(int i) { var s = ""; i++; while (i > 0) { var m = (i - 1) % 26; s = (char)('A' + m) + s; i = (i - 1) / 26; } return s; }

    public static byte[] Write(string sheetName, List<string[]> rows)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string path, string content) { using var w = new StreamWriter(z.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false)); w.Write(content); }
            Add("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Add("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            var name = SecurityElement.Escape(sheetName.Length > 31 ? sheetName[..31] : sheetName);
            Add("xl/workbook.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{name}" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add("xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            Add("xl/styles.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF0B3D91"/></patternFill></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="3"><xf/><xf fontId="1" fillId="2" applyFont="1" applyFill="1"/><xf applyAlignment="1"><alignment wrapText="1" vertical="top"/></xf></cellXfs></styleSheet>""");
            var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews><cols>""");
            var ncol = rows.Count > 0 ? rows.Max(r => r.Length) : 1;
            for (int i = 0; i < ncol; i++) sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"28\" customWidth=\"1\"/>");
            sb.Append("</cols><sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                sb.Append($"<row r=\"{r + 1}\">");
                for (int c = 0; c < rows[r].Length; c++)
                    sb.Append($"<c r=\"{ColName(c)}{r + 1}\" t=\"inlineStr\" s=\"{(r == 0 ? 1 : 2)}\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(Clean(rows[r][c]))}</t></is></c>");
                sb.Append("</row>");
            }
            sb.Append("</sheetData>");
            if (rows.Count > 0) sb.Append($"<autoFilter ref=\"A1:{ColName(ncol - 1)}{rows.Count}\"/>");
            sb.Append("</worksheet>");
            Add("xl/worksheets/sheet1.xml", sb.ToString());
        }
        return ms.ToArray();
    }

    static string Clean(string s) => new(s.Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || ch >= 0x20).ToArray());

    public static List<string[]> Read(byte[] data)
    {
        using var z = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        var shared = new List<string>();
        var sst = z.GetEntry("xl/sharedStrings.xml");
        if (sst != null)
        {
            using var s = sst.Open();
            shared = XDocument.Load(s).Root!.Elements(S + "si").Select(si => string.Concat(si.Descendants(S + "t").Select(t => t.Value))).ToList();
        }
        var wbRels = XDocument.Load(z.GetEntry("xl/_rels/workbook.xml.rels")!.Open());
        var wb = XDocument.Load(z.GetEntry("xl/workbook.xml")!.Open());
        var rid = wb.Root!.Element(S + "sheets")!.Elements(S + "sheet").First().Attribute(R + "id")!.Value;
        var target = wbRels.Root!.Elements().First(e => e.Attribute("Id")!.Value == rid).Attribute("Target")!.Value.TrimStart('/');
        if (!target.StartsWith("xl/")) target = "xl/" + target;
        var ws = XDocument.Load(z.GetEntry(target)!.Open());
        var rows = new List<string[]>();
        foreach (var row in ws.Descendants(S + "row"))
        {
            var cells = new SortedDictionary<int, string>();
            foreach (var c in row.Elements(S + "c"))
            {
                var refc = c.Attribute("r")?.Value ?? "";
                int col = 0;
                foreach (var ch in refc.TakeWhile(char.IsLetter)) col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
                col = Math.Max(0, col - 1);
                var t = c.Attribute("t")?.Value;
                var v = c.Element(S + "v")?.Value;
                string val = t switch
                {
                    "s" => v != null && int.TryParse(v, out var si) && si < shared.Count ? shared[si] : "",
                    "inlineStr" => string.Concat(c.Descendants(S + "t").Select(x => x.Value)),
                    "b" => v == "1" ? "true" : "false",
                    _ => v ?? ""
                };
                cells[col] = val;
            }
            var arr = new string[cells.Count == 0 ? 0 : cells.Keys.Max() + 1];
            for (int i = 0; i < arr.Length; i++) arr[i] = cells.GetValueOrDefault(i) ?? "";
            rows.Add(arr);
        }
        return rows;
    }
}
