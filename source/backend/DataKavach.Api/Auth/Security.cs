using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using DataKavach.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DataKavach.Auth;

public sealed class JwtOptions
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "DataKavach";
    public int SessionMinutes { get; set; } = 60;
}

public static class Roles
{
    public const string SuperAdmin = "Super Administrator";
    public static readonly string[] All =
    {
        SuperAdmin, "Organization Administrator", "Compliance Manager", "DPO / Privacy Officer", "IT Security Manager",
        "HR Manager", "Department Manager", "Auditor", "Consultant", "Read-only User",
        CustomerAdmin, CustomerUser, CustomerViewer
    };
    public const string CustomerAdmin = "Customer Administrator";
    public const string CustomerUser = "Customer User";
    public const string CustomerViewer = "Customer Viewer";
    public static readonly string[] CustomerRoles = { CustomerAdmin, CustomerUser, CustomerViewer };
    public static bool IsCustomerRole(string role) => CustomerRoles.Contains(role);

    public static readonly string[] Modules = { "org", "assessment", "evidence", "inventory", "dpia", "consent", "policy", "vendor", "security", "rights", "incident", "roadmap", "report", "template" };
    public static readonly string[] Actions = { "view", "edit", "delete", "approve", "export" };
    public static readonly string[] Special = { "pii.view", "admin.users", "admin.settings", "admin.audit", "admin.backup", "admin.apikeys", "portfolio.view" };

    public static IEnumerable<string> AllPermissions() => Modules.SelectMany(m => Actions.Select(a => $"{m}.{a}")).Concat(Special);

    public static IEnumerable<string> Defaults(string role)
    {
        IEnumerable<string> M(string[] mods, params string[] acts) => mods.SelectMany(m => acts.Select(a => $"{m}.{a}"));
        var all = Modules;
        return role switch
        {
            SuperAdmin => AllPermissions(),
            "Organization Administrator" => AllPermissions().Where(p => p != "admin.backup"),
            "Compliance Manager" => M(all, "view", "edit", "approve", "export").Append("pii.view").Append("admin.audit"),
            "DPO / Privacy Officer" => M(all, "view", "edit", "approve", "export").Append("pii.view").Append("admin.audit"),
            "IT Security Manager" => M(all, "view").Concat(M(new[] { "security", "incident", "evidence", "roadmap", "inventory", "vendor" }, "edit", "export")).Append("incident.approve"),
            "HR Manager" => M(all, "view").Concat(M(new[] { "inventory", "rights", "evidence", "roadmap", "consent" }, "edit")).Append("pii.view"),
            "Department Manager" => M(all, "view").Concat(M(new[] { "assessment", "evidence", "roadmap", "inventory" }, "edit")),
            "Auditor" => M(all, "view", "export").Append("admin.audit"),
            "Consultant" => M(all, "view", "edit", "export"),
            CustomerAdmin => M(all.Where(m => m is not ("template" or "org")).ToArray(), "view", "edit", "delete", "approve", "export")
                .Concat(new[] { "org.view", "org.edit", "template.view", "pii.view", "admin.users", "admin.audit", "admin.apikeys" }),
            CustomerUser => M(all, "view").Concat(M(new[] { "assessment", "evidence", "inventory", "consent", "rights", "incident", "roadmap", "vendor", "dpia", "security" }, "edit")).Append("report.export").Append("report.edit"),
            CustomerViewer => M(all, "view").Append("report.export"),
            _ => M(all, "view")
        };
    }
}

public sealed record CurrentUser(long Id, string Email, string Name, string Role, string UserType = "Provider", long OrgId = 0)
{
    /// <summary>Customer portal users are confined to their own organisation.</summary>
    public bool IsCustomer => UserType == "Customer";
    public static CurrentUser? From(ClaimsPrincipal? p)
    {
        if (p?.Identity?.IsAuthenticated != true) return null;
        return new CurrentUser(long.Parse(p.FindFirstValue("uid") ?? "0"), p.FindFirstValue("email") ?? "", p.FindFirstValue("name") ?? "", p.FindFirstValue("role") ?? "",
            p.FindFirstValue("utype") ?? "Provider", long.TryParse(p.FindFirstValue("org"), out var o) ? o : 0);
    }
}

/// <summary>Caches role permissions from the RolePermissions table.</summary>
public sealed class PermissionService(Db db)
{
    Dictionary<string, HashSet<string>>? _cache;
    readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<HashSet<string>> ForRoleAsync(string role)
    {
        if (_cache == null)
        {
            await _lock.WaitAsync();
            try
            {
                if (_cache == null)
                {
                    var rows = await db.QueryAsync($"SELECT {db.Q("Role")}, {db.Q("Permission")} FROM {db.Q("RolePermissions")}");
                    _cache = rows.GroupBy(r => Db.Str(r["Role"])!).ToDictionary(g => g.Key, g => g.Select(r => Db.Str(r["Permission"])!).ToHashSet(), StringComparer.OrdinalIgnoreCase);
                }
            }
            finally { _lock.Release(); }
        }
        if (role == Roles.SuperAdmin) return Roles.AllPermissions().ToHashSet();
        return _cache.TryGetValue(role, out var s) ? s : new HashSet<string>();
    }

