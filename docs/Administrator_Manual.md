# Data Kavach - Administrator Manual

Version 1.0.0 - DPDP Compliance Management & Report Generator

## 0. Service-provider (MSSP) model

Data Kavach runs as a managed service: **your company operates one installation for all clients**.

| | Provider staff (your company) | Customer portal users |
|---|---|---|
| Roles | Super Administrator, Organization Administrator, Compliance Manager, DPO / Privacy Officer, IT Security Manager, HR Manager, Department Manager, Auditor, Consultant, Read-only User | Customer Administrator, Customer User, Customer Viewer |
| Home page | **Client Portfolio** - every client's health, indicator, high-risk gaps, overdue actions, incidents, overdue breach notices, overdue rights requests, processors without DPA, portal users, last activity | The client's own dashboard |
| Organisations | Switch between all clients; onboard new clients | Fixed to one organisation; cannot see or reach any other client |
| Shared configuration | Requirement library, report templates, provider branding, role permissions, backups | Read-only / not visible |
| User management | All staff and portal users | Customer Administrators manage portal users of their own organisation only (customer roles only) |
| Audit trail | Everything | Their organisation's entries only |

Isolation is enforced by the server on every request, not only hidden in the screens: a customer token carries its organisation, and any list, record, file download, report, dashboard or setting outside it returns HTTP 403. The automated test suite includes these checks.

**Onboarding a client:** Client Portfolio -> *Onboard client* creates the organisation (with default departments and the 19 Rule 6 controls) and, optionally, its first Customer Administrator with a temporary password (changed at first sign-in). More portal users: Users & Roles -> *Add customer portal user*.

**Your company branding:** Settings & Security -> *Your company branding* (Super/Organization Administrator). Company name, tagline, portal name, logo, primary and accent colours, support e-mail/phone, website, address and login message. It is applied to the login page, the application shell and theme for everyone, and - when "Show on client reports" is on - as *Prepared by* and the consultant logo on every client report unless that client's profile sets its own.

## 1. Architecture

| Layer | Technology |
|---|---|
| Frontend | React 19 + TypeScript + Material UI 7 (served by the backend from `app\wwwroot`) |
| Backend | ASP.NET Core 10 Web API (minimal APIs), JWT authentication, role/permission authorisation |
| Database | Microsoft SQL Server 2019/2022 (Express or higher) via Microsoft ODBC Driver 17/18 |
| Data access | ADO.NET (`System.Data.Odbc`) with versioned SQL migrations (`Migrations\SqlServer\*.sql`) |
| Word export | WordprocessingML (.docx) generated as a standard OPC package - text, tables and charts stay editable; validated against the ECMA-376 schemas |
| PDF export | The same DOCX converted by LibreOffice (headless, UNO). The table of contents is regenerated during conversion so PDF page numbers are exact, and the page numbers are written back into the DOCX TOC |
| Hosting | IIS (default, in-process ASP.NET Core Module) or a Windows service (Kestrel) |
| Storage | Evidence, logos and exported reports on disk under `C:\DataKavach\data\storage`, SHA-256 fingerprinted |

Folder layout after installation:

```
C:\DataKavach\
  app\                  application binaries, wwwroot, appsettings.json, appsettings.Local.json (secrets, ACL-restricted)
  data\storage\         evidence, logos, report exports, data-protection keys
  backups\              database (.bak) and file backups, pre-upgrade snapshots
  logs\                 installer / upgrade / backup transcripts
  install-state.json    installation parameters used by upgrade/repair/backup/restore/uninstall
```

## 2. System requirements

* Windows Server 2019/2022/2025 or Windows 10/11 (64-bit), 4 GB RAM minimum (8 GB recommended), 5 GB disk plus evidence volume.
* SQL Server 2019 or later (Express is sufficient for most organisations; the 10 GB Express limit is ample for compliance records - evidence files are stored on disk, not in the database).
* The installer installs, with your approval: IIS, ASP.NET Core 10 Hosting Bundle, Microsoft ODBC Driver 18 for SQL Server, SQL Server 2022 Express (optional) and LibreOffice (for PDF). It uses `winget`; on servers without `winget`, place the installers in a `prereqs` folder next to `install.ps1`:

| File pattern in `prereqs\` | Component |
|---|---|
| `dotnet-hosting-10*.exe` | ASP.NET Core 10 Hosting Bundle |
| `msodbcsql*.msi` | ODBC Driver 18 for SQL Server (x64) |
| `SQL2022*Express*.exe` | SQL Server 2022 Express |
| `LibreOffice*.msi` | LibreOffice (x64) |

## 3. Installation

1. Extract the package to a local folder (for example `C:\Install\DataKavach_1.0.0`).
2. Double-click **EnterpriseDPDP_Setup.exe** (or right-click **INSTALL.bat** -> Run as administrator).
3. Answer the prompts. Defaults: install to `C:\DataKavach`, IIS hosting, HTTP 8080 + HTTPS 8443, SQL Server `localhost\SQLEXPRESS`, database `DataKavach`, Windows authentication.
4. When finished, the portal opens. If you did not create an administrator during installation, the first visitor is asked to create the Super Administrator.

The installer performs, in order: administrator check, software detection, dependency installation, SQL Server connection, database creation, backend deployment, frontend deployment, storage configuration, authentication configuration (random JWT signing key), IIS site / Windows service, firewall and HTTPS, migrations, health checks, administrator creation and opening the portal. It is safe to re-run; existing data and settings are preserved and a safety database backup is taken first.

### Common options

```powershell
# Windows service instead of IIS
.\install.ps1 -Mode Service -HttpPort 8080 -HttpsPort 8443

