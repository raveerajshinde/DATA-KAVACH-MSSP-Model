# Data Kavach 1.0.0 - Test Report

Run date: 26 Nov 2026 01:26 UTC against the packaged Release build (`app\`).

## Summary

| Area | Method | Result |
|---|---|---|
| End-to-end API (all modules) | `source/tests/api_test.py` - 31 scenarios on a fresh database | **31 / 31 passed** (detail: `api_test_results.md`) |
| Word output validity | All 16 report types validated against the ECMA-376 / ISO 29500 XSD schemas | **16 / 16 valid** |
| PDF output | All 16 report types + cover designs + template + package rendered by LibreOffice | 20 PDFs produced (`samples/`) |
| Table of contents page numbers | Every TOC entry checked against the page the heading actually appears on (Gap Assessment, Master Report) | **19 / 19 correct** |
| User interface | `source/tests/ui_smoke.py` - headless Chrome signs in as provider staff and as a customer portal user and opens 26 screens each | **No browser errors**; screenshots in `screenshots/provider` and `screenshots/customer` |
| MSSP isolation | Customer portal user tested against another client's lists, records, dashboard, reports, report files, organisation creation, portfolio, branding, backup, shared library, staff users | **All blocked (403)**; own organisation fully usable |
| Installer / lifecycle scripts | Parsed with the PowerShell 7.4 language parser | **7 / 7 parse without errors** |
| Windows bootstrapper | `EnterpriseDPDP_Setup.exe` built with MinGW-w64 (PE32+ x64, elevates and launches install.ps1) | Built |

## Acceptance criteria coverage

| # | Criterion | Evidence |
|---|---|---|
| 1 | Organisation registration works | API test "Organisation: registration..." |
| 2 | Branding configuration works | "Branding: ... logo upload ... branding preview PDF"; cover PDFs in samples |
| 3 | Assessment questionnaires work | "Assessment: questionnaire generated ... auto-applicability" |
| 4 | Compliance status calculated correctly | "Assessment: status updates ..." asserts indicator = (C + 0.5 P) / assessed |
| 5 | Evidence upload works | "Evidence: upload, versioning, SHA-256 verification, download audit" |
| 6 | Findings and remediation workflows work | "Roadmap: auto-generation ...", gap findings in Gap Assessment report |
| 7 | Policy templates generate correctly | "Policies: template generation ... upload + DPDP gap analysis ..." |
| 8 | Reports generate in editable DOCX | 16 DOCX schema-valid; text, tables and native charts (no flattened images) |
| 9 | Reports generate in professional PDF | 16 PDFs with cover, header/footer, page numbers, TOC, charts |
| 10 | Organisation information appears in reports | Cover, document control, scope tables (see samples) |
| 11 | Logo and brand colours appear correctly | Header/cover logos and colours; Board-pack template uses purple/Georgia override |
| 12 | Page numbers and tables of contents | "Page X of Y" footer; TOC verified page-accurate |
| 13 | User permissions enforced | "Permissions: role enforcement ..." (403s, PII masking) |
| 14 | Organisations isolated | "MSSP: customer portal user is confined to its own organisation" - customers see only their organisation; provider staff see all clients by design |
| 15 | All modules connect to the database | Every module exercised through the API |
| 16 | Installer works on a clean Windows machine | **Not executed in the build environment (Linux).** Scripts parse cleanly; run on a clean Windows VM before production |
| 17 | Backup and restore tested | In-app backup tested; `backup.ps1` / `restore.ps1` **not executed here** (Windows + SQL Server required) |

## Test environment limitations (read before go-live)

* The build sandbox had no SQL Server. The full suite ran through the same ODBC data-access code against SQLite. The SQL Server schema script and T-SQL paths (OUTPUT INSERTED, OFFSET/FETCH, BACKUP DATABASE) get their first real execution on your server. Run `api_test.py` against the installed instance:
  `python api_test.py https://<server>:8443 <output-folder>` on an empty database.
* NuGet was not reachable from the build environment, so the backend uses only packages available offline (System.Data.Odbc, Microsoft.IdentityModel, Hosting.WindowsServices) instead of EF Core / SqlClient / Open XML SDK. Functionality is equivalent; the code compiles on any machine with the .NET 10 SDK.
* Word was not available; DOCX files were validated against the official schemas and rendered with LibreOffice. Open a few samples in Microsoft Word as part of user acceptance.
