# Data Kavach - API Documentation

Base URL: `https://<server>:8443` (or `http://<server>:8080`). All responses are JSON unless a file is returned. Errors return `{ "error": "message" }` with HTTP 400 (validation), 401 (not authenticated), 403 (missing permission), 404, 409, 423 (locked), 429 (rate limit), 503 (PDF engine missing).

## 1. Authentication

| Method | Path | Body | Notes |
|---|---|---|---|
| GET | `/api/setup/status` | - | `{ needsSetup }` - true until the first administrator exists |
| POST | `/api/setup` | `{ email, name, password }` | Creates the Super Administrator (first run only) |
| POST | `/api/auth/login` | `{ email, password, mfaCode? }` | Returns `{ token, expires, user, permissions }` or `{ mfaRequired: true }`. 10 requests/min/IP |
| POST | `/api/auth/refresh` | - | New token for an active session |
| POST | `/api/auth/logout` | - | Audit entry |
| POST | `/api/auth/change-password` | `{ currentPassword, newPassword }` | |
| POST | `/api/auth/mfa/setup` | - | `{ secret, uri }` (otpauth URI) |
| POST | `/api/auth/mfa/enable` | `{ code }` | |
| POST | `/api/auth/mfa/disable` | `{ password }` | |

Send the token as `Authorization: Bearer <token>`.

## 2. Generic record API

All modules use the same record endpoints. `{key}` is one of:

`organizations, departments, requirements, assessments, assessmentItems, evidence, evidenceVersions, inventory, dataflows, dpias, dpiaRisks, consentNotices, consentRecords, consentEvents, policies, policyRecommendations, vendors, vendorAssessments, securityControls, rightsRequests, requestEvents, incidents, incidentNotifications, incidentEvents, actions, reports, reportFiles`

| Method | Path | Notes |
|---|---|---|
| GET | `/api/meta` | Entity metadata (fields, types, options, labels), roles, permissions, report types, policy templates |
| GET | `/api/e/{key}?orgId=&parentId=&q=&page=&pageSize=&sort=&desc=&f.{field}=` | Paged list `{ rows, total, page, pageSize }`. `orgId` required for organisation records; `f.` filters by exact value |
| GET | `/api/e/{key}/{id}` | One record |
| POST | `/api/e/{key}?orgId=` | Create (camelCase fields). Child records include the parent id field (e.g. `dpiaId`) |
| PUT | `/api/e/{key}/{id}` | Partial update - only supplied fields change; changes are audited field by field |
| DELETE | `/api/e/{key}/{id}` | Soft delete |
| GET | `/api/e/{key}/export?orgId=&format=xlsx|csv` | Export |
| GET | `/api/e/{key}/import-template` | Excel template with column headers |
| POST | `/api/e/{key}/import?orgId=` | multipart `file` (.xlsx/.csv) -> `{ imported, errors[] }` |

Business rules enforced server-side include: DPIA risk scoring, rights request references and due dates, incident references and notification obligations, consent history, security-control validation (no "Compliant" without validated configuration evidence), grievance period <= 90 days, vendor risk suggestion, remediation Gap IDs and plans.

## 3. Module endpoints

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/organizations/{id}/logo?kind=logo|consultant` | multipart PNG/JPG |
| DELETE | `/api/organizations/{id}/logo?kind=` | Remove logo |
| GET | `/api/files/stored/{id}` | Logo image |
| POST | `/api/assessments?orgId=` | Create assessment + questionnaire from the requirement library (auto-applicability) |
| GET | `/api/assessments/{id}/items` | Items joined with requirement text, evidence counts and status counts |
| GET | `/api/dashboard?orgId=&assessmentId=` | Dashboard metrics and deadlines |
| POST | `/api/roadmap/generate?orgId=&assessmentId=` | Create actions from open gaps (idempotent) |
| POST | `/api/actions/{id}/approve` | `{ decision: Approved|Rejected }` |
| POST | `/api/dpias/{id}/submit` / `/approve` | `{ decision, conclusion }` |
| POST | `/api/incidents/{id}/approve` | `{ kind: legal|management, decision }` |
| GET | `/api/incidents/{id}/template/{ruleKey}` | Pre-filled notification text (`DP_AFFECTED`, `DPB_INITIAL`, `DPB_DETAILED`, `CERTIN`) |
| POST | `/api/incidents/{id}/sync` | Re-evaluate notification obligations |
| GET/PUT | `/api/breach/rules?orgId=` | Deadline engine configuration |
| POST | `/api/policies/generate?orgId=` | `{ templateKey }` -> `{ id }` |
| POST | `/api/policies/upload?orgId=` | multipart `file`, `title`, `policyType` -> analysed policy |
| POST | `/api/policies/{id}/analyze` | Re-run DPDP gap analysis |
| POST | `/api/policyRecommendations/{id}/decide` | `{ status: Approved|Rejected|Pending, revisedClause? }` |
| POST | `/api/policies/{id}/apply` | New version applying approved recommendations (original kept) |
| POST | `/api/policies/{id}/approve` | `{ notes }` |
| POST | `/api/evidence?orgId=` | multipart `file`, `title`, `category`, `description`, `linkedEntity`, `linkedId`, `comment` |
| POST | `/api/evidence/{id}/versions` | multipart `file`, `comment` |
| GET | `/api/evidence/{id}/verify` | SHA-256 verification per version |
| GET | `/api/files/evidence/{versionId}` | Download (audited) |

## 4. Reports and templates

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/reports/status` | `{ pdfAvailable }` |
| POST | `/api/reports` | `{ orgId, reportType, assessmentId?, periodFrom?, periodTo?, entityId?, templateId? }` -> `{ id }` (Draft) |
| GET | `/api/reports/{id}` | `{ report, content }` - content = `{ title, subtitle, sections[ { id, heading, level, include, pageBreakBefore, blocks[] } ] }` |
| PUT | `/api/reports/{id}/content` | Save edited content (resets approval) |
| POST | `/api/reports/{id}/approve` | |
| GET | `/api/reports/{id}/export?format=docx|pdf` | File download; stored and audited |
| GET | `/api/reports/{id}/preview.pdf` | Inline PDF |
| GET | `/api/files/report/{fileId}` | Earlier export |
| POST | `/api/reports/package` | `{ orgId, types[], formats[], assessmentId?, periodFrom?, periodTo?, templateId? }` -> ZIP |
| GET | `/api/reports/outline?orgId=&type=` | Section ids/headings (template designer) |
| GET | `/api/reports/branding-preview?orgId=&templateId=&cover=` | Sample PDF |
| GET/POST | `/api/templates?orgId=` | List / create |
| PUT/DELETE | `/api/templates/{id}` | Update / delete (not default templates) |
| POST | `/api/templates/{id}/duplicate?orgId=` | |
| POST | `/api/templates/restore-defaults` | |