# Remote SQL Server with a SQL login
.\install.ps1 -SqlInstance "DBSRV01\PROD" -SqlUser dk_app -SqlPassword (Read-Host -AsSecureString)

# Use a CA-issued certificate already in LocalMachine\My
.\install.ps1 -CertificateThumbprint 3A1F...C9

# Fully unattended
.\install.ps1 -Unattended -InstallSqlExpress -AdminEmail admin@corp.in -AdminPassword (ConvertTo-SecureString 'Temp#Pass2026' -AsPlainText -Force)
```

### Database access

* **Windows authentication (default)**: the installer creates a SQL login for the application identity (`IIS APPPOOL\DataKavach` or `NT SERVICE\DataKavach`) and adds it to `db_owner` and `db_backupoperator` of the application database only.
* **Remote SQL Server**: use a SQL login (`-SqlUser`). The login needs `dbcreator` during installation (or create the empty database beforehand) and `db_owner` on the database afterwards.
* The connection string is stored in `app\appsettings.Local.json`, whose ACL allows only Administrators, SYSTEM and (read) the application identity.

## 4. Upgrade, repair, backup, restore, uninstall

| Task | Command | Notes |
|---|---|---|
| Upgrade | `.\upgrade.ps1` (run from the new package) | Zips the current app folder and backs up the database, deploys new files keeping `appsettings.Local.json` and `web.config`, applies migrations, health-checks, and rolls back automatically on failure |
| Roll back last upgrade | `.\upgrade.ps1 -Rollback` | Restores app files and database from the last pre-upgrade snapshot |
| Repair | `.\repair.ps1` | Re-checks prerequisites, permissions, SQL access, hosting, migrations and health |
| Enable PDF later | `.\repair.ps1 -InstallLibreOffice` | |
| Locked-out admin | `.\repair.ps1 -ResetAdminEmail admin@corp.in` | Sets a temporary password; change required at next login |
| Backup now | `.\backup.ps1` | Database `.bak` (CHECKSUM) + documents ZIP + settings + manifest with SHA-256 |
| Scheduled backup | `.\backup.ps1 -Schedule -At 01:30 -RetentionDays 30` | Windows scheduled task as SYSTEM |
| Restore | `.\restore.ps1 -BackupSet C:\DataKavach\backups\set_YYYYMMDD_HHMMSS` | Verifies checksums, takes a safety backup, restores DB and files, re-applies permissions, migrates, health-checks |
| Uninstall | `.\uninstall.ps1` | Final backup, removes site/service/firewall rules; keeps data, backups and database |
| Full removal | `.\uninstall.ps1 -RemoveData -DropDatabase` | Asks for confirmation |

Backup sets contain personal data and the JWT signing key - store them encrypted and access-controlled. Data-protection keys (used to encrypt MFA secrets) are protected with machine-level DPAPI; restoring onto a different server requires users to re-enrol MFA (an administrator can reset MFA per user).

In-application backup (Settings & Security -> Back up now) writes a database backup and a ZIP of the storage folder to the backup directory. The SQL Server service account must be able to write there; the installer grants this for local instances.

## 5. Security controls

* Passwords: PBKDF2 (ASP.NET Core Identity hasher), minimum 10 characters with upper, lower case and digit; 5 failed attempts lock the account for 15 minutes; login rate-limited per IP.
* MFA: TOTP (RFC 6238) compatible with Microsoft/Google Authenticator; secrets encrypted at rest with ASP.NET Core Data Protection (DPAPI machine key on Windows).
* Sessions: signed JWT (HS256), default 60 minutes, refreshed while the user is active; idle sessions end after 30 minutes in the browser.
* Authorisation: every API call checks a permission (`module.view|edit|delete|approve|export` plus `pii.view`, `admin.*`). Role permissions are editable under Users & Roles.
* Personal data masking: data principal names, contacts and identifiers are masked for roles without `pii.view`, including in rights reports.
* Audit trail: create, update (field-level before/after), delete, approve, export, download, upload, import, login and configuration changes are logged with user, time and IP. Entries are hash-chained (SHA-256); **Audit Trail -> Verify integrity** detects any alteration made outside the application.
* Evidence integrity: each evidence version is stored immutably with its SHA-256; **Verify integrity** recomputes the hashes. Uploads are restricted by extension and size (50 MB), images are content-checked.
* Soft delete: compliance records are never physically deleted from the UI; deletions remain in the audit trail.
* HTTP security headers: CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, no-store on API responses.
* Organisations: all records are stored per organisation and every query is scoped by organisation; records cannot be linked across organisations. As configured for this deployment, users can switch between all organisations (grouping model) - permissions are per role, not per organisation.

## 6. Requirement library maintenance

Administration -> Requirement Library lists the 57 requirements mapped to the DPDP Act, 2023 and the DPDP Rules, 2025 (G.S.R. 846(E), 13 November 2025). Each requirement has an applicability tag that is matched against the organisation profile when an assessment is created:

| Tag | Applies when (organisation profile) |
|---|---|
| ALL | always |
| CONSENT | relies on consent |
| CHILDREN / DISABILITY_GUARDIAN | processes children's data / data through lawful guardians |
| PROCESSOR | uses Data Processors |
| CROSS_BORDER | transfers outside India |
| THIRD_SCHEDULE | e-commerce >= 2 crore users, online gaming >= 50 lakh, social media >= 2 crore |
| CONSENT_MANAGER | accepts consent through Consent Managers |
| SDF | notified Significant Data Fiduciary (DPIA, independent audit, DPO in India) |
| STATE / RESEARCH | State instrumentality / research-archiving-statistics exemption |

Commencement dates are stored per requirement (13 Nov 2026 for Rule 4 / s.6(9); 13 May 2027 for Rules 3, 5-16, 22, 23 and the remaining substantive provisions). Update wording and dates when the Government issues amendments - existing assessments keep their recorded results.

## 7. Breach notification deadline engine

Incidents & Breaches -> Deadline rules. Defaults:

| Obligation | Basis | Default | Type |
|---|---|---|---|
| Data Protection Board - initial intimation | Rule 7(2)(a) "without delay" | 24 h | internal target |
| Affected Data Principals | Rule 7(1) "without delay" | 24 h | internal target |
| Data Protection Board - detailed report | Rule 7(2)(b) | 72 h | statutory (cannot be configured above 72 h) |
| CERT-In | CERT-In Directions 28.04.2022 | 6 h | statutory, separate obligation |
| Sectoral regulator | RBI/SEBI/IRDAI etc. | 6 h | configure per regulator |

Deadlines run from the time the organisation became aware. Changing the classification of an incident (personal data breach, CERT-In reportable, sectoral) adds or retires obligations automatically.

## 8. Report engine

* Reports are generated as editable content (JSON) from live data, edited and approved in the browser, then exported. DOCX and PDF are always produced from the same stored content; editing an approved report returns it to Draft.
* Drafts carry a DRAFT watermark unless a template or organisation watermark is set. Every export is stored with its SHA-256 and logged.
* PDF conversion runs LibreOffice in a private temporary profile, one conversion at a time, with a 180-second timeout (`Pdf:TimeoutSeconds`). IIS request timeout is set to 20 minutes for large compliance packages.
* If LibreOffice is missing, DOCX export still works; Word refreshes the table of contents on opening (the document requests a field update).

## 9. Configuration reference (`app\appsettings.Local.json`)

```json
{
  "Database": { "Provider": "SqlServer", "ConnectionString": "Driver={ODBC Driver 18 for SQL Server};Server=localhost\\SQLEXPRESS;Database=DataKavach;Trusted_Connection=Yes;Encrypt=Yes;TrustServerCertificate=Yes;", "AutoMigrate": true },
  "Jwt": { "Issuer": "DataKavach", "SigningKey": "<random>", "SessionMinutes": 60 },
  "Storage": { "Root": "C:\\DataKavach\\data\\storage", "MaxUploadBytes": 52428800 },
  "Pdf": { "SofficePath": "C:\\Program Files\\LibreOffice\\program\\soffice.exe", "PythonPath": "", "TimeoutSeconds": 180 },
  "Backup": { "Directory": "C:\\DataKavach\\backups" },
  "Https": { "Redirect": false }
}
```

Application CLI (run from `C:\DataKavach\app`): `dotnet DataKavach.Api.dll --migrate | --health | --create-admin <email> <name> <password> | --set-password <email> <password> | --script-schema <dir>`.

## 10. Troubleshooting

| Symptom | Check |
|---|---|
| HTTP 500.19 / 500.30 in IIS | Hosting Bundle installed after IIS? Run `repair.ps1`; restart IIS (`iisreset`). See Event Viewer -> Application. |
| "Login failed for user IIS APPPOOL\DataKavach" | Run `repair.ps1` (re-grants SQL access) or grant `db_owner` manually. |
| "Data source name not found" | ODBC driver name in the connection string must match an installed driver (`Get-OdbcDriver`). |
| PDF export returns 503 | LibreOffice missing - `repair.ps1 -InstallLibreOffice`. |
| PDF conversion slow the first time | LibreOffice initialises a profile on first use (10-20 s); later conversions take 3-8 s. |
| Database backup fails with "Access is denied" | The SQL Server service account needs write access to the backup folder (`repair.ps1` grants it for local instances). |
| Audit chain reports broken entry | Someone modified the AuditLogs table directly. Preserve a copy of the database and investigate. |
