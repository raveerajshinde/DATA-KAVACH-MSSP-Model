# Data Kavach - User Manual

Data Kavach helps an organisation assess, manage, document and monitor compliance with the **Digital Personal Data Protection Act, 2023** and the **Digital Personal Data Protection Rules, 2025**, and produce professional Word and PDF reports.

> Indicators and percentages in Data Kavach reflect self-assessment status only. They are not a legal opinion, certification or proof of compliance. Generated policies are drafts until reviewed and approved by legal counsel.

## Two kinds of users

* **Provider staff** (your company) sign in to the **Client Portfolio**, which lists every client with its health (On track / Needs attention / At risk / Not started) and key exposures. Click a client to open its dashboard, or use the *Client* selector at the top to switch. *Onboard client* creates a new client and its portal administrator in one step.
* **Customers** sign in to their own **Customer Portal** (shown as "Managed by <your company>"). They see and work only on their organisation's data. A Customer Administrator can add colleagues as Customer User (works on assessments, evidence, inventory, consent, rights, incidents, vendors, DPIA, security, roadmap; can export reports) or Customer Viewer (read-only, can download reports).

## Getting started (recommended order)

1. **Organisation & Branding** - create the organisation, fill the profile, privacy contacts and especially **Applicability** (consent, children's data, processors, cross-border transfers, Significant Data Fiduciary, Third Schedule class). Upload logos, choose colours, font, cover design, header/footer, watermark and signatories. The live preview updates as you type; *Preview actual PDF* renders a sample through the real report engine.
2. **Data Inventory & Flows** - record each processing activity: data principals, personal data, source, purpose, **permitted ground** (consent or the specific s.7 legitimate use, e.g. s.7(i) for employment), systems, cloud provider, access roles, recipients, processors, cross-border transfers, retention, erasure method and safeguards. Import/export with Excel or CSV (download the import template first).
3. **Compliance Assessment** - create an assessment. Requirements that do not apply to your profile start as *Not Applicable* with the reason recorded (you can override). For each requirement read the question, guidance and expected evidence, set the status (Compliant, Partially Compliant, Non-Compliant, Not Assessed, Not Applicable), risk, department, owner, target date, observations, gap and recommendation, and attach evidence.
4. **Security Safeguards** - assess the 19 Rule 6 controls. Map your tools (Sentinel, Wazuh, Splunk, Elastic, CrowdStrike, Defender, Fortinet, Palo Alto...). A control can be marked Compliant only after configuration evidence is recorded and the control is validated.
5. **Vendors & Processors** - register vendors, data accessed, location, sub-processors, certifications, contract and DPA status and clauses (security, breach notification window, erasure, audit). Risk is suggested automatically. Record periodic assessments.
6. **Impact Assessments (DPIA)** - mandatory only for notified Significant Data Fiduciaries; recommended for high-risk processing. Add risks with likelihood x impact (1-5) before and after controls; the heat map and overall residual risk update automatically. Submit for review and approve.
7. **Consent Management** - publish notices (versioned, language), record purpose-specific consents and withdrawals with full history, and connect websites/apps through the Integration API (API keys).
8. **Data Principal Rights** - log access, correction, erasure, withdrawal, grievance and nomination requests. Due dates are calculated (grievances use your published period, max 90 days). Every change is recorded in the request's audit trail; record closure evidence.
9. **Breaches & Incidents** - record the time you became aware. Mark the incident as a personal data breach and/or CERT-In/sectoral reportable to create the notification obligations with countdowns. Use *Prepare & record* to open a pre-filled template (Rule 7(1), 7(2)(a), 7(2)(b), CERT-In), edit, send through your channel and record the time, method and reference. Add timeline entries and record legal and management approvals. Mark exercises as *Mock drill* to measure readiness against 72 hours.
10. **Policies & Notices** - generate drafts from 12 templates, or upload your existing policy (.docx/.txt) to receive clause-level recommendations: existing clause, recommended modification, reason, legal reference and revised clause. Approve or reject each, then *Create revised version* - the original is never overwritten. Approve the policy internally after legal review.
11. **Remediation Roadmap** - *Generate from gaps* creates actions from non-compliant and partially compliant requirements, failing safeguards, vendor contract gaps and high DPIA risks. Priority follows penalty exposure (security safeguards and breach handling first) and actions are placed in 30-, 60- and 90-day plans. Update status, evidence and approval.
12. **Reports** - see below.

## Dashboard

Shows the assessment indicator and completion, status distribution, open gaps by risk, module-wise and department-wise status, remediation progress, evidence pending, vendor risk, safeguard validation, rights and incident status, and upcoming deadlines (breach notifications, requests, actions, vendor and DPIA reviews, and the 13 May 2027 commencement).

## Report Generator

1. Choose one of the 16 report types: Executive Summary, Gap Assessment, Personal Data Inventory, Data Processing Register, Data Flow Mapping, DPIA, Consent Management, Privacy Policy, Data Protection Policy, Vendor Risk, Security Safeguards, Data Principal Rights, Breach Readiness, Incident and Breach, Remediation Roadmap, Master Compliance Report.
2. Select the assessment, reporting period and (optionally) a template, then **Generate draft for review**.
3. In the editor: include/exclude and reorder sections, edit headings, paragraphs (`**bold**`), bullet lists, tables (cells, rows) and callouts, add sections or page breaks. *Preview whole report* shows the formatted content.
4. **Approve** (requires approval permission). Editing an approved report returns it to Draft.
5. Export **Word (DOCX)** or **PDF** - both come from the same content and include the branded cover, organisation and consultant logos, header and footer, page numbers ("Page X of Y"), table of contents, document control, confidentiality marking, risk tables, charts, evidence references and sign-off block. Every export is kept in *Export history*.
6. **Complete compliance package** builds several reports at once into a ZIP with a SHA-256 manifest.

All text and tables in the Word file remain editable; charts are native Word charts.

## Template Designer

Default templates (five cover designs) are read-only - **Duplicate** one to customise it. Choose the report type (or any), cover design, logo placement, font, colours and watermark, drag sections into order, switch sections on/off, start sections on a new page, and add custom text or table sections. **Preview PDF** renders the branding through the real engine. Select the template when generating a report.

## Evidence Repository

All evidence across modules. Each upload becomes an immutable version with SHA-256 fingerprint, uploader and time; downloads are logged. **Verify integrity** recomputes fingerprints.

## Account security

Settings & Security -> set up MFA (scan the QR code with an authenticator app) and change your password. Sessions refresh while you work and end after 30 minutes of inactivity.

## Roles (defaults - administrators can change them)

| Role | Typical access |
|---|---|
| Super Administrator | Everything, including backups and Super Administrator management |
| Organisation Administrator | Everything except backups |
| Compliance Manager / DPO | View, edit, approve and export all modules; unmasked personal data; audit trail |
| IT Security Manager | View all; edit security, incidents, evidence, roadmap, inventory, vendors; approve incidents |
| HR Manager | View all; edit inventory, rights, consent, evidence, roadmap; unmasked personal data |
| Department Manager | View all; edit assessment, evidence, roadmap, inventory |
| Auditor | View and export all; audit trail |
| Consultant | View, edit and export all modules |
| Read-only User | View only; personal data masked |
| Customer Administrator | Customer portal: everything for their own organisation, including approvals, report export, their own portal users and audit trail |
| Customer User | Customer portal: view all; edit assessment, evidence, inventory, consent, rights, incidents, vendors, DPIA, security, roadmap; generate and export reports |
| Customer Viewer | Customer portal: view only; download reports |
