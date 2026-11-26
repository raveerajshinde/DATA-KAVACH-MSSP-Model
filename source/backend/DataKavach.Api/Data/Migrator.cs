using System.Reflection;
using System.Text;

namespace DataKavach.Data;

/// <summary>
/// Versioned SQL migrations. Scripts live in Migrations/{SqlServer|Sqlite}/NNNN_name.sql (embedded).
/// Applied scripts are recorded in SchemaVersions. Each script runs in a transaction.
/// Statements are separated by a line containing only "-- go".
/// </summary>
public sealed class Migrator(Db db, ILogger<Migrator> log)
{
    public async Task<List<string>> MigrateAsync()
    {
        var applied = new List<string>();
        await EnsureVersionTable();
        var done = (await db.QueryAsync($"SELECT {db.Q("Version")} FROM {db.Q("SchemaVersions")}")).Select(r => Db.Int(r["Version"])).ToHashSet();
        foreach (var (ver, name, sql) in Scripts(db.Dialect))
        {
            if (done.Contains(ver)) continue;
            log.LogInformation("Applying migration {Version} {Name}", ver, name);
            await using var c = await db.OpenAsync();
            using var tx = c.BeginTransaction();
            try
            {
                foreach (var stmt in Split(sql)) await db.ExecAsync(c, tx, stmt);
                await db.ExecAsync(c, tx, $"INSERT INTO {db.Q("SchemaVersions")} ({db.Q("Version")}, {db.Q("Name")}, {db.Q("AppliedAt")}) VALUES (?, ?, ?)", ver, name, DateTime.UtcNow);
                tx.Commit();
                applied.Add($"{ver:D4}_{name}");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                throw new InvalidOperationException($"Migration {ver} {name} failed: {ex.Message}", ex);
            }
        }
        return applied;
    }

    public async Task<int> CurrentVersionAsync()
    {
        await EnsureVersionTable();
        return Db.Int(await db.ScalarAsync($"SELECT MAX({db.Q("Version")}) FROM {db.Q("SchemaVersions")}"));
    }

    async Task EnsureVersionTable()
    {
        var sql = db.Dialect == Dialect.SqlServer
            ? "IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL CREATE TABLE dbo.SchemaVersions ([Version] INT NOT NULL PRIMARY KEY, [Name] NVARCHAR(200) NOT NULL, [AppliedAt] DATETIME2 NOT NULL)"
            : "CREATE TABLE IF NOT EXISTS \"SchemaVersions\" (\"Version\" INTEGER NOT NULL PRIMARY KEY, \"Name\" TEXT NOT NULL, \"AppliedAt\" TEXT NOT NULL)";
        await db.ExecAsync(sql);
    }

    public static IEnumerable<string> Split(string sql) =>
        sql.Replace("\r\n", "\n").Split("\n-- go\n")
            .Select(s => string.Join("\n", s.Split('\n').Where(l => !l.TrimStart().StartsWith("--"))).Trim())   // drop full-line comments (some ODBC drivers mis-parse them)
            .Where(s => s.Length > 0);

    static IEnumerable<(int ver, string name, string sql)> Scripts(Dialect d)
    {
        var asm = Assembly.GetExecutingAssembly();
        var prefix = $"DataKavach.Migrations.{d}.";
        foreach (var res in asm.GetManifestResourceNames().Where(n => n.StartsWith(prefix) && n.EndsWith(".sql")).OrderBy(n => n))
        {
            var file = res[prefix.Length..^4];               // e.g. _0001_initial (resource names prefix digits with _)
            var clean = file.TrimStart('_');
            var us = clean.IndexOf('_');
            var ver = int.Parse(clean[..us]);
            using var s = asm.GetManifestResourceStream(res)!;
            using var r = new StreamReader(s, Encoding.UTF8);
            yield return (ver, clean[(us + 1)..], r.ReadToEnd());
        }
    }
}

/// <summary>Generates CREATE TABLE scripts from the schema definitions (used to author migration files).</summary>
public static class DdlGenerator
{
    public static readonly List<EntityDef> SystemTables = new()
    {
        new EntityDef { Key = "_users", Table = "Users", Label = "Users", Module = "admin", OrgScoped = false, Cols = new()
        {
            new("Email", ColType.Str, 200), new("FullName", ColType.Str, 200), new("PasswordHash", ColType.Str, 500), new("Role", ColType.Str, 50),
            new("Department", ColType.Str, 100), new("Phone", ColType.Str, 50), new("IsActive", ColType.Bool), new("MfaEnabled", ColType.Bool),
            new("MfaSecret", ColType.Str, 1000), new("FailedAttempts", ColType.Int), new("LockoutUntil", ColType.DateTime), new("LastLoginAt", ColType.DateTime),
            new("MustChangePassword", ColType.Bool), new("PasswordChangedAt", ColType.DateTime)
        }, ExtraIndexes = new[] { "!Email" } },
        new EntityDef { Key = "_roleperms", Table = "RolePermissions", Label = "Role permissions", Module = "admin", OrgScoped = false, SoftDelete = false, Cols = new()
        { new("Role", ColType.Str, 50), new("Permission", ColType.Str, 100) }, ExtraIndexes = new[] { "!Role,Permission" } },
        new EntityDef { Key = "_audit", Table = "AuditLogs", Label = "Audit log", Module = "admin", OrgScoped = false, SoftDelete = false, Cols = new()
        {
            new("At", ColType.DateTime), new("UserId", ColType.Long), new("UserEmail", ColType.Str, 200), new("OrganizationId", ColType.Long),
            new("Action", ColType.Str, 50), new("Entity", ColType.Str, 60), new("EntityId", ColType.Long), new("Details", ColType.Text),
            new("Ip", ColType.Str, 64), new("PrevHash", ColType.Str, 64), new("Hash", ColType.Str, 64)
        }, ExtraIndexes = new[] { "At", "Entity,EntityId", "OrganizationId" } },
        new EntityDef { Key = "_settings", Table = "Settings", Label = "Settings", Module = "admin", OrgScoped = false, SoftDelete = false, Cols = new()
        { new("OrganizationId", ColType.Long), new("SettingKey", ColType.Str, 100), new("SettingValue", ColType.Text) }, ExtraIndexes = new[] { "!OrganizationId,SettingKey" } },
    };