Block types in report content: `p` (text, `**bold**`), `h2`, `bullets` (items), `table` (columns, rows, widths), `kv` (rows of [label, value]), `chart` (chartType `bar|hbar|pie|stacked`, data[{label,value,values}], series), `callout` (title, text, tone `info|warning|danger|success`), `pagebreak`, `signature`.

Report type keys: `executive-summary, gap-assessment, data-inventory, processing-register, data-flow, dpia, consent, privacy-policy, data-protection-policy, vendor-risk, security, rights, breach-readiness, incident, roadmap, master`.

## 5. Administration

| Method | Path | Permission |
|---|---|---|
| GET/POST | `/api/users` | admin.users |
| PUT/DELETE | `/api/users/{id}` | admin.users |
| POST | `/api/users/{id}/reset-password` `{ password }`, `/reset-mfa` | admin.users |
| GET | `/api/roles`; PUT `/api/roles/{role}` (array of permissions) | admin.users |
| GET | `/api/audit?orgId=&entity=&entityId=&action=&user=&page=` | admin.audit |
| GET | `/api/audit/verify` | admin.audit |
| GET/PUT | `/api/settings?orgId=` `{ rightsResponseDays }` | admin.settings |
| GET/POST | `/api/apikeys?orgId=`; DELETE `/api/apikeys/{id}` | admin.apikeys |
| POST | `/api/admin/backup`; GET `/api/admin/backups` | admin.backup |
| GET | `/api/health` | public |

## 6. Service-provider (MSSP) endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/api/branding` | Public. Provider name, tagline, portal name, colours, support contacts, `hasLogo` |
| GET | `/api/branding/logo` | Public. Provider logo |
| PUT | `/api/branding` | Provider staff with admin.settings |
| POST/DELETE | `/api/branding/logo` | multipart PNG/JPG |
| GET | `/api/portfolio` | Provider staff. `{ totals, daysToCore, clients[] }` with health, indicator, gaps, overdue items per client |
| POST | `/api/clients/onboard` | `{ name, industry, adminName?, adminEmail?, adminPassword? }` -> `{ orgId, userId }` |

User records carry `userType` (`Provider` / `Customer`) and `organizationId`. Customer roles (`Customer Administrator`, `Customer User`, `Customer Viewer`) require `organizationId` when created by provider staff. Sign-in returns both in `user`. Every endpoint returns 403 when a customer token addresses another organisation.

## 7. Integration API (external applications)

Authenticate with an organisation API key (Consent Management -> Integration API) in the `X-Api-Key` header. 120 requests/min per key.

```http
POST /api/v1/consents
X-Api-Key: dk_...
Content-Type: application/json

{ "principalRef": "CUST-1001", "identifierType": "Customer ID", "purpose": "Order updates",
  "noticeId": 3, "language": "English", "channel": "Website", "evidenceRef": "form-8812", "isParentalConsent": false }
```
Response: `{ "id": 41, "status": "Given", "givenAt": "...", "noticeVersion": "2.0" }`

| Method | Path | Body / query | Result |
|---|---|---|---|
| POST | `/api/v1/consents` | consent record fields | Records consent with notice version |
| POST | `/api/v1/consents/withdraw` | `{ principalRef, purpose? }` | `{ withdrawn }` - all active consents or one purpose |
| GET | `/api/v1/consents?principalRef=` | - | Consent status per purpose |
| POST | `/api/v1/rights-requests` | `{ requestType, principalName, principalContact, principalIdentifier, details }` | `{ reference, dueAt }` |

`requestType` values: `Access (s.11)`, `Correction / completion / update (s.12)`, `Erasure (s.12(3))`, `Consent withdrawal (s.6(4))`, `Grievance (s.13)`, `Nomination (s.14)`, `Other`.
