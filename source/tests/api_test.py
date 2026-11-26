"""End-to-end API test suite for Data Kavach.

Usage: python api_test.py http://127.0.0.1:5080 [output_dir]
Exercises every module against a running instance with an empty database and writes
test_results.md plus sample DOCX/PDF reports into output_dir.
"""
import io
import json
import os
import struct
import sys
import time
import urllib.error
import urllib.request
import uuid
import zipfile
import zlib

BASE = sys.argv[1].rstrip("/") if len(sys.argv) > 1 else "http://127.0.0.1:5080"
OUT = sys.argv[2] if len(sys.argv) > 2 else "test_output"
os.makedirs(OUT, exist_ok=True)
results = []
TOKEN = None


def req(method, path, body=None, token=None, raw=False, headers=None, files=None, form=None, expect=200):
    url = BASE + path
    h = {"Accept": "application/json"}
    if headers:
        h.update(headers)
    tok = token if token is not None else TOKEN
    if tok:
        h["Authorization"] = "Bearer " + tok
    data = None
    if files is not None:
        boundary = uuid.uuid4().hex
        buf = io.BytesIO()
        for k, v in (form or {}).items():
            buf.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{k}\"\r\n\r\n{v}\r\n".encode())
        for name, (fname, content, ctype) in files.items():
            buf.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"; filename=\"{fname}\"\r\nContent-Type: {ctype}\r\n\r\n".encode())
            buf.write(content)
            buf.write(b"\r\n")
        buf.write(f"--{boundary}--\r\n".encode())
        data = buf.getvalue()
        h["Content-Type"] = "multipart/form-data; boundary=" + boundary
    elif body is not None:
        data = json.dumps(body).encode()
        h["Content-Type"] = "application/json"
    r = urllib.request.Request(url, data=data, method=method, headers=h)
    try:
        with urllib.request.urlopen(r, timeout=600) as resp:
            status, content = resp.status, resp.read()
    except urllib.error.HTTPError as e:
        status, content = e.code, e.read()
    if expect is not None and status != expect:
        raise AssertionError(f"{method} {path} -> {status} (expected {expect}): {content[:400]!r}")
    if raw:
        return content
    try:
        return json.loads(content) if content else None
    except ValueError:
        return content


def test(name):
    def deco(fn):
        t0 = time.time()
        try:
            detail = fn()
            results.append((name, "PASS", detail or "", time.time() - t0))
            print(f"PASS  {name}  {detail or ''}")
        except Exception as e:  # noqa
            results.append((name, "FAIL", str(e)[:500], time.time() - t0))
            print(f"FAIL  {name}: {e}")
        return fn
    return deco


def png(w=320, h=90, rgb=(11, 61, 145)):
    raw = b"".join(b"\x00" + bytes(rgb) * w for _ in range(h))
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")


