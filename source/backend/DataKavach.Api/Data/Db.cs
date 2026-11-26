using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Globalization;

namespace DataKavach.Data;

public enum Dialect { SqlServer, Sqlite }

public sealed class DbOptions
{
    public string Provider { get; set; } = "SqlServer";
    /// <summary>ODBC connection string, e.g. "Driver={ODBC Driver 18 for SQL Server};Server=localhost\\SQLEXPRESS;Database=DataKavach;Trusted_Connection=Yes;Encrypt=Yes;TrustServerCertificate=Yes;"</summary>
    public string ConnectionString { get; set; } = "";
}

public sealed class Db
{
    public Dialect Dialect { get; }
    readonly string _cs;

    public Db(DbOptions o)
    {
        Dialect = o.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ? Dialect.Sqlite : Dialect.SqlServer;
        _cs = o.ConnectionString;
    }

    public async Task<OdbcConnection> OpenAsync()
    {
        var c = new OdbcConnection(_cs);
        await c.OpenAsync();
        if (Dialect == Dialect.Sqlite)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
        }
        return c;
    }

    public string Q(string ident) => Dialect == Dialect.SqlServer ? $"[{ident}]" : $"\"{ident}\"";

    public string Page(string sqlWithOrderBy, int offset, int limit) => Dialect == Dialect.SqlServer
        ? $"{sqlWithOrderBy} OFFSET {Math.Max(0, offset)} ROWS FETCH NEXT {Math.Clamp(limit, 1, 5000)} ROWS ONLY"
        : $"{sqlWithOrderBy} LIMIT {Math.Clamp(limit, 1, 5000)} OFFSET {Math.Max(0, offset)}";

    public string Like(string col) => Dialect == Dialect.SqlServer ? $"{col} LIKE ?" : $"{col} LIKE ? COLLATE NOCASE";

    void Bind(OdbcCommand cmd, object?[] args)
    {
        foreach (var a in args)
        {
            var p = cmd.CreateParameter();
            switch (a)
            {
                case null: p.Value = DBNull.Value; p.OdbcType = OdbcType.NVarChar; break;
                case DBNull: p.Value = DBNull.Value; p.OdbcType = OdbcType.NVarChar; break;
                case string s:
                    if (s.Length > 4000) { p.OdbcType = OdbcType.NText; p.Size = s.Length; }
                    else { p.OdbcType = OdbcType.NVarChar; p.Size = Math.Max(1, s.Length); }
                    p.Value = s; break;
                case bool b: p.OdbcType = OdbcType.Bit; p.Value = b; break;
                case int i: p.OdbcType = OdbcType.Int; p.Value = i; break;
                case long l: p.OdbcType = OdbcType.BigInt; p.Value = l; break;
                case decimal d: p.OdbcType = OdbcType.Decimal; p.Precision = 18; p.Scale = 2; p.Value = d; break;
                case double db: p.OdbcType = OdbcType.Double; p.Value = db; break;
                case DateOnly dO when Dialect == Dialect.SqlServer: p.OdbcType = OdbcType.NVarChar; p.Size = 10; p.Value = dO.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); break;
                case DateOnly dO: p.OdbcType = OdbcType.Date; p.Value = dO.ToDateTime(TimeOnly.MinValue); break;
                // msodbcsql rejects DateTime parameters bound by System.Data.Odbc ("Datetime field overflow") - send ISO 8601 text, SQL Server converts it to DATETIME2 exactly
                case DateTime dts when Dialect == Dialect.SqlServer: p.OdbcType = OdbcType.NVarChar; p.Size = 23; p.Value = dts.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture); break;
                case DateTime dt: p.OdbcType = OdbcType.DateTime; p.Value = DateTime.SpecifyKind(dt.AddTicks(-(dt.Ticks % TimeSpan.TicksPerMillisecond)), DateTimeKind.Unspecified); break;   // ms precision avoids "Datetime field overflow" with msodbcsql
                default: p.OdbcType = OdbcType.NVarChar; p.Value = Convert.ToString(a, CultureInfo.InvariantCulture); break;
            }
            cmd.Parameters.Add(p);
        }
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params object?[] args)
    {
        await using var c = await OpenAsync();
        return await QueryAsync(c, null, sql, args);
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsync(OdbcConnection c, OdbcTransaction? tx, string sql, params object?[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        Bind(cmd, args);
        var list = new List<Dictionary<string, object?>>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(row);
        }
        return list;
    }

    public async Task<Dictionary<string, object?>?> FirstAsync(string sql, params object?[] args) => (await QueryAsync(sql, args)).FirstOrDefault();

    public async Task<int> ExecAsync(string sql, params object?[] args)
    {
        await using var c = await OpenAsync();
        return await ExecAsync(c, null, sql, args);
    }

    public async Task<int> ExecAsync(OdbcConnection c, OdbcTransaction? tx, string sql, params object?[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        Bind(cmd, args);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql, params object?[] args)
    {
        await using var c = await OpenAsync();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, args);
        var v = await cmd.ExecuteScalarAsync();
        return v is DBNull ? null : v;
    }

    public async Task<long> CountAsync(string sql, params object?[] args) => Convert.ToInt64(await ScalarAsync(sql, args) ?? 0L);

    /// <summary>Insert a row and return its identity.</summary>
    public async Task<long> InsertAsync(string table, IDictionary<string, object?> values, OdbcConnection? conn = null, OdbcTransaction? tx = null)
    {
        var cols = values.Keys.ToList();
        var colSql = string.Join(", ", cols.Select(Q));
        var ph = string.Join(", ", cols.Select(_ => "?"));
        string sql = Dialect == Dialect.SqlServer
            ? $"INSERT INTO {Q(table)} ({colSql}) OUTPUT INSERTED.{Q("Id")} VALUES ({ph})"
            : $"INSERT INTO {Q(table)} ({colSql}) VALUES ({ph})";
        var own = conn == null;
        var c = conn ?? await OpenAsync();
        try
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            Bind(cmd, values.Values.ToArray());
            if (Dialect == Dialect.SqlServer) return Convert.ToInt64(await cmd.ExecuteScalarAsync());
            await cmd.ExecuteNonQueryAsync();
            using var idCmd = c.CreateCommand();
            idCmd.Transaction = tx;
            idCmd.CommandText = "SELECT last_insert_rowid()";
            return Convert.ToInt64(await idCmd.ExecuteScalarAsync());
        }
        finally { if (own) await c.DisposeAsync(); }
    }

    public async Task<int> UpdateAsync(string table, long id, IDictionary<string, object?> values, OdbcConnection? conn = null, OdbcTransaction? tx = null)
    {
        if (values.Count == 0) return 0;
        var set = string.Join(", ", values.Keys.Select(k => $"{Q(k)} = ?"));
        var args = values.Values.Append(id).ToArray();
        var sql = $"UPDATE {Q(table)} SET {set} WHERE {Q("Id")} = ?";
        if (conn != null) return await ExecAsync(conn, tx, sql, args);
        return await ExecAsync(sql, args);
    }

    // ---- value helpers -------------------------------------------------
    public static string? Str(object? v) => v switch { null => null, DateTime d => d.ToString("o", CultureInfo.InvariantCulture), _ => Convert.ToString(v, CultureInfo.InvariantCulture) };
    public static long Long(object? v) => v == null ? 0 : Convert.ToInt64(v, CultureInfo.InvariantCulture);
    public static int Int(object? v) => v == null ? 0 : Convert.ToInt32(v, CultureInfo.InvariantCulture);
    public static bool Bool(object? v) => v switch { null => false, bool b => b, string s => s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase), _ => Convert.ToInt64(v, CultureInfo.InvariantCulture) != 0 };
    public static DateTime? Dt(object? v)
    {
        if (v == null) return null;
        if (v is DateTime d) return d;
        if (v is DateOnly dO) return dO.ToDateTime(TimeOnly.MinValue);
        var s = Convert.ToString(v, CultureInfo.InvariantCulture);
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var r) ? r : null;
    }
}