    static string SqlType(Col c, Dialect d) => d == Dialect.SqlServer
        ? c.Type switch
        {
            ColType.Str => $"NVARCHAR({(c.Len <= 0 ? 200 : c.Len)})",
            ColType.Text => "NVARCHAR(MAX)",
            ColType.Int => "INT",
            ColType.Long => "BIGINT",
            ColType.Dec => "DECIMAL(18,2)",
            ColType.Bool => "BIT",
            ColType.Date => "DATE",
            ColType.DateTime => "DATETIME2(3)",
            _ => "NVARCHAR(200)"
        }
        : c.Type switch
        {
            ColType.Int or ColType.Long or ColType.Bool => "INTEGER",
            ColType.Dec => "NUMERIC",
            ColType.Date => "DATE",
            ColType.DateTime => "TIMESTAMP",
            _ => "TEXT"
        };

    public static List<Col> StandardCols(EntityDef e)
    {
        var list = new List<Col>();
        if (e.OrgScoped) list.Add(new Col("OrganizationId", ColType.Long) { Required = true });
        list.AddRange(e.Cols);
        list.Add(new Col("CreatedAt", ColType.DateTime) { Required = true });
        list.Add(new Col("CreatedBy", ColType.Str, 200));
        list.Add(new Col("UpdatedAt", ColType.DateTime));
        list.Add(new Col("UpdatedBy", ColType.Str, 200));
        if (e.SoftDelete) list.Add(new Col("IsDeleted", ColType.Bool) { Required = true });
        return list;
    }

    public static string CreateAll(Dialect d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-- DataKavach initial schema ({d}). Generated from Data/Schema.cs.");
        foreach (var e in SystemTables.Concat(Schema.All)) sb.Append(CreateTable(e, d));
        return sb.ToString();
    }

    static string Q(string s, Dialect d) => d == Dialect.SqlServer ? $"[{s}]" : $"\"{s}\"";

    public static string CreateTable(EntityDef e, Dialect d)
    {
        var sb = new StringBuilder();
        var cols = StandardCols(e);
        sb.AppendLine($"CREATE TABLE {Q(e.Table, d)} (");
        var lines = new List<string>
        {
            d == Dialect.SqlServer ? $"  {Q("Id", d)} BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT {Q("PK_" + e.Table, d)} PRIMARY KEY" : $"  {Q("Id", d)} INTEGER PRIMARY KEY AUTOINCREMENT"
        };
        foreach (var c in cols)
        {
            var nn = c.Name is "OrganizationId" && e.OrgScoped || c.Name is "CreatedAt" ? " NOT NULL" : "";
            var def = c.Name == "IsDeleted" ? (d == Dialect.SqlServer ? " NOT NULL CONSTRAINT " + Q($"DF_{e.Table}_IsDeleted", d) + " DEFAULT 0" : " NOT NULL DEFAULT 0") : "";
            lines.Add($"  {Q(c.Name, d)} {SqlType(c, d)}{(def.Length > 0 ? def : nn)}");
        }
        if (e.OrgScoped) lines.Add($"  CONSTRAINT {Q($"FK_{e.Table}_Org", d)} FOREIGN KEY ({Q("OrganizationId", d)}) REFERENCES {Q("Organizations", d)} ({Q("Id", d)})");
        if (e.ParentKey != null && e.ParentCol != null)
        {
            var parent = Schema.Get(e.ParentKey);
            lines.Add($"  CONSTRAINT {Q($"FK_{e.Table}_{parent.Table}", d)} FOREIGN KEY ({Q(e.ParentCol, d)}) REFERENCES {Q(parent.Table, d)} ({Q("Id", d)})");
        }
        sb.AppendLine(string.Join(",\n", lines));
        sb.AppendLine(")");
        sb.AppendLine("-- go");
        var idx = new List<string>();
        if (e.OrgScoped) idx.Add("OrganizationId");
        if (e.ParentCol != null) idx.Add(e.ParentCol);
        idx.AddRange(e.ExtraIndexes);
        foreach (var raw in idx.Distinct())
        {
            var unique = raw.StartsWith('!');
            var colsI = raw.TrimStart('!').Split(',');
            var name = $"{(unique ? "UX" : "IX")}_{e.Table}_{string.Join("_", colsI)}";
            sb.AppendLine($"CREATE {(unique ? "UNIQUE " : "")}INDEX {Q(name, d)} ON {Q(e.Table, d)} ({string.Join(", ", colsI.Select(c => Q(c, d)))})");
            sb.AppendLine("-- go");
        }
        return sb.ToString();
    }
}
