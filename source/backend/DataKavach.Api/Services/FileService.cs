using DataKavach.Auth;
using DataKavach.Data;
using Microsoft.Extensions.Options;

namespace DataKavach.Services;

public sealed class FileService(Db db, IOptions<StorageOptions> opt, AuditService audit, EntityService es)
{
    readonly StorageOptions _o = opt.Value;
    public string Root => Path.GetFullPath(_o.Root);

    public string Abs(string rel)
    {
        var full = Path.GetFullPath(Path.Combine(Root, rel));
        if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw ApiError.Bad("Invalid path");
        return full;
    }

    public static string SafeName(string name)
    {
        name = Path.GetFileName(name);
        foreach (var c in Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':' })) name = name.Replace(c, '_');
        return name.Length > 200 ? name[^200..] : name;
    }

    public async Task<(string rel, string sha, long size)> StoreAsync(IFormFile file, string folder, string[]? allowed = null)
    {
        if (file.Length == 0) throw ApiError.Bad("Empty file");
        if (file.Length > _o.MaxUploadBytes) throw ApiError.Bad($"File exceeds the {_o.MaxUploadBytes / 1024 / 1024} MB limit");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!(allowed ?? _o.AllowedExtensions).Contains(ext)) throw ApiError.Bad($"File type {ext} is not allowed");
        var rel = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}.bin");
        var abs = Abs(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        await using (var fs = File.Create(abs)) await file.CopyToAsync(fs);
        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif")
        {
            var head = new byte[8];
            await using (var f = File.OpenRead(abs)) _ = await f.ReadAsync(head);
            bool ok = ext == ".png" ? head[0] == 0x89 && head[1] == 0x50 : ext == ".gif" ? head[0] == 0x47 : head[0] == 0xFF && head[1] == 0xD8;
            if (!ok) { File.Delete(abs); throw ApiError.Bad("File content does not match its image type"); }
        }
        return (rel, await Hashing.Sha256FileAsync(abs), file.Length);
    }

    public async Task<long> StoreGenericAsync(IFormFile file, long orgId, string purpose, CurrentUser u, string[]? allowed = null)
    {
        var (rel, sha, size) = await StoreAsync(file, Path.Combine("org" + orgId, purpose), allowed);
        var id = await db.InsertAsync("StoredFiles", new Dictionary<string, object?>
        {
            ["OrganizationId"] = orgId, ["Purpose"] = purpose, ["FileName"] = SafeName(file.FileName), ["ContentType"] = file.ContentType, ["SizeBytes"] = size, ["Sha256"] = sha, ["StoragePath"] = rel,
            ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email
        });
        await audit.LogAsync("UPLOAD", "files", id, orgId, new { purpose, file = file.FileName, sha }, u);
        return id;
    }

    public async Task<long> CreateEvidenceAsync(CurrentUser u, long orgId, string title, string? description, string? category, string? linkedEntity, long? linkedId, IFormFile file, string? comment)
    {
        await es.Require(u, "evidence.edit");
        if (string.IsNullOrWhiteSpace(title)) title = Path.GetFileNameWithoutExtension(file.FileName);
        var id = await db.InsertAsync("Evidence", new Dictionary<string, object?>
        {
            ["OrganizationId"] = orgId, ["Title"] = title.Length > 300 ? title[..300] : title, ["Description"] = description, ["Category"] = category ?? "Other",
            ["LinkedEntity"] = linkedEntity, ["LinkedId"] = linkedId ?? 0, ["CurrentVersion"] = 0, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email, ["IsDeleted"] = false
        });
        await audit.LogAsync("CREATE", "evidence", id, orgId, new { title, linkedEntity, linkedId }, u);
        await AddVersionAsync(u, id, file, comment);
        return id;
    }

    public async Task<long> AddVersionAsync(CurrentUser u, long evidenceId, IFormFile file, string? comment)
    {
        await es.Require(u, "evidence.edit");
        var ev = await es.GetRawAsync(Schema.Get("evidence"), evidenceId);
        var orgId = Db.Long(ev["OrganizationId"]);
        var (rel, sha, size) = await StoreAsync(file, Path.Combine("org" + orgId, "evidence", evidenceId.ToString()));
        var ver = Db.Int(ev["CurrentVersion"]) + 1;
        var vid = await db.InsertAsync("EvidenceVersions", new Dictionary<string, object?>
        {
            ["OrganizationId"] = orgId, ["EvidenceId"] = evidenceId, ["Version"] = ver, ["FileName"] = SafeName(file.FileName), ["ContentType"] = file.ContentType, ["SizeBytes"] = size,
            ["Sha256"] = sha, ["StoragePath"] = rel, ["Comment"] = comment, ["CreatedAt"] = DateTime.UtcNow, ["CreatedBy"] = u.Email
        });
        await db.UpdateAsync("Evidence", evidenceId, new Dictionary<string, object?> { ["CurrentVersion"] = ver, ["UpdatedAt"] = DateTime.UtcNow, ["UpdatedBy"] = u.Email });
        await audit.LogAsync("UPLOAD", "evidenceVersions", vid, orgId, new { evidenceId, version = ver, file = file.FileName, sha256 = sha }, u);
        return vid;
    }

    public async Task<List<object>> VerifyEvidenceAsync(CurrentUser u, long evidenceId)
    {
        await es.Require(u, "evidence.view");
        await es.GetRawAsync(Schema.Get("evidence"), evidenceId);
        var vers = await db.QueryAsync($"SELECT * FROM {db.Q("EvidenceVersions")} WHERE {db.Q("EvidenceId")} = ? ORDER BY {db.Q("Version")}", evidenceId);
        var res = new List<object>();
        foreach (var v in vers)
        {
            var abs = Abs(Db.Str(v["StoragePath"])!);
            var actual = File.Exists(abs) ? await Hashing.Sha256FileAsync(abs) : null;
            res.Add(new { version = Db.Int(v["Version"]), fileName = Db.Str(v["FileName"]), recorded = Db.Str(v["Sha256"]), actual, intact = actual == Db.Str(v["Sha256"]) });
        }
        await audit.LogAsync("VERIFY", "evidence", evidenceId, null, new { versions = res.Count }, u);
        return res;
    }
}