    public void Invalidate() => _cache = null;

    public async Task<bool> HasAsync(CurrentUser? u, string perm) => u != null && (await ForRoleAsync(u.Role)).Contains(perm);
}

public sealed class TokenService(IOptions<JwtOptions> opt)
{
    readonly JwtOptions _o = opt.Value;
    public SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(_o.SigningKey));

    public (string token, DateTime expires) Issue(long id, string email, string name, string role, string userType = "Provider", long orgId = 0, string purpose = "session", int? minutes = null)
    {
        var exp = DateTime.UtcNow.AddMinutes(minutes ?? _o.SessionMinutes);
        var h = new JsonWebTokenHandler();
        var token = h.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _o.Issuer,
            Audience = _o.Issuer,
            Expires = exp,
            Claims = new Dictionary<string, object> { ["uid"] = id.ToString(), ["email"] = email, ["name"] = name, ["role"] = role, ["utype"] = userType, ["org"] = orgId.ToString(), ["purpose"] = purpose },
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
        });
        return (token, exp);
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string token, string purpose = "session")
    {
        var h = new JsonWebTokenHandler();
        var res = await h.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _o.Issuer,
            ValidAudience = _o.Issuer,
            IssuerSigningKey = Key,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        });
        if (!res.IsValid) return null;
        if (res.ClaimsIdentity.FindFirst("purpose")?.Value != purpose) return null;
        return new ClaimsPrincipal(res.ClaimsIdentity);
    }
}

public sealed class JwtAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e, TokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
{
    public const string SchemeName = "DkJwt";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = null;
        var auth = Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) token = auth[7..].Trim();
        // Downloads opened in a new tab pass ?access_token= (short-lived token)
        if (token == null && Request.Query.TryGetValue("access_token", out var qt) && Request.Path.StartsWithSegments("/api/files")) token = qt.ToString();
        if (string.IsNullOrEmpty(token)) return AuthenticateResult.NoResult();
        var p = await tokens.ValidateAsync(token);
        if (p == null) return AuthenticateResult.Fail("Invalid or expired token");
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(p.Claims, SchemeName, "name", "role")), SchemeName));
    }
}

public static class Passwords
{
    static readonly PasswordHasher<object> H = new();
    public static string Hash(string pw) => H.HashPassword(new object(), pw);
    public static bool Verify(string hash, string pw) => H.VerifyHashedPassword(new object(), hash, pw) != PasswordVerificationResult.Failed;

    public static string? Validate(string pw)
    {
        if (pw.Length < 10) return "Password must be at least 10 characters.";
        if (!pw.Any(char.IsUpper) || !pw.Any(char.IsLower) || !pw.Any(char.IsDigit)) return "Password must contain upper-case, lower-case and a digit.";
        return null;
    }
}

/// <summary>RFC 6238 TOTP (SHA-1, 6 digits, 30 s) compatible with Microsoft/Google Authenticator.</summary>
public static class Totp
{
    const string B32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string NewSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append(B32[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(B32[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    static byte[] Decode(string s)
    {
        s = s.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var ch in s)
        {
            var v = B32.IndexOf(ch);
            if (v < 0) continue;
            buffer = (buffer << 5) | v; bits += 5;
            if (bits >= 8) { output.Add((byte)((buffer >> (bits - 8)) & 0xFF)); bits -= 8; }
        }
        return output.ToArray();
    }

    public static string Code(string secret, long step)
    {
        var key = Decode(secret);
        var msg = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(msg);
        using var h = new HMACSHA1(key);
        var hash = h.ComputeHash(msg);
        int off = hash[^1] & 0xF;
        int bin = ((hash[off] & 0x7F) << 24) | (hash[off + 1] << 16) | (hash[off + 2] << 8) | hash[off + 3];
        return (bin % 1_000_000).ToString("D6");
    }

    public static bool Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim().Replace(" ", "");
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        for (long i = -1; i <= 1; i++)
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code(secret, step + i)), Encoding.ASCII.GetBytes(code))) return true;
        return false;
    }

    public static string Uri(string secret, string email, string issuer = "Data Kavach") =>
        $"otpauth://totp/{System.Uri.EscapeDataString(issuer)}:{System.Uri.EscapeDataString(email)}?secret={secret}&issuer={System.Uri.EscapeDataString(issuer)}&digits=6&period=30";
}

/// <summary>Field-level encryption for secrets at rest (MFA secrets etc.) using ASP.NET Core Data Protection.</summary>
public sealed class FieldCrypto(IDataProtectionProvider dp)
{
    readonly IDataProtector _p = dp.CreateProtector("DataKavach.FieldEncryption.v1");
    public string Protect(string s) => "enc:" + _p.Protect(s);
    public string Unprotect(string s) => s.StartsWith("enc:") ? _p.Unprotect(s[4..]) : s;
}

public static class Hashing
{
    public static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    public static string Sha256Hex(string s) => Sha256Hex(Encoding.UTF8.GetBytes(s));
    public static async Task<string> Sha256FileAsync(string path)
    {
        await using var f = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(f)).ToLowerInvariant();
    }
}