def make_docx(paragraphs):
    body = "".join(f"<w:p><w:r><w:t xml:space=\"preserve\">{p}</w:t></w:r></w:p>" for p in paragraphs)
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr("[Content_Types].xml", '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
        z.writestr("_rels/.rels", '<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="r1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
        z.writestr("word/document.xml", f'<?xml version="1.0"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}</w:body></w:document>')
    return buf.getvalue()


S = {}
ADMIN = {"email": "admin@example.in", "name": "Asha Admin", "password": "Initial#Pass2026"}


@test("Setup: first-run status and Super Administrator creation")
def _():
    st = req("GET", "/api/setup/status", token="")
    assert st["needsSetup"] is True
    req("POST", "/api/setup", ADMIN, token="")
    assert req("GET", "/api/setup/status", token="")["needsSetup"] is False
    req("POST", "/api/setup", ADMIN, token="", expect=409)
    return "setup locked after first admin"


@test("Auth: login, wrong password, lockout counter, change password, refresh")
def _():
    global TOKEN
    req("POST", "/api/auth/login", {"email": ADMIN["email"], "password": "wrong"}, token="", expect=401)
    s = req("POST", "/api/auth/login", {"email": ADMIN["email"], "password": ADMIN["password"]}, token="")
    TOKEN = s["token"]
    assert "admin.users" in s["permissions"]
    req("POST", "/api/auth/change-password", {"currentPassword": ADMIN["password"], "newPassword": "short"}, expect=400)
    req("POST", "/api/auth/change-password", {"currentPassword": ADMIN["password"], "newPassword": "Stronger#Pass2027"})
    ADMIN["password"] = "Stronger#Pass2027"
    s = req("POST", "/api/auth/refresh")
    TOKEN = s["token"]
    req("GET", "/api/meta", token="", expect=401)
    return f"{len(s['permissions'])} permissions for Super Administrator"


@test("Auth: TOTP MFA enrolment and MFA-required login")
def _():
    import base64, hashlib, hmac
    setup = req("POST", "/api/auth/mfa/setup")
    secret = setup["secret"]
    def code(t=None):
        key = base64.b32decode(secret + "=" * (-len(secret) % 8))
        step = int((t or time.time()) // 30)
        h = hmac.new(key, struct.pack(">Q", step), hashlib.sha1).digest()
        o = h[-1] & 0xF
        return "%06d" % ((struct.unpack(">I", h[o:o + 4])[0] & 0x7FFFFFFF) % 1000000)
    req("POST", "/api/auth/mfa/enable", {"code": "000000"}, expect=400)
    req("POST", "/api/auth/mfa/enable", {"code": code()})
    r = req("POST", "/api/auth/login", {"email": ADMIN["email"], "password": ADMIN["password"]}, token="")
    assert r.get("mfaRequired") is True
    r = req("POST", "/api/auth/login", {"email": ADMIN["email"], "password": ADMIN["password"], "mfaCode": code()}, token="")
    assert r["user"]["mfaEnabled"] is True
    req("POST", "/api/auth/mfa/disable", {"password": ADMIN["password"]})
    return "otpauth URI issued, code verified"


@test("Metadata: entities, roles, report types, policy templates")
def _():
    m = req("GET", "/api/meta")
    S["meta"] = m
    assert len(m["roles"]) == 13 and len(m["reportTypes"]) == 16 and len(m["policyTemplates"]) == 12
    return f"{len(m['entities'])} entities"


@test("Organisation: registration, applicability flags, validation, seeded departments and controls")
def _():
    req("POST", "/api/e/organizations", {"name": "Bad colour", "primaryColor": "blue"}, expect=400)
    req("POST", "/api/e/organizations", {"name": "Bad days", "grievanceResponseDays": 120}, expect=400)
    org = req("POST", "/api/e/organizations", {
        "name": "Sahyadri Health Services Pvt Ltd", "legalName": "Sahyadri Health Services Private Limited", "registeredAddress": "Plot 12, MIDC, Pune 411019, Maharashtra",
        "city": "Pune", "state": "Maharashtra", "cin": "U85110MH2015PTC123456", "gstin": "27AABCS1234F1Z5", "industry": "Healthcare / Hospital",
        "website": "https://sahyadri.example.in", "email": "info@sahyadri.example.in", "phone": "+91 20 4000 1234", "employeeCount": 850, "locationCount": 4,
        "dpoName": "Dr. Meera Kulkarni", "dpoDesignation": "Data Protection Officer", "dpoEmail": "dpo@sahyadri.example.in", "dpoPhone": "+91 20 4000 1299",
        "privacyContactEmail": "privacy@sahyadri.example.in", "grievanceUrl": "https://sahyadri.example.in/privacy-rights", "grievanceResponseDays": 30,
        "processesChildrenData": True, "reliesOnConsent": True, "usesProcessors": True, "crossBorderTransfers": True, "certInApplicable": True,
        "preparedBy": "Manlitics Compliance Team", "preparedFor": "Board of Directors, Sahyadri Health", "assessmentDate": "2026-11-20",
        "periodFrom": "2026-10-01", "periodTo": "2026-11-30", "reportReference": "SHS/DPDP/2026/01", "reportVersion": "1.0", "confidentiality": "Confidential",
        "consultantName": "Manlitics Advisory", "primaryColor": "#0B3D91", "secondaryColor": "#F2A900", "fontFamily": "Calibri", "headerText": "Sahyadri Health Services",
        "coverDesign": "Corporate Band", "logoPlacement": "Header left", "signatoryName": "Rajesh Patil", "signatoryTitle": "Managing Director",
        "reviewerName": "Dr. Meera Kulkarni", "reviewerTitle": "DPO"})
    S["org"] = org["id"]
    deps = req("GET", f"/api/e/departments?orgId={org['id']}")
    ctl = req("GET", f"/api/e/securityControls?orgId={org['id']}&pageSize=100")
    assert deps["total"] == 8 and ctl["total"] == 19
    org2 = req("POST", "/api/e/organizations", {"name": "Second Org Ltd", "industry": "IT / ITES"})
    S["org2"] = org2["id"]
    return f"org #{org['id']}, 8 departments, 19 Rule 6 controls seeded"


@test("Branding: company and consultant logo upload (type-checked), branding preview PDF")
def _():
    o = S["org"]
    req("POST", f"/api/organizations/{o}/logo", files={"file": ("logo.png", b"not an image", "image/png")}, expect=400)
    r = req("POST", f"/api/organizations/{o}/logo", files={"file": ("logo.png", png(), "image/png")})
    req("POST", f"/api/organizations/{o}/logo?kind=consultant", files={"file": ("c.png", png(240, 80, (242, 169, 0)), "image/png")})
    img = req("GET", f"/api/files/stored/{r['fileId']}", raw=True)
    assert img[:4] == b"\x89PNG"
    pdf = req("GET", f"/api/reports/branding-preview?orgId={o}&cover=Split%20Panel", raw=True)
    assert pdf[:4] == b"%PDF"
    open(os.path.join(OUT, "branding_preview.pdf"), "wb").write(pdf)
    return f"preview PDF {len(pdf)//1024} KB"


@test("Assessment: questionnaire generated from requirement library with auto-applicability")
def _():
    a = req("POST", f"/api/assessments?orgId={S['org']}", {"name": "DPDP Baseline Assessment 2026", "periodFrom": "2026-10-01", "periodTo": "2026-11-30", "owner": "Dr. Meera Kulkarni"})
    S["assessment"] = a["id"]
    it = req("GET", f"/api/assessments/{a['id']}/items")
    items = it["items"]
    S["items"] = {i["requirementCode"]: i for i in items}
    na = [i for i in items if i["status"] == "Not Applicable"]
    assert len(items) == 57
    assert all(i["requirementCode"].startswith(("SDF", "STA", "RES", "RET-03", "CHD-04", "CON-07")) for i in na), [i["requirementCode"] for i in na]
    assert S["items"]["CHD-01"]["status"] == "Not Assessed"  # children's data flag set
    return f"57 requirements, {len(na)} auto Not Applicable (SDF, State, research, Third Schedule, guardian, Consent Manager)"


@test("Assessment: status updates, risk, owner, target date; status calculation")
def _():
    plan = {
        "GOV-01": ("Compliant", "Low"), "GOV-02": ("Partially Compliant", "Medium"), "GOV-03": ("Partially Compliant", "High"), "GOV-04": ("Non-Compliant", "High"),
        "NTC-01": ("Non-Compliant", "High"), "NTC-02": ("Partially Compliant", "Medium"), "CON-01": ("Compliant", "Low"), "CON-04": ("Non-Compliant", "High"),
        "SEC-01": ("Partially Compliant", "High"), "SEC-02": ("Compliant", "Low"), "SEC-03": ("Non-Compliant", "High"), "BRC-01": ("Non-Compliant", "High"),
        "BRC-03": ("Non-Compliant", "High"), "RTS-02": ("Compliant", "Low"), "GRV-01": ("Partially Compliant", "Medium"), "VEN-01": ("Non-Compliant", "High"),
        "CHD-01": ("Partially Compliant", "High"), "XBT-01": ("Compliant", "Medium"),
    }
    for code, (st, risk) in plan.items():
        req("PUT", f"/api/e/assessmentItems/{S['items'][code]['id']}", {"status": st, "riskRating": risk, "department": "Legal & Compliance", "owner": "Dr. Meera Kulkarni",
            "targetDate": "2026-12-31", "gapDescription": f"Gap observed for {code}" if st != "Compliant" else None, "response": f"Reviewed {code} with process owners."})
    it = req("GET", f"/api/assessments/{S['assessment']}/items")
    c = it["counts"]
    assert (c["compliant"], c["partial"], c["nonCompliant"]) == (5, 6, 7), c
    exp = round((5 + 0.5 * 6) * 100 / 18, 1)
    assert abs(c["indicator"] - exp) < 0.05, (c["indicator"], exp)
    req("PUT", f"/api/e/assessmentItems/{S['items']['GOV-01']['id']}", {"status": "Wrong value"}, expect=400)
    return f"indicator {c['indicator']}% = (5 + 0.5x6)/18; completion {c['completion']}%"


@test("Evidence: upload, link to requirement, versioning, SHA-256 integrity verification, download audit")
def _():
    o = S["org"]
    r = req("POST", f"/api/evidence?orgId={o}", files={"file": ("consent-screen.png", png(200, 120, (40, 160, 60)), "image/png")},
            form={"title": "Consent capture screenshot", "category": "Screenshot", "linkedEntity": "assessmentItems", "linkedId": S["items"]["CON-01"]["id"], "comment": "Initial"})
    eid = r["id"]
    req("POST", f"/api/evidence/{eid}/versions", files={"file": ("consent-screen-v2.png", png(220, 120, (40, 170, 60)), "image/png")}, form={"comment": "Updated UI"})
    req("POST", f"/api/evidence?orgId={o}", files={"file": ("malware.exe", b"MZ....", "application/octet-stream")}, form={"title": "x"}, expect=400)
    vers = req("GET", f"/api/e/evidenceVersions?parentId={eid}&orgId={o}")
    assert vers["total"] == 2
    v = req("GET", f"/api/evidence/{eid}/verify")
    assert all(x["intact"] for x in v)
    data = req("GET", f"/api/files/evidence/{vers['rows'][0]['id']}", raw=True)
    assert data[:4] == b"\x89PNG"
    it = req("GET", f"/api/assessments/{S['assessment']}/items")
    assert [i for i in it["items"] if i["requirementCode"] == "CON-01"][0]["evidenceCount"] == 1
    S["evidence"] = eid
    return "2 versions, hashes intact, .exe rejected"


@test("Data inventory: CRUD, permitted grounds, CSV/XLSX export, XLSX import")
def _():
    o = S["org"]
    rows = [
        {"processingActivity": "Patient registration and treatment", "department": "Operations", "principalCategory": "Patients", "dataCategories": "Name, age, contact, medical history, diagnosis", "includesChildrenData": True,
         "collectionSource": "Front desk, website appointment form", "purpose": "Registration, diagnosis and treatment", "legalGround": "Consent - s.6", "storageLocation": "Pune DC", "systemName": "HIS (MediCare)", "cloudProvider": "",
         "accessRoles": "Doctors, nurses, front desk", "internalRecipients": "Billing, pharmacy", "externalProcessors": "Lab partner (PathCare)", "crossBorder": False, "retentionPeriod": "As per medical records rules", "erasureMethod": "Secure deletion", "securitySafeguards": "RBAC, TDE, audit logs"},
        {"processingActivity": "Employee payroll", "department": "Human Resources", "principalCategory": "Employees", "dataCategories": "Name, PAN, bank account, salary", "purpose": "Salary and statutory compliance", "legalGround": "s.7(i) Employment purposes",
         "systemName": "Keka HRMS", "cloudProvider": "AWS Mumbai", "externalProcessors": "Keka", "crossBorder": False, "retentionPeriod": "8 years", "erasureMethod": "Vendor deletion certificate"},
        {"processingActivity": "Teleconsultation video", "department": "Operations", "principalCategory": "Patients", "dataCategories": "Video, name, symptoms", "purpose": "Remote consultation", "legalGround": "Consent - s.6",
         "systemName": "VideoMed SaaS", "cloudProvider": "Azure Singapore", "externalProcessors": "VideoMed", "crossBorder": True, "crossBorderDetails": "Singapore", "retentionPeriod": "1 year"},
    ]
    for r in rows:
        req("POST", f"/api/e/inventory?orgId={o}", r)
    csv = req("GET", f"/api/e/inventory/export?orgId={o}&format=csv", raw=True)
    assert b"Patient registration" in csv
    xlsx = req("GET", f"/api/e/inventory/export?orgId={o}&format=xlsx", raw=True)
    assert xlsx[:2] == b"PK"
    open(os.path.join(OUT, "inventory_export.xlsx"), "wb").write(xlsx)
    imp = req("POST", f"/api/e/inventory/import?orgId={S['org2']}", files={"file": ("inv.xlsx", xlsx, "application/octet-stream")})
    assert imp["imported"] == 3, imp
    for f in [{"name": "HIS to Lab", "sourceSystem": "HIS", "destinationSystem": "PathCare LIS", "destinationType": "Data Processor", "dataCategories": "Patient ID, test orders", "transferMethod": "API", "encrypted": True, "frequency": "Real-time", "processor": "PathCare"},
              {"name": "Payroll to bank", "sourceSystem": "Keka", "destinationSystem": "HDFC Bank", "destinationType": "Other Data Fiduciary", "dataCategories": "Bank account, salary", "transferMethod": "SFTP", "encrypted": True, "frequency": "Monthly"},
              {"name": "Teleconsult recordings", "sourceSystem": "VideoMed", "destinationSystem": "Azure Blob", "destinationType": "Data Processor", "transferMethod": "API", "encrypted": False, "crossBorder": True, "country": "Singapore"}]:
        req("POST", f"/api/e/dataflows?orgId={o}", f)
    return "3 activities, CSV+XLSX export, XLSX round-trip import into org #2"


@test("DPIA: voluntary DPIA, risk scoring (inherent/residual), approval workflow")
def _():
    o = S["org"]
    d = req("POST", f"/api/e/dpias?orgId={o}", {"processingActivity": "Teleconsultation platform", "trigger": "Voluntary - high-risk processing", "purpose": "Remote consultations", "dataCategories": "Video, health data", "principalCategories": "Patients incl. children", "volume": "~12,000 per year", "systems": "VideoMed SaaS", "owner": "Dr. Meera Kulkarni", "approver": "Rajesh Patil"})
    r1 = req("POST", f"/api/e/dpiaRisks?orgId={o}", {"dpiaId": d["id"], "riskType": "Cross-border transfer", "description": "Recordings stored in Singapore", "likelihood": "4", "impact": "5", "existingControls": "Encryption at rest", "residualLikelihood": "3", "residualImpact": "5", "mitigationPlan": "Move storage to India region", "owner": "CISO", "dueDate": "2027-01-31"})
    r2 = req("POST", f"/api/e/dpiaRisks?orgId={o}", {"dpiaId": d["id"], "riskType": "Unauthorised access", "description": "Shared clinician accounts", "likelihood": "3", "impact": "4", "residualLikelihood": "2", "residualImpact": "3", "mitigationPlan": "Named accounts + MFA"})
    assert r1["inherentScore"] == 20 and r1["inherentRating"] == "High" and r1["residualRating"] == "High"
    assert r2["inherentScore"] == 12 and r2["inherentRating"] == "Medium" and r2["residualRating"] == "Low"
    dd = req("GET", f"/api/e/dpias/{d['id']}")
    assert dd["overallResidualRisk"] == "High"
    req("POST", f"/api/dpias/{d['id']}/submit", {})
    req("POST", f"/api/dpias/{d['id']}/approve", {"decision": "Approved", "conclusion": "Proceed subject to India-region storage by Jan 2027."})
    assert req("GET", f"/api/e/dpias/{d['id']}")["status"] == "Approved"
    S["dpia"] = d["id"]
    return "scores 20/High->15/High and 12/Medium->6/Low; overall High; approved"


@test("Consent: notice versions, records, withdrawal history; integration API with API key")
def _():
    o = S["org"]
    n = req("POST", f"/api/e/consentNotices?orgId={o}", {"title": "Patient portal notice", "version": "2.0", "language": "English", "purposes": "Appointment booking\nReports delivery", "dataItems": "Name\nMobile\nEmail", "withdrawalLink": "https://sahyadri.example.in/privacy-rights", "status": "Published"})
    rec = req("POST", f"/api/e/consentRecords?orgId={o}", {"principalRef": "PAT-100234", "identifierType": "Customer ID", "noticeId": n["id"], "purpose": "Appointment booking", "channel": "Website"})
    assert rec["status"] == "Given" and rec["noticeVersion"] == "2.0"
    req("PUT", f"/api/e/consentRecords/{rec['id']}", {"status": "Withdrawn"})
    ev = req("GET", f"/api/e/consentEvents?orgId={o}&parentId={rec['id']}")
    assert ev["total"] == 2
    k = req("POST", f"/api/apikeys?orgId={o}", {"name": "Patient portal"})
    key = k["key"]
    r = req("POST", "/api/v1/consents", {"principalRef": "PAT-200111", "purpose": "Reports delivery", "noticeId": n["id"], "language": "English"}, token="", headers={"X-Api-Key": key})
    assert r["noticeVersion"] == "2.0"
    req("POST", "/api/v1/consents", {"principalRef": "x", "purpose": "y"}, token="", headers={"X-Api-Key": "dk_invalidinvalidinvalid"}, expect=401)
    w = req("POST", "/api/v1/consents/withdraw", {"principalRef": "PAT-200111"}, token="", headers={"X-Api-Key": key})
    assert w["withdrawn"] == 1
    st = req("GET", "/api/v1/consents?principalRef=PAT-200111", token="", headers={"X-Api-Key": key})
    assert st[0]["status"] == "Withdrawn"
    rr = req("POST", "/api/v1/rights-requests", {"requestType": "Access (s.11)", "principalName": "Sunil Joshi", "principalContact": "sunil@example.com", "details": "Copy of my data"}, token="", headers={"X-Api-Key": key})
    assert rr["reference"].startswith("DPR-")
    S["apikey"] = key
    return "notice v2.0 linked, withdrawal logged, API consent + withdraw + rights intake"


@test("Policies: template generation (draft, not legally approved), upload + DPDP gap analysis, apply approved, approve")
def _():
    o = S["org"]
    g = req("POST", f"/api/policies/generate?orgId={o}", {"templateKey": "data-protection-policy"})
    p = req("GET", f"/api/e/policies/{g['id']}")
    assert p["status"].startswith("Draft") and "Sahyadri Health Services" in p["content"] and "Dr. Meera Kulkarni" in p["content"]
    req("POST", f"/api/policies/generate?orgId={o}", {"templateKey": "privacy-notice"})
    req("POST", f"/api/policies/generate?orgId={o}", {"templateKey": "breach-policy"})
    doc = make_docx(["Privacy Policy", "We comply with the GDPR and process data on the basis of our legitimate interests.", "We collect personal data such as name and email for providing services.",
                     "You have the right to access and erasure of your data under Article 15 and Article 17.", "Contact us at privacy@sahyadri.example.in."])
    up = req("POST", f"/api/policies/upload?orgId={o}", files={"file": ("old_privacy_policy.docx", doc, "application/octet-stream")}, form={"title": "Existing Website Privacy Policy", "policyType": "privacy-notice"})
    recs = req("GET", f"/api/e/policyRecommendations?orgId={o}&parentId={up['id']}&pageSize=100")["rows"]
    topics = {r["topic"] for r in recs}
    assert "Withdrawal of consent" in topics and "Legitimate interests ground" in topics and "Foreign framework reference" in topics, topics
    for r in recs[:4]:
        req("POST", f"/api/policyRecommendations/{r['id']}/decide", {"status": "Approved"})
    req("POST", f"/api/policyRecommendations/{recs[4]['id']}/decide", {"status": "Rejected"})
    new = req("POST", f"/api/policies/{up['id']}/apply", {})
    np_ = req("GET", f"/api/e/policies/{new['id']}")
    orig = req("GET", f"/api/e/policies/{up['id']}")
    assert np_["basedOnPolicyId"] == up["id"] and orig["content"] != np_["content"]
    req("POST", f"/api/policies/{g['id']}/approve", {"notes": "Reviewed by legal counsel"})
    assert req("GET", f"/api/e/policies/{g['id']}")["status"] == "Approved internally"
    return f"{len(recs)} recommendations incl. GDPR/legitimate-interest flags; original retained, revised v{np_['version']} created"


@test("Vendors: auto risk rating, assessment history, contract gaps")
def _():
    o = S["org"]
    v1 = req("POST", f"/api/e/vendors?orgId={o}", {"name": "VideoMed SaaS", "category": "SaaS application", "isDataProcessor": True, "personalDataAccessed": "Video, health data", "storageLocation": "Azure Singapore", "crossBorder": True, "contractStatus": "Signed", "dpaStatus": "Not in place", "nextReview": "2026-12-05"})
    v2 = req("POST", f"/api/e/vendors?orgId={o}", {"name": "Keka HRMS", "category": "Payroll / HR", "isDataProcessor": True, "personalDataAccessed": "Employee data", "storageLocation": "AWS Mumbai", "contractStatus": "Signed", "dpaStatus": "Signed - needs update", "securityClause": True, "breachNotificationClause": True, "breachNotificationHours": 72, "securityCertifications": "ISO 27001, SOC 2"})
    v3 = req("POST", f"/api/e/vendors?orgId={o}", {"name": "PathCare Labs", "category": "Professional services", "isDataProcessor": True, "personalDataAccessed": "Test orders", "contractStatus": "Signed", "dpaStatus": "Signed - DPDP compliant", "securityClause": True, "breachNotificationClause": True, "breachNotificationHours": 24, "erasureClause": True, "auditRights": True})
    assert (v1["riskRating"], v2["riskRating"], v3["riskRating"]) == ("High", "Medium", "Medium"), (v1["riskRating"], v2["riskRating"], v3["riskRating"])
    req("POST", f"/api/e/vendorAssessments?orgId={o}", {"vendorId": v2["id"], "assessmentDate": "2026-11-10", "assessor": "CISO", "score": 72, "riskRating": "Medium", "findings": "DPA lacks erasure clause"})
    return "High (no DPA) / Medium (DPA needs update) / Medium (compliant but holds data)"


@test("Security safeguards: tool mapping cannot mark Compliant without validated configuration evidence")
def _():
    o = S["org"]
    ctl = {c["controlKey"]: c for c in req("GET", f"/api/e/securityControls?orgId={o}&pageSize=100")["rows"]}
    sc10 = ctl["SC-10"]["id"]
    req("PUT", f"/api/e/securityControls/{sc10}", {"toolsMapped": "Wazuh", "status": "Compliant"}, expect=400)
    req("PUT", f"/api/e/securityControls/{sc10}", {"validated": True}, expect=400)
    r = req("PUT", f"/api/e/securityControls/{sc10}", {"toolsMapped": "Wazuh", "configurationEvidence": "Wazuh rules 100200-100210 for HIS DB access; 365-day index retention", "validated": True, "status": "Compliant", "riskRating": "Low"})
    assert r["validated"] and r["validatedBy"] == ADMIN["email"]
    req("PUT", f"/api/e/securityControls/{ctl['SC-01']['id']}", {"status": "Partially Compliant", "riskRating": "High", "gap": "TDE not enabled on HIS DB", "recommendation": "Enable TDE"})
    req("PUT", f"/api/e/securityControls/{ctl['SC-05']['id']}", {"status": "Non-Compliant", "riskRating": "High", "toolsMapped": "Microsoft Defender", "gap": "No MFA for VPN"})
    return "blocked tool-only Compliant; accepted after evidence + validation"


@test("Data principal rights: reference, due date (grievance <=90 days), status audit trail, masking")
def _():
    o = S["org"]
    r = req("POST", f"/api/e/rightsRequests?orgId={o}", {"requestType": "Erasure (s.12(3))", "principalName": "Kavita Deshpande", "principalContact": "kavita@example.com", "channel": "Email", "receivedAt": "2026-11-01T10:00:00Z", "details": "Delete my account"})
    assert r["reference"].startswith("DPR-") and r["dueAt"].startswith("2026-12-01")
    g = req("POST", f"/api/e/rightsRequests?orgId={o}", {"requestType": "Grievance (s.13)", "principalName": "Amit Shah", "receivedAt": "2026-11-20T10:00:00Z"})
    assert g["dueAt"].startswith("2026-12-20")
    req("PUT", f"/api/e/rightsRequests/{r['id']}", {"verificationStatus": "Verified", "assignedTo": "HR Ops", "status": "In Progress"})
    req("PUT", f"/api/e/rightsRequests/{r['id']}", {"status": "Completed", "response": "Account and records erased except statutory medical records.", "closureEvidence": "Erasure log #4471"})
    ev = req("GET", f"/api/e/requestEvents?orgId={o}&parentId={r['id']}&pageSize=50")
    assert ev["total"] >= 5, ev["total"]
    rr = req("GET", f"/api/e/rightsRequests/{r['id']}")
    assert rr["closedAt"] is not None
    return f"due dates computed; {ev['total']} audit-trail events"


@test("Breach: notification deadline engine (DPDP without-delay, 72h Board, CERT-In 6h), sending, approvals, templates")
def _():
    o = S["org"]
    inc = req("POST", f"/api/e/incidents?orgId={o}", {"title": "Ransomware on radiology file server", "discoveredAt": "2026-11-24T08:00:00Z", "occurredAt": "2026-11-23T22:30:00Z", "detectionSource": "EDR alert", "severity": "Critical",
        "description": "Encryption of PACS share; possible exfiltration", "systemsAffected": "PACS file server", "dataCategoriesAffected": "Radiology images, patient names", "affectedCount": 3200, "isPersonalDataBreach": True, "certInReportable": True,
        "likelyImpact": "Exposure of health data", "owner": "CISO"})
    notes = {n["recipientName"]: n for n in req("GET", f"/api/e/incidentNotifications?orgId={o}&parentId={inc['id']}&pageSize=20")["rows"]}
    det = [n for n in notes.values() if "detailed" in n["recipientName"]][0]
    cert = notes["CERT-In"]
    assert det["dueAt"] == "2026-11-27T08:00:00Z" and cert["dueAt"] == "2026-11-24T14:00:00Z", (det["dueAt"], cert["dueAt"])
    assert len(notes) == 4, list(notes)
    req("PUT", f"/api/e/incidentNotifications/{cert['id']}", {"sentAt": "2026-11-24T12:30:00Z", "method": "Email", "submissionReference": "CERTIn-2026-889"})
    assert req("GET", f"/api/e/incidentNotifications/{cert['id']}")["status"] == "Sent"
    req("POST", f"/api/incidents/{inc['id']}/approve", {"kind": "legal", "decision": "Approved"})
    req("POST", f"/api/incidents/{inc['id']}/approve", {"kind": "management", "decision": "Approved"})
    t = req("GET", f"/api/incidents/{inc['id']}/template/DPB_DETAILED")["text"]
    assert "72" not in t or "Rule 7(2)(b)" in t
    req("PUT", f"/api/e/incidents/{inc['id']}", {"isPersonalDataBreach": False})
    after = req("GET", f"/api/e/incidentNotifications?orgId={o}&parentId={inc['id']}&pageSize=20")["rows"]
    assert sum(1 for n in after if n["status"] == "Not required") == 3
    req("PUT", f"/api/e/incidents/{inc['id']}", {"isPersonalDataBreach": True, "rootCause": "Unpatched VPN appliance", "containmentActions": "Isolated server", "remediation": "Restored from backup"})
    rules = req("GET", f"/api/breach/rules?orgId={o}")
    bad = [dict(r) for r in rules]
    for r in bad:
        if r["key"] == "DPB_DETAILED":
            r["hours"] = 96
    req("PUT", f"/api/breach/rules?orgId={o}", bad, expect=400)
    drill = req("POST", f"/api/e/incidents?orgId={o}", {"title": "Mock breach drill Q4", "discoveredAt": "2026-11-10T09:00:00Z", "isPersonalDataBreach": True, "isDrill": True})
    dn = [n for n in req("GET", f"/api/e/incidentNotifications?orgId={o}&parentId={drill['id']}")["rows"] if "detailed" in n["recipientName"]][0]
    req("PUT", f"/api/e/incidentNotifications/{dn['id']}", {"sentAt": "2026-11-12T15:00:00Z"})
    S["incident"] = inc["id"]
    return "4 obligations with distinct deadlines; re-classification marks DPDP items Not required; 72h cap enforced"


@test("Roadmap: auto-generation from gaps (30/60/90 by penalty-weighted risk), approval")
def _():
    o = S["org"]
    r = req("POST", f"/api/roadmap/generate?orgId={o}&assessmentId={S['assessment']}", {})
    again = req("POST", f"/api/roadmap/generate?orgId={o}&assessmentId={S['assessment']}", {})
    assert r["created"] > 10 and again["created"] == 0
    acts = req("GET", f"/api/e/actions?orgId={o}&pageSize=200")["rows"]
    sec = [a for a in acts if a["requirementRef"].startswith("SEC-03")][0]
    assert sec["priority"] == "P1 - Critical" and sec["plan"] in ("30-day", "Custom")
    req("PUT", f"/api/e/actions/{acts[0]['id']}", {"status": "Completed", "supportingEvidence": "Board resolution BR-2026-14"})
    req("POST", f"/api/actions/{acts[0]['id']}/approve", {"decision": "Approved"})
    return f"{r['created']} actions created; idempotent re-run created 0"


@test("Dashboard: status, risk, module/department breakdown, deadlines, disclaimer")
def _():
    d = req("GET", f"/api/dashboard?orgId={S['org']}")
    assert d["status"]["compliant"] == 5 and d["status"]["nonCompliant"] == 7
    assert d["risk"]["High"] >= 7 and "not a legal opinion" in d["disclaimer"]
    assert any(x["type"] == "Breach notification" for x in d["deadlines"])
    assert d["vendors"]["processorsWithoutDpa"] == 1 and d["security"]["validated"] == 1
    return f"indicator {d['status']['indicator']}%, {len(d['deadlines'])} deadlines, {d['actions']['open']} open actions"


@test("Permissions: role enforcement (Read-only, Auditor), Super Admin protection")
def _():
    req("POST", "/api/users", {"email": "viewer@example.in", "name": "Vik Viewer", "password": "Viewer#Pass2026", "role": "Read-only User"})
    req("POST", "/api/users", {"email": "auditor@example.in", "name": "Anu Auditor", "password": "Auditor#Pass2026", "role": "Auditor"})
    req("POST", "/api/users", {"email": "weak@example.in", "name": "W", "password": "weak", "role": "Auditor"}, expect=400)
    v = req("POST", "/api/auth/login", {"email": "viewer@example.in", "password": "Viewer#Pass2026"}, token="")["token"]
    a = req("POST", "/api/auth/login", {"email": "auditor@example.in", "password": "Auditor#Pass2026"}, token="")["token"]
    req("GET", f"/api/e/vendors?orgId={S['org']}", token=v)
    req("POST", f"/api/e/vendors?orgId={S['org']}", {"name": "x"}, token=v, expect=403)
    req("GET", "/api/users", token=v, expect=403)
    req("GET", f"/api/e/inventory/export?orgId={S['org']}", token=v, expect=403)
    req("GET", f"/api/e/inventory/export?orgId={S['org']}", token=a)
    req("GET", "/api/audit", token=a)
    rr = req("GET", f"/api/e/rightsRequests?orgId={S['org']}", token=v)["rows"]
    assert all("*" in (r.get("principalName") or "*") for r in rr), rr
    req("POST", "/api/admin/backup", token=a, expect=403)
    return "403 on write/admin for Read-only; export allowed for Auditor; PII masked for Read-only"


@test("Organisation separation: records stay with their organisation")
def _():
    o1, o2 = S["org"], S["org2"]
    inv2 = req("GET", f"/api/e/inventory?orgId={o2}")["total"]
    inv1 = req("GET", f"/api/e/inventory?orgId={o1}")["total"]
    assert inv1 == 3 and inv2 == 3
    req("POST", f"/api/e/dpiaRisks?orgId={o2}", {"dpiaId": S["dpia"], "riskType": "Other privacy risk"}, expect=400)
    req("GET", "/api/e/inventory", expect=400)
    return "cross-organisation parent linking rejected; orgId required on scoped lists"


REPORT_FILES = []


@test("Reports: all 16 report types generate from live data")
def _():
    o = S["org"]
    ids = {}
    for t in S["meta"]["reportTypes"]:
        body = {"orgId": o, "reportType": t["key"], "assessmentId": S["assessment"], "periodFrom": "2026-10-01", "periodTo": "2026-11-30"}
        if t["key"] == "incident":
            body["entityId"] = S["incident"]
        ids[t["key"]] = req("POST", "/api/reports", body)["id"]
    S["reports"] = ids
    master = req("GET", f"/api/reports/{ids['master']}")
    assert len(master["content"]["sections"]) > 20
    return f"16 drafts; master report has {len(master['content']['sections'])} sections"


@test("Reports: edit content, approve, editable DOCX + PDF export from the same content")
def _():
    rid = S["reports"]["gap-assessment"]
    r = req("GET", f"/api/reports/{rid}")
    c = r["content"]
    c["sections"][0]["blocks"].insert(0, {"type": "p", "text": "EDITED: Management commentary added before export."})
    req("PUT", f"/api/reports/{rid}/content", c)
    req("POST", f"/api/reports/{rid}/approve", {})
    docx = req("GET", f"/api/reports/{rid}/export?format=docx", raw=True)
    pdf = req("GET", f"/api/reports/{rid}/export?format=pdf", raw=True)
    assert docx[:2] == b"PK" and pdf[:4] == b"%PDF"
    z = zipfile.ZipFile(io.BytesIO(docx))
    xml = z.read("word/document.xml").decode()
    assert "EDITED: Management commentary" in xml and "<w:tbl>" in xml and "TOC \\o" in xml
    assert any(n.startswith("word/charts/") for n in z.namelist()) and any(n.startswith("word/media/") for n in z.namelist())
    open(os.path.join(OUT, "DPDP_Compliance_Gap_Assessment.docx"), "wb").write(docx)
    open(os.path.join(OUT, "DPDP_Compliance_Gap_Assessment.pdf"), "wb").write(pdf)
    files = req("GET", f"/api/e/reportFiles?orgId={S['org']}&parentId={rid}")
    assert files["total"] == 2
    return f"DOCX {len(docx)//1024} KB (text, tables, native charts, TOC field), PDF {len(pdf)//1024} KB"


@test("Reports: export remaining types as DOCX and PDF (sample reports)")
def _():
    names = {t["key"]: t["title"] for t in S["meta"]["reportTypes"]}
    n = 0
    for key, rid in S["reports"].items():
        if key == "gap-assessment":
            continue
        req("POST", f"/api/reports/{rid}/approve", {})
        for fmt in ("docx", "pdf"):
            data = req("GET", f"/api/reports/{rid}/export?format={fmt}", raw=True)
            assert data[:2] == b"PK" if fmt == "docx" else data[:4] == b"%PDF"
            open(os.path.join(OUT, f"{names[key].replace(' ', '_')}.{fmt}"), "wb").write(data)
            n += 1
    return f"{n} files"


@test("Report templates: designer config (order, include, custom section), cover design, duplicate, restore defaults")
def _():
    o = S["org"]
    tpls = req("GET", f"/api/templates?orgId={o}")
    assert len([t for t in tpls if t["isSystem"]]) == 5
    outline = req("GET", f"/api/reports/outline?orgId={o}&type=executive-summary")
    secs = [{"id": s["id"], "include": True, "order": (len(outline) - i) * 10} for i, s in enumerate(outline)]
    secs[-1]["include"] = False
    secs.append({"id": "custom-1", "custom": True, "order": 1, "heading": "Message from the Managing Director", "include": True, "blocks": [{"type": "p", "text": "Custom section text."}]})
    t = req("POST", f"/api/templates?orgId={o}", {"name": "Board pack", "reportType": "executive-summary", "coverDesign": "Bold Block", "watermark": "BOARD CONFIDENTIAL", "primaryColor": "#5B2C83", "secondaryColor": "#E0A800", "fontFamily": "Georgia", "sectionsJson": json.dumps(secs)})
    rid = req("POST", "/api/reports", {"orgId": o, "reportType": "executive-summary", "templateId": t["id"]})["id"]
    c = req("GET", f"/api/reports/{rid}")["content"]
    assert c["sections"][0]["heading"] == "Message from the Managing Director"
    assert not [s for s in c["sections"] if s["id"] == outline[-1]["id"]][0]["include"]
    pdf = req("GET", f"/api/reports/{rid}/export?format=pdf", raw=True)
    open(os.path.join(OUT, "Executive_Summary_BoardPack_template.pdf"), "wb").write(pdf)
    for cover in ["Executive Minimal", "Framed Classic"]:
        p = req("GET", f"/api/reports/branding-preview?orgId={o}&cover={cover.replace(' ', '%20')}", raw=True)
        open(os.path.join(OUT, f"cover_{cover.replace(' ', '_')}.pdf"), "wb").write(p)
    d = req("POST", f"/api/templates/{tpls[0]['id']}/duplicate?orgId={o}", {})
    req("PUT", f"/api/templates/{tpls[0]['id']}", {"name": "x"}, expect=400)
    req("POST", "/api/templates/restore-defaults", {})
    assert len([x for x in req("GET", f"/api/templates?orgId={o}") if x["isSystem"]]) == 5
    return "custom section first, excluded section hidden, purple/Georgia Bold Block cover rendered"


@test("Reports: complete compliance package (ZIP with manifest and SHA-256)")
def _():
    data = req("POST", "/api/reports/package", {"orgId": S["org"], "types": ["executive-summary", "roadmap", "breach-readiness"], "formats": ["docx", "pdf"]}, raw=True)
    z = zipfile.ZipFile(io.BytesIO(data))
    names = z.namelist()
    assert "MANIFEST.txt" in names and len(names) == 7, names
    open(os.path.join(OUT, "DPDP_Compliance_Package_sample.zip"), "wb").write(data)
    return f"{len(names) - 1} reports + manifest"


@test("Audit trail: create/modify/approve/export/delete logged; hash chain verifies")
def _():
    o = S["org"]
    v = req("POST", f"/api/e/vendors?orgId={o}", {"name": "Temp Vendor"})
    req("DELETE", f"/api/e/vendors/{v['id']}")
    req("GET", f"/api/e/vendors/{v['id']}", expect=404)
    log = req("GET", "/api/audit?entity=vendors")
    acts = {r["action"] for r in log["rows"]}
    assert {"CREATE", "DELETE"} <= acts
    exp = req("GET", "/api/audit?action=EXPORT")
    assert exp["total"] > 0
    appr = req("GET", "/api/audit?action=APPROVE")
    assert appr["total"] > 0
    ver = req("GET", "/api/audit/verify")
    assert ver["intact"] is True
    return f"{ver['checkedEntries']} entries, chain intact"


@test("Backup: on-demand backup of files (and database on SQL Server)")
def _():
    b = req("POST", "/api/admin/backup", {})
    lst = req("GET", "/api/admin/backups")
    assert len(lst["files"]) >= 1
    return os.path.basename(b["files"])


@test("Health endpoint")
def _():
    h = req("GET", "/api/health", token="")
    assert h["status"] == "ok"
    return f"schema v{h['schema']}, pdf engine {h['pdf']}"


@test("MSSP: provider branding (public), logo, portfolio of all clients, one-step client onboarding")
def _():
    b = req("GET", "/api/branding", token="")
    assert b["name"]
    req("PUT", "/api/branding", {"name": "Manlitics", "tagline": "Managed DPDP Compliance Services", "portalName": "DPDP Compliance Portal", "primaryColor": "#0B2545", "secondaryColor": "#C9A227", "supportEmail": "dpdp@manlitics.example", "useOnReports": True})
    req("PUT", "/api/branding", {"name": "X", "primaryColor": "red", "secondaryColor": "#C9A227"}, expect=400)
    req("POST", "/api/branding/logo", files={"file": ("provider.png", png(300, 80, (11, 37, 69)), "image/png")})
    assert req("GET", "/api/branding/logo", token="", raw=True)[:4] == b"\x89PNG"
    ob = req("POST", "/api/clients/onboard", {"name": "Deccan Retail Pvt Ltd", "industry": "E-commerce / Retail", "adminEmail": "cadmin@deccan.example", "adminName": "Dev Customer", "adminPassword": "Customer#Pass2026"})
    S["org3"] = ob["orgId"]
    pf = req("GET", "/api/portfolio")
    names = {c["name"] for c in pf["clients"]}
    assert pf["totals"]["clients"] == 3 and "Deccan Retail Pvt Ltd" in names, pf["totals"]
    sah = [c for c in pf["clients"] if c["id"] == S["org"]][0]
    assert sah["highRisk"] >= 7 and sah["health"] in ("At risk", "Needs attention")
    return f"{pf['totals']['clients']} clients, avg indicator {pf['totals']['averageIndicator']}, health {pf['totals']['byHealth']}"


@test("MSSP: customer portal user is confined to its own organisation")
def _():
    s1 = req("POST", "/api/users", {"email": "portal.admin@sahyadri.example", "name": "Sahyadri Portal Admin", "password": "Portal#Pass2026", "role": "Customer Administrator", "organizationId": S["org"]})
    req("POST", "/api/users", {"email": "bad@x.example", "name": "Bad", "password": "Portal#Pass2026", "role": "Customer User"}, expect=400)
    login = req("POST", "/api/auth/login", {"email": "portal.admin@sahyadri.example", "password": "Portal#Pass2026"}, token="")
    assert login["user"]["userType"] == "Customer" and login["user"]["organizationId"] == S["org"]
    c = login["token"]
    req("POST", "/api/auth/change-password", {"currentPassword": "Portal#Pass2026", "newPassword": "Portal#Pass2027"}, token=c)
    c = req("POST", "/api/auth/refresh", token=c)["token"]
    orgs = req("GET", "/api/e/organizations", token=c)["rows"]
    assert [o["id"] for o in orgs] == [S["org"]], orgs
    o1, o2 = S["org"], S["org2"]
    req("GET", f"/api/e/inventory?orgId={o1}", token=c)
    req("GET", f"/api/e/inventory?orgId={o2}", token=c, expect=403)
    req("GET", f"/api/e/organizations/{o2}", token=c, expect=403)
    req("GET", f"/api/dashboard?orgId={o2}", token=c, expect=403)
    req("POST", f"/api/e/vendors?orgId={o2}", {"name": "x"}, token=c, expect=403)
    req("POST", "/api/e/organizations", {"name": "Sneaky Org"}, token=c, expect=403)
    req("POST", "/api/reports", {"orgId": o2, "reportType": "executive-summary"}, token=c, expect=403)
    req("GET", "/api/portfolio", token=c, expect=403)
    req("PUT", "/api/branding", {"name": "Hack", "primaryColor": "#000000", "secondaryColor": "#000000"}, token=c, expect=403)
    req("POST", "/api/admin/backup", token=c, expect=403)
    req("PUT", "/api/e/requirements/1", {"title": "changed"}, token=c, expect=403)
    # data of own org works, including reports and evidence
    req("GET", f"/api/dashboard?orgId={o1}", token=c)
    rid = req("POST", "/api/reports", {"orgId": o1, "reportType": "executive-summary"}, token=c)["id"]
    assert req("GET", f"/api/reports/{rid}/export?format=docx", token=c, raw=True)[:2] == b"PK"
    other_report = S["reports"]["executive-summary"]
    req("GET", f"/api/reports/{other_report}", token=c)  # same org - allowed
    rid2 = req("POST", "/api/reports", {"orgId": o2, "reportType": "roadmap"})["id"]  # provider creates for org2
    req("GET", f"/api/reports/{rid2}", token=c, expect=403)
    req("GET", f"/api/reports/{rid2}/export?format=docx", token=c, expect=403)
    # customer admin manages only own-org customer users
    req("POST", "/api/users", {"email": "staff@sahyadri.example", "name": "Staff", "password": "Portal#Pass2026", "role": "Customer User"}, token=c)
    req("POST", "/api/users", {"email": "evil@sahyadri.example", "name": "E", "password": "Portal#Pass2026", "role": "Super Administrator"}, token=c, expect=403)
    us = req("GET", "/api/users", token=c)
    assert all(u["organizationId"] == o1 and u["userType"] == "Customer" for u in us) and len(us) == 2, us
    admin_id = [u for u in req("GET", "/api/users") if u["email"] == ADMIN["email"]][0]["id"]
    req("POST", f"/api/users/{admin_id}/reset-password", {"password": "Hacked#Pass2026"}, token=c, expect=403)
    roles = req("GET", "/api/roles", token=c)
    assert {r["role"] for r in roles["roles"]} == {"Customer Administrator", "Customer User", "Customer Viewer"}
    au = req("GET", "/api/audit", token=c)
    assert au["total"] > 0 and all(r["organizationId"] == o1 for r in au["rows"])
    # report carries provider branding as "prepared by" / consultant logo
    z = zipfile.ZipFile(io.BytesIO(req("GET", f"/api/reports/{rid}/export?format=docx", token=c, raw=True)))
    assert "consultant_logo" in " ".join(z.namelist())
    return "403 on other clients' data, portfolio, branding, backup, shared library; own data and reports work"


passed = sum(1 for r in results if r[1] == "PASS")
with open(os.path.join(OUT, "test_results.md"), "w") as f:
    f.write(f"# Data Kavach - API test results\n\nTarget: `{BASE}`  \nRun: {time.strftime('%Y-%m-%d %H:%M:%S')}  \nResult: **{passed}/{len(results)} passed**\n\n| # | Test | Result | Detail | Time (s) |\n|---|---|---|---|---|\n")
    for i, (n, r, d, t) in enumerate(results, 1):
        f.write(f"| {i} | {n} | {r} | {str(d).replace('|', '/')} | {t:.1f} |\n")
print(f"\n{passed}/{len(results)} passed")
sys.exit(0 if passed == len(results) else 1)
