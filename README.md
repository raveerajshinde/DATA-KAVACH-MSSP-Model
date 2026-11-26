# Data Kavach - DPDP Compliance Management & Report Generator (v1.0.0)

Self-hosted compliance platform for the Digital Personal Data Protection Act, 2023 and DPDP Rules, 2025 (G.S.R. 846(E), notified 13 November 2025).

## Install (Windows)

1. Extract this package to a local folder.
2. Run **EnterpriseDPDP_Setup.exe** (or right-click **INSTALL.bat** -> Run as administrator).
3. Follow the prompts. The portal opens at `https://<server>:8443` when done.

See `docs\Administrator_Manual.md` for options (Windows service mode, remote SQL Server, certificates, offline prerequisites), upgrade, repair, backup/restore and uninstall.

## Package contents

| Path | Contents |
|---|---|
| `EnterpriseDPDP_Setup.exe`, `INSTALL.bat`, `install.ps1` | Installer (elevates, runs the 15-step install) |
| `upgrade.ps1`, `repair.ps1`, `uninstall.ps1`, `backup.ps1`, `restore.ps1`, `common.ps1` | Lifecycle scripts |
| `app\` | Published ASP.NET Core 10 backend with the built React frontend (`wwwroot`) |
| `database\` | SQL Server schema scripts (applied automatically by the migration runner) |
| `source\backend` | Backend source (C#), migrations, requirement library seed, report engine |
| `source\frontend` | Frontend source (React + TypeScript + MUI) |
| `source\tests` | End-to-end API test suite and UI smoke test |
| `source\tools` | Requirement library generator (with Act/Rules references) |
| `docs\` | User manual, Administrator manual, API documentation |
| `samples\` | Sample reports (DOCX + PDF for all 16 report types, cover designs, compliance package) |
| `test-results\` | Test results and UI screenshots |

## Build from source

```powershell
# backend (needs .NET 10 SDK)
cd source\backend\DataKavach.Api
dotnet publish -c Release -o ..\..\..\app
# frontend (needs Node 20+)
cd ..\..\frontend
npm ci
npm run build
Copy-Item dist\* ..\..\app\wwwroot -Recurse -Force
```

## Important notes

* Compliance indicators are self-assessment results, not a legal opinion or certification. Generated policies are drafts until approved by legal counsel.
* The requirement library reflects the Act and the Rules as notified on 13 November 2025. Review it when amendments or further notifications are issued (Administration -> Requirement Library).
