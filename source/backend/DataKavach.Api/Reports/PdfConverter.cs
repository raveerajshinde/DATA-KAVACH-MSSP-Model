using System.Diagnostics;
using System.Text.Json;

namespace DataKavach.Reports;

public sealed class PdfOptions
{
    /// <summary>Path to soffice(.exe). Auto-detected when empty.</summary>
    public string SofficePath { get; set; } = "";
    /// <summary>Python interpreter with the 'uno' module (LibreOffice's bundled python on Windows). Auto-detected when empty.</summary>
    public string PythonPath { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 180;
}

/// <summary>Converts DOCX to PDF with LibreOffice (headless), updating the TOC so page numbers are correct.</summary>
public sealed class PdfConverter(Microsoft.Extensions.Options.IOptions<PdfOptions> opt, ILogger<PdfConverter> log)
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    readonly PdfOptions _o = opt.Value;

    public string? Soffice => Resolve(_o.SofficePath, OperatingSystem.IsWindows()
        ? new[] { @"C:\Program Files\LibreOffice\program\soffice.exe", @"C:\Program Files (x86)\LibreOffice\program\soffice.exe" }
        : new[] { "/usr/bin/soffice", "/usr/lib/libreoffice/program/soffice", "/opt/libreoffice/program/soffice" });

    public string? Python
    {
        get
        {
            if (!string.IsNullOrEmpty(_o.PythonPath) && File.Exists(_o.PythonPath)) return _o.PythonPath;
            if (OperatingSystem.IsWindows())
            {
                var so = Soffice;
                if (so != null) { var p = Path.Combine(Path.GetDirectoryName(so)!, "python.exe"); if (File.Exists(p)) return p; }
                return null;
            }
            return Resolve("", new[] { "/usr/bin/python3", "/usr/local/bin/python3" });
        }
    }

    static string? Resolve(string configured, string[] candidates)
    {
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured)) return configured;
        return candidates.FirstOrDefault(File.Exists);
    }

    public bool Available => Soffice != null;

    public sealed record Result(byte[] Pdf, Dictionary<string, int> TocPages);

    public async Task<Result> ConvertAsync(byte[] docx, CancellationToken ct = default)
    {
        var soffice = Soffice ?? throw new InvalidOperationException("LibreOffice is not installed. Install LibreOffice (the installer can do this) to enable PDF export.");
        var work = Directory.CreateTempSubdirectory("dk_pdf_").FullName;
        await Gate.WaitAsync(ct);
        try
        {
            var src = Path.Combine(work, "report.docx");
            var pdf = Path.Combine(work, "report.pdf");
            var toc = Path.Combine(work, "toc.json");
            await File.WriteAllBytesAsync(src, docx, ct);
            var script = Path.Combine(AppContext.BaseDirectory, "Tools", "dk_convert.py");
            var py = Python;
            if (py != null && File.Exists(script))
            {
                var (code, err) = await Run(py, new[] { script, soffice, src, pdf, toc }, ct);
                if (code != 0 || !File.Exists(pdf)) log.LogWarning("UNO conversion failed ({Code}): {Err}. Falling back to --convert-to.", code, err);
            }
            if (!File.Exists(pdf))
            {
                var profile = Path.Combine(work, "profile");
                var (code, err) = await Run(soffice, new[] { "--headless", "--norestore", "--nolockcheck", $"-env:UserInstallation={new Uri(profile).AbsoluteUri}", "--convert-to", "pdf", "--outdir", work, src }, ct);
                if (!File.Exists(pdf)) throw new InvalidOperationException($"PDF conversion failed ({code}): {err}");
            }
            var pages = new Dictionary<string, int>();
            if (File.Exists(toc))
                foreach (var e in JsonSerializer.Deserialize<List<JsonElement>>(await File.ReadAllTextAsync(toc, ct)) ?? new())
                    pages.TryAdd(DocxWriter.Norm(e.GetProperty("title").GetString() ?? ""), e.GetProperty("page").GetInt32());
            return new Result(await File.ReadAllBytesAsync(pdf, ct), pages);
        }
        finally
        {
            Gate.Release();
            try { Directory.Delete(work, true); } catch { }
        }
    }

    async Task<(int code, string err)> Run(string exe, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (!OperatingSystem.IsWindows()) psi.Environment["HOME"] = Path.GetTempPath();
        using var p = Process.Start(psi)!;
        var errTask = p.StandardError.ReadToEndAsync(ct);
        _ = p.StandardOutput.ReadToEndAsync(ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_o.TimeoutSeconds));
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } return (-1, "timeout"); }
        return (p.ExitCode, await errTask);
    }
}
