# Data Kavach - API test results

Target: `http://127.0.0.1:5090`  
Run: 2026-11-26 07:40:34  
Result: **31/31 passed**

| # | Test | Result | Detail | Time (s) |
|---|---|---|---|---|
| 1 | Setup: first-run status and Super Administrator creation | PASS | setup locked after first admin | 0.4 |
| 2 | Auth: login, wrong password, lockout counter, change password, refresh | PASS | 77 permissions for Super Administrator | 0.6 |
| 3 | Auth: TOTP MFA enrolment and MFA-required login | PASS | otpauth URI issued, code verified | 0.3 |
| 4 | Metadata: entities, roles, report types, policy templates | PASS | 30 entities | 0.0 |
| 5 | Organisation: registration, applicability flags, validation, seeded departments and controls | PASS | org #1, 8 departments, 19 Rule 6 controls seeded | 0.2 |
| 6 | Branding: company and consultant logo upload (type-checked), branding preview PDF | PASS | preview PDF 79 KB | 1.1 |
| 7 | Assessment: questionnaire generated from requirement library with auto-applicability | PASS | 57 requirements, 10 auto Not Applicable (SDF, State, research, Third Schedule, guardian, Consent Manager) | 0.0 |
| 8 | Assessment: status updates, risk, owner, target date; status calculation | PASS | indicator 44.4% = (5 + 0.5x6)/18; completion 38.3% | 0.2 |
| 9 | Evidence: upload, link to requirement, versioning, SHA-256 integrity verification, download audit | PASS | 2 versions, hashes intact, .exe rejected | 0.1 |
| 10 | Data inventory: CRUD, permitted grounds, CSV/XLSX export, XLSX import | PASS | 3 activities, CSV+XLSX export, XLSX round-trip import into org #2 | 0.1 |
| 11 | DPIA: voluntary DPIA, risk scoring (inherent/residual), approval workflow | PASS | scores 20/High->15/High and 12/Medium->6/Low; overall High; approved | 0.1 |
| 12 | Consent: notice versions, records, withdrawal history; integration API with API key | PASS | notice v2.0 linked, withdrawal logged, API consent + withdraw + rights intake | 0.1 |
| 13 | Policies: template generation (draft, not legally approved), upload + DPDP gap analysis, apply approved, approve | PASS | 11 recommendations incl. GDPR/legitimate-interest flags; original retained, revised v1.1 created | 0.2 |
| 14 | Vendors: auto risk rating, assessment history, contract gaps | PASS | High (no DPA) / Medium (DPA needs update) / Medium (compliant but holds data) | 0.0 |
| 15 | Security safeguards: tool mapping cannot mark Compliant without validated configuration evidence | PASS | blocked tool-only Compliant; accepted after evidence + validation | 0.0 |
| 16 | Data principal rights: reference, due date (grievance <=90 days), status audit trail, masking | PASS | due dates computed; 7 audit-trail events | 0.1 |
| 17 | Breach: notification deadline engine (DPDP without-delay, 72h Board, CERT-In 6h), sending, approvals, templates | PASS | 4 obligations with distinct deadlines; re-classification marks DPDP items Not required; 72h cap enforced | 0.2 |
| 18 | Roadmap: auto-generation from gaps (30/60/90 by penalty-weighted risk), approval | PASS | 18 actions created; idempotent re-run created 0 | 0.2 |
| 19 | Dashboard: status, risk, module/department breakdown, deadlines, disclaimer | PASS | indicator 44.4%, 5 deadlines, 17 open actions | 0.0 |
| 20 | Permissions: role enforcement (Read-only, Auditor), Super Admin protection | PASS | 403 on write/admin for Read-only; export allowed for Auditor; PII masked for Read-only | 0.4 |
| 21 | Organisation separation: records stay with their organisation | PASS | cross-organisation parent linking rejected; orgId required on scoped lists | 0.0 |
| 22 | Reports: all 16 report types generate from live data | PASS | 16 drafts; master report has 31 sections | 0.4 |
| 23 | Reports: edit content, approve, editable DOCX + PDF export from the same content | PASS | DOCX 23 KB (text, tables, native charts, TOC field), PDF 460 KB | 3.1 |
| 24 | Reports: export remaining types as DOCX and PDF (sample reports) | PASS | 30 files | 31.9 |
| 25 | Report templates: designer config (order, include, custom section), cover design, duplicate, restore defaults | PASS | custom section first, excluded section hidden, purple/Georgia Bold Block cover rendered | 3.1 |
| 26 | Reports: complete compliance package (ZIP with manifest and SHA-256) | PASS | 6 reports + manifest | 6.2 |
| 27 | Audit trail: create/modify/approve/export/delete logged; hash chain verifies | PASS | 203 entries, chain intact | 0.0 |
| 28 | Backup: on-demand backup of files (and database on SQL Server) | PASS | DataKavach_files_20261126_021031.zip | 0.1 |
| 29 | Health endpoint | PASS | schema v2, pdf engine True | 0.0 |
| 30 | MSSP: provider branding (public), logo, portfolio of all clients, one-step client onboarding | PASS | 3 clients, avg indicator 44.4, health {'On track': 0, 'Needs attention': 0, 'At risk': 1, 'Not started': 2} | 0.2 |
| 31 | MSSP: customer portal user is confined to its own organisation | PASS | 403 on other clients' data, portfolio, branding, backup, shared library; own data and reports work | 2.8 |
