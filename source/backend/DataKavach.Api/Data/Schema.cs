namespace DataKavach.Data;

public enum ColType { Str, Text, Int, Long, Dec, Bool, Date, DateTime }

/// <summary>Column definition. Drives DDL generation, validation, API metadata and UI forms.</summary>
public sealed record Col(string Name, ColType Type, int Len = 0)
{
    public bool Required { get; init; }
    public string? Label { get; init; }
    public string[]? Options { get; init; }
    public bool Hidden { get; init; }          // not shown in forms (system managed)
    public bool ReadOnly { get; init; }        // shown but not editable via generic CRUD
    public bool Sensitive { get; init; }       // masked for users without pii.view
    public string? Ref { get; init; }          // lookup entity key (e.g. "departments")
    public string? Group { get; init; }        // form section
    public string? Help { get; init; }
    public bool Multi { get; init; }           // multi-select stored as "; " separated text
    public string DisplayLabel => Label ?? Humanize(Name);

    public static string Humanize(string n)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < n.Length; i++)
        {
            if (i > 0 && char.IsUpper(n[i]) && !char.IsUpper(n[i - 1])) sb.Append(' ');
            sb.Append(n[i]);
        }
        return sb.ToString();
    }
}

public sealed class EntityDef
{
    public required string Key { get; init; }          // API key, e.g. "vendors"
    public required string Table { get; init; }
    public required string Label { get; init; }
    public required string Module { get; init; }       // permission module
    public bool OrgScoped { get; init; } = true;
    public string? ParentKey { get; init; }            // parent entity key
    public string? ParentCol { get; init; }            // FK column to parent
    public required List<Col> Cols { get; init; }
    public string[] ListCols { get; init; } = Array.Empty<string>();
    public string[] SearchCols { get; init; } = Array.Empty<string>();
    public string[] ExtraIndexes { get; init; } = Array.Empty<string>();
    public bool GenericWrite { get; init; } = true;    // allow generic POST/PUT/DELETE
    public bool SoftDelete { get; init; } = true;
    public string TitleCol { get; init; } = "Name";
    public Col? Find(string name) => Cols.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}

public static class Opt
{
    public static readonly string[] Status = { "Not Assessed", "Compliant", "Partially Compliant", "Non-Compliant", "Not Applicable" };
    public static readonly string[] Risk = { "High", "Medium", "Low" };
    public static readonly string[] Priority = { "P1 - Critical", "P2 - High", "P3 - Medium", "P4 - Low" };
    public static readonly string[] ActionStatus = { "Open", "In Progress", "Completed", "Deferred", "Cancelled" };
    public static readonly string[] Approval = { "Pending", "Approved", "Rejected" };
    public static readonly string[] Industry = { "IT / ITES", "Manufacturing", "Healthcare / Hospital", "Education", "Banking / Financial Services", "Insurance", "E-commerce / Retail", "Telecom", "Government / PSU", "Media / Entertainment", "Logistics", "Other" };
    public static readonly string[] Confidentiality = { "Public", "Internal", "Confidential", "Strictly Confidential" };
    public static readonly string[] PrincipalCategory = { "Employees", "Customers", "Vendors", "Contractors", "Website Visitors", "Job Applicants", "Patients", "Students", "Children", "Other" };
    public static readonly string[] Ground = { "Consent - s.6", "s.7(a) Voluntarily provided for specified purpose", "s.7(b) State subsidy/benefit/service", "s.7(c) State function / sovereignty / security", "s.7(d) Legal obligation to disclose to State", "s.7(e) Compliance with judgment/order", "s.7(f) Medical emergency", "s.7(g) Epidemic / public health", "s.7(h) Disaster / public order", "s.7(i) Employment purposes", "Exempt - s.17" };
    public static readonly string[] Likelihood = { "1", "2", "3", "4", "5" };
}

public static class Schema
{
    static Col S(string n, int len = 200) => new(n, ColType.Str, len);
    static Col T(string n) => new(n, ColType.Text);
    static Col I(string n) => new(n, ColType.Int);
    static Col L(string n) => new(n, ColType.Long);
    static Col B(string n) => new(n, ColType.Bool);
    static Col D(string n) => new(n, ColType.Date);
    static Col DT(string n) => new(n, ColType.DateTime);
    static Col O(string n, string[] opts, int len = 100) => new(n, ColType.Str, len) { Options = opts };

    public static readonly List<EntityDef> All = new()
    {
        new EntityDef
        {
            Key = "organizations", Table = "Organizations", Label = "Organizations", Module = "org", OrgScoped = false,
            ListCols = new[] { "Name", "Industry", "City", "EmployeeCount", "IsSignificantDataFiduciary" },
            SearchCols = new[] { "Name", "Cin", "Gstin" },
            Cols = new()
            {
                S("Name") with { Required = true, Label = "Organization name", Group = "Profile" },
                S("LegalName") with { Group = "Profile" },
                T("RegisteredAddress") with { Group = "Profile" },
                S("City", 100) with { Group = "Profile" },
                S("State", 100) with { Group = "Profile" },
                S("Cin", 30) with { Label = "Corporate Identification Number (CIN)", Group = "Profile" },
                S("Gstin", 20) with { Label = "GSTIN", Group = "Profile" },
                O("Industry", Opt.Industry) with { Group = "Profile" },
                S("Website") with { Group = "Profile" },
                S("Email") with { Label = "Company email", Group = "Profile" },
                S("Phone", 50) with { Label = "Contact number", Group = "Profile" },
                I("EmployeeCount") with { Label = "Number of employees", Group = "Profile" },
                I("LocationCount") with { Label = "Number of locations", Group = "Profile" },
                S("DpoName") with { Label = "DPO / Privacy Officer name", Group = "Privacy contacts" },
                S("DpoDesignation") with { Label = "DPO designation", Group = "Privacy contacts" },
                S("DpoEmail") with { Label = "DPO email", Group = "Privacy contacts" },
                S("DpoPhone", 50) with { Label = "DPO phone", Group = "Privacy contacts" },
                S("PrivacyContactEmail") with { Label = "Privacy / grievance contact email", Group = "Privacy contacts" },
                S("GrievanceUrl") with { Label = "Rights & grievance page URL", Group = "Privacy contacts" },
                I("GrievanceResponseDays") with { Label = "Published grievance response period (days, max 90)", Group = "Privacy contacts" },
                B("IsSignificantDataFiduciary") with { Label = "Notified as Significant Data Fiduciary", Group = "Applicability", Help = "s.10 - only when notified by the Central Government" },
                B("ProcessesChildrenData") with { Label = "Processes personal data of children (<18)", Group = "Applicability" },
                B("ProcessesGuardianData") with { Label = "Processes data of persons with disability via lawful guardian", Group = "Applicability" },
                B("ReliesOnConsent") with { Label = "Relies on consent for any processing", Group = "Applicability" },
                B("UsesProcessors") with { Label = "Uses Data Processors / vendors", Group = "Applicability" },
                B("CrossBorderTransfers") with { Label = "Transfers personal data outside India", Group = "Applicability" },
                B("UsesConsentManager") with { Label = "Accepts consent via Consent Managers", Group = "Applicability" },
                O("ThirdScheduleClass", new[] { "None", "E-commerce entity (>= 2 crore users)", "Online gaming intermediary (>= 50 lakh users)", "Social media intermediary (>= 2 crore users)" }) with { Label = "Third Schedule class (Rule 8)", Group = "Applicability" },
                B("IsStateInstrumentality") with { Label = "State / instrumentality of State", Group = "Applicability" },
                B("ResearchExemption") with { Label = "Relies on research/archiving/statistical exemption", Group = "Applicability" },
                B("CertInApplicable") with { Label = "CERT-In incident reporting applies", Group = "Applicability" },
                S("PreparedBy") with { Label = "Report prepared by", Group = "Report details" },
                S("PreparedFor") with { Label = "Report prepared for", Group = "Report details" },
                D("AssessmentDate") with { Group = "Report details" },
                D("PeriodFrom") with { Label = "Assessment period from", Group = "Report details" },
                D("PeriodTo") with { Label = "Assessment period to", Group = "Report details" },
                S("ReportReference", 100) with { Label = "Report reference number", Group = "Report details" },
                S("ReportVersion", 30) with { Group = "Report details" },
                O("Confidentiality", Opt.Confidentiality, 50) with { Label = "Confidentiality classification", Group = "Report details" },
                L("LogoFileId") with { Hidden = true },
                L("ConsultantLogoFileId") with { Hidden = true },
                S("ConsultantName") with { Group = "Branding" },
                S("PrimaryColor", 10) with { Group = "Branding" },
                S("SecondaryColor", 10) with { Group = "Branding" },
                O("FontFamily", new[] { "Calibri", "Arial", "Segoe UI", "Cambria", "Georgia", "Times New Roman", "Verdana", "Tahoma" }, 50) with { Group = "Branding" },
                S("HeaderText") with { Label = "Report header text", Group = "Branding" },
                S("FooterText") with { Label = "Report footer text", Group = "Branding" },
                S("Watermark", 60) with { Label = "Watermark text", Group = "Branding" },
                O("CoverDesign", new[] { "Corporate Band", "Executive Minimal", "Bold Block", "Split Panel", "Framed Classic" }, 50) with { Group = "Branding" },
                O("LogoPlacement", new[] { "Header left", "Header right", "Cover only" }, 30) with { Group = "Branding" },
                S("SignatoryName") with { Label = "Signatory name", Group = "Branding" },
                S("SignatoryTitle") with { Label = "Signatory designation", Group = "Branding" },
                S("ReviewerName") with { Label = "Reviewer name", Group = "Branding" },
                S("ReviewerTitle") with { Label = "Reviewer designation", Group = "Branding" },
            }
        },
        new EntityDef
        {
            Key = "departments", Table = "Departments", Label = "Departments", Module = "org",
            ListCols = new[] { "Name", "HeadName", "HeadEmail" },
            Cols = new() { S("Name") with { Required = true }, S("HeadName") with { Label = "Department head" }, S("HeadEmail") }
        },
        new EntityDef
        {
            Key = "requirements", Table = "Requirements", Label = "Requirement Library", Module = "assessment", OrgScoped = false, TitleCol = "Title",
            ListCols = new[] { "Code", "Module", "Title", "LegalReference", "Applicability", "DefaultRisk", "EffectiveFrom" },
            SearchCols = new[] { "Code", "Title", "Module", "LegalReference" },
            ExtraIndexes = new[] { "Code" },
            Cols = new()
            {
                S("Code", 20) with { Required = true, Label = "Requirement ID" }, S("Module", 100) with { Required = true }, S("Title", 300) with { Required = true },
                S("ActSection", 100) with { Label = "Act section" }, S("Rule", 100) with { Label = "Rule" }, S("LegalReference", 300),
                T("Description") with { Label = "Requirement description" }, T("Question") with { Label = "Assessment question" }, T("Guidance") with { Label = "Implementation guidance" },
                T("EvidenceRequired") with { Label = "Evidence requirement" },
                O("Applicability", new[] { "ALL", "CONSENT", "CHILDREN", "DISABILITY_GUARDIAN", "PROCESSOR", "CROSS_BORDER", "THIRD_SCHEDULE", "CONSENT_MANAGER", "SDF", "STATE", "RESEARCH" }, 40),
                D("EffectiveFrom") with { Label = "Obligation in force from" }, O("DefaultRisk", Opt.Risk, 20), S("PenaltyReference", 200), B("IsActive"), I("SortOrder") with { Hidden = true }
            }
        },
        new EntityDef
        {
            Key = "assessments", Table = "Assessments", Label = "Assessments", Module = "assessment",
            ListCols = new[] { "Name", "PeriodFrom", "PeriodTo", "Status", "Owner" },
            Cols = new() { S("Name") with { Required = true }, D("PeriodFrom"), D("PeriodTo"), O("Status", new[] { "In Progress", "Under Review", "Completed" }, 30), S("Owner"), T("Scope"), T("Notes") }
        },
        new EntityDef
        {
            Key = "assessmentItems", Table = "AssessmentItems", Label = "Assessment Items", Module = "assessment", ParentKey = "assessments", ParentCol = "AssessmentId", TitleCol = "RequirementCode",
            ListCols = new[] { "RequirementCode", "Status", "RiskRating", "Department", "Owner", "TargetDate" },
            ExtraIndexes = new[] { "RequirementId", "Status" },
            Cols = new()
            {
                L("AssessmentId") with { Hidden = true, Required = true }, L("RequirementId") with { Hidden = true, Required = true },
                S("RequirementCode", 20) with { ReadOnly = true },
                O("Status", Opt.Status, 30) with { Label = "Compliance status" }, O("RiskRating", Opt.Risk, 20),
                B("IsApplicable") with { Label = "Applicable" }, S("ApplicabilityNote", 300),
                T("Response") with { Label = "Assessment response / observations" }, T("GapDescription"), T("Recommendation"),
                S("Department", 100) with { Ref = "departments" }, S("Owner", 150) with { Label = "Assigned owner" }, D("TargetDate") with { Label = "Target completion date" },
                S("AssessedBy", 150) with { ReadOnly = true }, DT("AssessedAt") with { ReadOnly = true }
            }
        },
        new EntityDef
        {
            Key = "evidence", Table = "Evidence", Label = "Evidence Repository", Module = "evidence", GenericWrite = false, TitleCol = "Title",
            ListCols = new[] { "Title", "Category", "LinkedEntity", "CurrentVersion", "UpdatedAt" },
            SearchCols = new[] { "Title", "Category", "Description" },
            ExtraIndexes = new[] { "LinkedEntity,LinkedId" },
            Cols = new()
            {
                S("Title", 300) with { Required = true }, T("Description"),
                O("Category", new[] { "Policy document", "Procedure", "System configuration", "Screenshot", "Contract", "Report", "Log extract", "Training record", "Other" }),
                S("LinkedEntity", 50) with { Hidden = true }, L("LinkedId") with { Hidden = true }, I("CurrentVersion") with { ReadOnly = true }
            }
        },
        new EntityDef
        {
            Key = "evidenceVersions", Table = "EvidenceVersions", Label = "Evidence Versions", Module = "evidence", ParentKey = "evidence", ParentCol = "EvidenceId", GenericWrite = false, SoftDelete = false, TitleCol = "FileName",
            ListCols = new[] { "Version", "FileName", "SizeBytes", "Sha256", "CreatedBy", "CreatedAt" },
            Cols = new() { L("EvidenceId") with { Hidden = true }, I("Version"), S("FileName", 260), S("ContentType", 150), L("SizeBytes"), S("Sha256", 64), S("StoragePath", 500) with { Hidden = true }, S("Comment", 500) }
        },
        new EntityDef
        {
            Key = "inventory", Table = "DataInventory", Label = "Personal Data Inventory", Module = "inventory", TitleCol = "ProcessingActivity",
            ListCols = new[] { "ProcessingActivity", "PrincipalCategory", "DataCategories", "LegalGround", "SystemName", "RetentionPeriod", "CrossBorder" },
            SearchCols = new[] { "ProcessingActivity", "DataCategories", "SystemName", "Purpose" },
            Cols = new()
            {
                S("ProcessingActivity", 300) with { Required = true, Group = "Activity" },
                S("Department", 100) with { Ref = "departments", Group = "Activity" },
                S("BusinessOwner", 150) with { Group = "Activity" },
                O("PrincipalCategory", Opt.PrincipalCategory, 300) with { Label = "Data principal categories", Multi = true, Group = "Activity" },
                T("DataCategories") with { Label = "Types of personal data", Group = "Activity" },
                B("IncludesChildrenData") with { Group = "Activity" },
                S("CollectionSource", 300) with { Label = "Data collection source", Group = "Activity" },
                T("Purpose") with { Label = "Processing purpose", Required = true, Group = "Activity" },
                O("LegalGround", Opt.Ground, 120) with { Label = "Legal basis / permitted ground", Group = "Activity" },
                S("ApproxVolume", 100) with { Label = "Approx. number of records", Group = "Activity" },
                S("StorageLocation", 300) with { Label = "Data storage location", Group = "Systems" },
                S("SystemName", 300) with { Label = "Database / application name", Group = "Systems" },
                S("CloudProvider", 150) with { Group = "Systems" },
                S("AccessRoles", 500) with { Label = "Data access roles", Group = "Systems" },
                S("InternalRecipients", 500) with { Group = "Sharing" },
                S("ExternalProcessors", 500) with { Label = "External processors / recipients", Group = "Sharing" },
                B("CrossBorder") with { Label = "Cross-border transfer", Group = "Sharing" },
                S("CrossBorderDetails", 500) with { Label = "Cross-border countries / details", Group = "Sharing" },
                S("RetentionPeriod", 200) with { Group = "Lifecycle" },
                S("ErasureMethod", 300) with { Group = "Lifecycle" },
                T("SecuritySafeguards") with { Group = "Lifecycle" },
                S("LastReviewed", 30) with { Group = "Lifecycle" }
            }
        },
        new EntityDef
        {
            Key = "dataflows", Table = "DataFlows", Label = "Data Flow Register", Module = "inventory",
            ListCols = new[] { "Name", "SourceSystem", "DestinationSystem", "TransferMethod", "Encrypted", "CrossBorder" },
            Cols = new()
            {
                S("Name", 300) with { Required = true, Label = "Flow name" }, S("SourceSystem", 300), S("DestinationSystem", 300),
                O("DestinationType", new[] { "Internal system", "Data Processor", "Other Data Fiduciary", "Government / regulator", "Data Principal" }),
                S("DataCategories", 500), O("TransferMethod", new[] { "API", "SFTP", "Email", "Database link", "Manual upload", "Physical media", "Other" }),
                B("Encrypted") with { Label = "Encrypted in transit" }, B("CrossBorder"), S("Country", 100), O("Frequency", new[] { "Real-time", "Daily", "Weekly", "Monthly", "Ad hoc" }), S("Processor", 200), T("Notes")
            }
        },
        new EntityDef
        {
            Key = "dpias", Table = "Dpias", Label = "Data Protection Impact Assessments", Module = "dpia", TitleCol = "ProcessingActivity",
            ListCols = new[] { "ProcessingActivity", "Trigger", "Status", "OverallResidualRisk", "Owner", "ApprovedAt" },
            Cols = new()
            {
                S("ProcessingActivity", 300) with { Required = true, Group = "Scope" },
                O("Trigger", new[] { "Mandatory - Significant Data Fiduciary (s.10, Rule 13)", "Voluntary - high-risk processing", "Voluntary - new system / change", "Voluntary - children's data", "Other" }, 120) with { Group = "Scope", Help = "DPIA is mandatory only for notified Significant Data Fiduciaries; otherwise it is good practice." },
                T("Purpose") with { Label = "Purpose of processing", Group = "Scope" }, T("RightsDescription") with { Label = "Description of rights of Data Principals affected", Group = "Scope" },
                T("DataCategories") with { Group = "Scope" }, T("PrincipalCategories") with { Label = "Categories of data principals", Group = "Scope" },
                S("Volume", 200) with { Label = "Volume of data", Group = "Scope" }, T("Systems") with { Label = "Processing systems", Group = "Scope" },
                T("Necessity") with { Label = "Necessity and proportionality", Group = "Scope" },
                O("Status", new[] { "Draft", "In Review", "Approved", "Rejected" }, 30) with { ReadOnly = true, Group = "Approval" },
                O("OverallResidualRisk", Opt.Risk, 20) with { ReadOnly = true, Group = "Approval" },
                S("Owner", 150) with { Label = "Assessment owner", Group = "Approval" }, S("Approver", 150) with { Group = "Approval" },
                T("Conclusion") with { Group = "Approval" }, DT("ApprovedAt") with { ReadOnly = true, Group = "Approval" }, S("ApprovedBy", 150) with { ReadOnly = true, Group = "Approval" },
                D("NextReview") with { Group = "Approval" }
            }
        },
        new EntityDef
        {
            Key = "dpiaRisks", Table = "DpiaRisks", Label = "DPIA Risks", Module = "dpia", ParentKey = "dpias", ParentCol = "DpiaId", TitleCol = "RiskType",
            ListCols = new[] { "RiskType", "Description", "InherentScore", "InherentRating", "ResidualScore", "ResidualRating", "Owner" },
            Cols = new()
            {
                L("DpiaId") with { Hidden = true, Required = true },
                O("RiskType", new[] { "Unauthorised access", "Data leakage", "Excessive collection", "Retention beyond purpose", "Vendor / processor risk", "Cross-border transfer", "Inaccurate data", "Children's data", "Rights not honoured", "Other privacy risk" }) with { Required = true },
                T("Description"), O("Likelihood", Opt.Likelihood, 2), O("Impact", Opt.Likelihood, 2),
                I("InherentScore") with { ReadOnly = true }, S("InherentRating", 20) with { ReadOnly = true },
                T("ExistingControls"), O("ResidualLikelihood", Opt.Likelihood, 2), O("ResidualImpact", Opt.Likelihood, 2),
                I("ResidualScore") with { ReadOnly = true }, S("ResidualRating", 20) with { ReadOnly = true },
                T("MitigationPlan"), S("Owner", 150), D("DueDate")
            }
        },
        new EntityDef
        {
            Key = "consentNotices", Table = "ConsentNotices", Label = "Consent Notices", Module = "consent", TitleCol = "Title",
            ListCols = new[] { "Title", "Version", "Language", "Status", "PublishedAt" },
            Cols = new()
            {
                S("Title", 300) with { Required = true }, S("Version", 20) with { Required = true }, O("Language", new[] { "English", "Hindi", "Marathi", "Tamil", "Telugu", "Kannada", "Malayalam", "Bengali", "Gujarati", "Punjabi", "Odia", "Assamese", "Urdu", "Other Eighth Schedule" }, 40),
                T("Purposes") with { Label = "Specified purposes (one per line)" }, T("DataItems") with { Label = "Itemised personal data (one per line)" },
                T("NoticeText"), S("WithdrawalLink", 300) with { Label = "Withdrawal / rights link" }, S("GrievanceContact", 300), S("BoardComplaintInfo", 300) with { Label = "How to complain to the Board" },
                S("CollectionPoint", 300) with { Label = "Collection point (web / app / form)" },
                O("Status", new[] { "Draft", "Published", "Retired" }, 20), DT("PublishedAt")
            }
        },
        new EntityDef
        {
            Key = "consentRecords", Table = "ConsentRecords", Label = "Consent Records", Module = "consent", TitleCol = "PrincipalRef",
            ListCols = new[] { "PrincipalRef", "Purpose", "NoticeVersion", "Status", "GivenAt", "WithdrawnAt", "Channel" },
            SearchCols = new[] { "PrincipalRef", "Purpose" },
            ExtraIndexes = new[] { "PrincipalRef" },
            Cols = new()
            {
                S("PrincipalRef", 200) with { Required = true, Label = "Data principal identifier", Sensitive = true },
                O("IdentifierType", new[] { "Customer ID", "Email", "Mobile", "Employee ID", "Account number", "Other" }, 40),
                L("NoticeId") with { Ref = "consentNotices", Label = "Notice" }, S("NoticeVersion", 20), S("Purpose", 300) with { Required = true }, S("Language", 40),
                O("Status", new[] { "Given", "Withdrawn", "Expired" }, 20), DT("GivenAt"), DT("WithdrawnAt"),
                O("Channel", new[] { "Website", "Mobile app", "Paper form", "Call centre", "Email", "Consent Manager", "API" }, 40),
                S("ConsentManagerId", 100), S("EvidenceRef", 300) with { Label = "Evidence of consent (reference / hash)" }, B("IsParentalConsent"), S("SourceIp", 64)
            }
        },
        new EntityDef
        {
            Key = "consentEvents", Table = "ConsentEvents", Label = "Consent History", Module = "consent", ParentKey = "consentRecords", ParentCol = "ConsentRecordId", GenericWrite = false, SoftDelete = false, TitleCol = "EventType",
            ListCols = new[] { "EventType", "Details", "CreatedBy", "CreatedAt" },
            Cols = new() { L("ConsentRecordId"), S("EventType", 50), T("Details") }
        },
        new EntityDef
        {
            Key = "policies", Table = "Policies", Label = "Policies & Notices", Module = "policy", TitleCol = "Title",
            ListCols = new[] { "Title", "TemplateKey", "Version", "Source", "Status", "ApprovedBy" },
            Cols = new()
            {
                S("Title", 300) with { Required = true }, S("TemplateKey", 60), S("Version", 20),
                O("Source", new[] { "Generated from template", "Uploaded existing policy", "Revised from recommendations" }, 60) with { ReadOnly = true },
                O("Status", new[] { "Draft - not legally approved", "Under Review", "Approved internally", "Superseded" }, 60) with { ReadOnly = true },
                T("Content"), L("BasedOnPolicyId") with { Hidden = true }, S("OriginalFileName", 260) with { ReadOnly = true },
                S("ApprovedBy", 150) with { ReadOnly = true }, DT("ApprovedAt") with { ReadOnly = true }, T("ReviewNotes")
            }
        },
        new EntityDef
        {
            Key = "policyRecommendations", Table = "PolicyRecommendations", Label = "Policy Recommendations", Module = "policy", ParentKey = "policies", ParentCol = "PolicyId", TitleCol = "Topic",
            ListCols = new[] { "Topic", "ExistingClause", "RecommendedModification", "LegalReference", "ApprovalStatus" },
            Cols = new()
            {
                L("PolicyId") with { Hidden = true, Required = true }, S("Topic", 200), T("ExistingClause"), T("RecommendedModification"), T("Reason") with { Label = "Reason for modification" },
                S("LegalReference", 200), T("RevisedClause"), O("ApprovalStatus", Opt.Approval, 20), S("ReviewedBy", 150) with { ReadOnly = true }
            }
        },
        new EntityDef
        {
            Key = "vendors", Table = "Vendors", Label = "Vendors & Processors", Module = "vendor",
            ListCols = new[] { "Name", "Category", "PersonalDataAccessed", "DpaStatus", "RiskRating", "NextReview" },
            SearchCols = new[] { "Name", "Services", "ContactPerson" },
            Cols = new()
            {
                S("Name") with { Required = true, Group = "Vendor" }, O("Category", new[] { "Cloud / Hosting", "SaaS application", "Payroll / HR", "IT services / MSP", "Marketing", "Payment", "Logistics", "Call centre / BPO", "Security", "Professional services", "Other" }, 60) with { Group = "Vendor" },
                S("ContactPerson", 150) with { Group = "Vendor" }, S("ContactEmail", 200) with { Group = "Vendor" },
                T("Services") with { Label = "Services provided", Group = "Vendor" }, B("IsDataProcessor") with { Label = "Acts as Data Processor", Group = "Vendor" },
                T("PersonalDataAccessed") with { Group = "Data" }, T("ProcessingPurpose") with { Group = "Data" }, S("StorageLocation", 300) with { Label = "Data storage location", Group = "Data" },
                B("CrossBorder") with { Group = "Data" }, T("SubProcessors") with { Group = "Data" }, S("SecurityCertifications", 300) with { Label = "Security certifications (ISO 27001, SOC 2...)", Group = "Data" },
                O("ContractStatus", new[] { "Signed", "Under negotiation", "Expired", "No contract" }, 40) with { Group = "Contract" },
                O("DpaStatus", new[] { "Signed - DPDP compliant", "Signed - needs update", "Not in place", "Not required" }, 40) with { Label = "Data processing agreement", Group = "Contract" },
                D("ContractExpiry") with { Group = "Contract" },
                B("SecurityClause") with { Label = "Security safeguard clause (Rule 6(1)(f))", Group = "Contract" },
                B("BreachNotificationClause") with { Group = "Contract" }, I("BreachNotificationHours") with { Label = "Breach notification to us within (hours)", Group = "Contract" },
                B("ErasureClause") with { Label = "Erasure / return of data clause", Group = "Contract" }, B("AuditRights") with { Group = "Contract" },
                S("RetentionRequirements", 300) with { Group = "Contract" },
                O("RiskRating", Opt.Risk, 20) with { Group = "Risk" }, D("LastAssessed") with { Group = "Risk" }, D("NextReview") with { Group = "Risk" }, T("Notes") with { Group = "Risk" }
            }
        },
        new EntityDef
        {
            Key = "vendorAssessments", Table = "VendorAssessments", Label = "Vendor Assessments", Module = "vendor", ParentKey = "vendors", ParentCol = "VendorId", TitleCol = "AssessmentDate",
            ListCols = new[] { "AssessmentDate", "Assessor", "Score", "RiskRating", "Findings" },
            Cols = new() { L("VendorId") with { Hidden = true, Required = true }, D("AssessmentDate") with { Required = true }, S("Assessor", 150), I("Score") with { Label = "Score (0-100)" }, O("RiskRating", Opt.Risk, 20), T("Findings"), T("Actions") }
        },
        new EntityDef
        {
            Key = "securityControls", Table = "SecurityControls", Label = "Security Safeguards", Module = "security", TitleCol = "ControlName",
            ListCols = new[] { "ControlKey", "Domain", "ControlName", "Status", "ToolsMapped", "Validated", "RiskRating" },
            Cols = new()
            {
                S("ControlKey", 20) with { ReadOnly = true }, S("Domain", 100) with { ReadOnly = true }, S("ControlName", 200) with { ReadOnly = true }, S("RuleReference", 100) with { ReadOnly = true },
                T("Expectation") with { ReadOnly = true },
                O("Status", Opt.Status, 30), S("ToolsMapped", 500) with { Label = "Security tools mapped", Multi = true, Options = new[] { "Microsoft Sentinel", "Wazuh", "Splunk", "Elastic Security", "CrowdStrike", "Microsoft Defender", "Fortinet", "Palo Alto Networks", "Sophos", "Gurucul", "Other" } },
                T("ConfigurationEvidence") with { Label = "Configuration evidence / description" },
                B("Validated") with { Label = "Control validated", Help = "Tool installation alone is not compliance - attach configuration evidence and validate." },
                S("ValidatedBy", 150) with { ReadOnly = true }, DT("ValidatedAt") with { ReadOnly = true },
                O("RiskRating", Opt.Risk, 20), T("Gap"), T("Recommendation"), S("Owner", 150)
            }
        },
        new EntityDef
        {
            Key = "rightsRequests", Table = "RightsRequests", Label = "Data Principal Requests", Module = "rights", TitleCol = "Reference",
            ListCols = new[] { "Reference", "RequestType", "PrincipalName", "ReceivedAt", "DueAt", "Status", "AssignedTo" },
            SearchCols = new[] { "Reference", "PrincipalName" },
            Cols = new()
            {
                S("Reference", 40) with { ReadOnly = true },
                O("RequestType", new[] { "Access (s.11)", "Correction / completion / update (s.12)", "Erasure (s.12(3))", "Consent withdrawal (s.6(4))", "Grievance (s.13)", "Nomination (s.14)", "Other" }, 60) with { Required = true },
                S("PrincipalName", 200) with { Sensitive = true }, S("PrincipalContact", 200) with { Sensitive = true }, S("PrincipalIdentifier", 200) with { Sensitive = true, Label = "Identifier (customer ID etc.)" },
                O("Channel", new[] { "Web form", "Email", "Phone", "Letter", "In person", "Consent Manager", "API" }, 40),
                DT("ReceivedAt") with { Required = true }, T("Details"),
                O("VerificationStatus", new[] { "Pending", "Verified", "Failed", "Not required" }, 20), DT("VerifiedAt"),
                S("AssignedTo", 150), DT("DueAt") with { ReadOnly = true },
                O("Status", new[] { "New", "Verifying", "In Progress", "Awaiting Data Principal", "Completed", "Rejected", "Closed" }, 40),
                T("Response"), T("RejectionReason"), DT("ClosedAt") with { ReadOnly = true }, T("ClosureEvidence")
            }
        },
        new EntityDef
        {
            Key = "requestEvents", Table = "RequestEvents", Label = "Request Audit Trail", Module = "rights", ParentKey = "rightsRequests", ParentCol = "RequestId", GenericWrite = false, SoftDelete = false, TitleCol = "Action",
            ListCols = new[] { "Action", "Notes", "CreatedBy", "CreatedAt" },
            Cols = new() { L("RequestId"), S("Action", 100), T("Notes") }
        },
        new EntityDef
        {
            Key = "incidents", Table = "Incidents", Label = "Incidents & Breaches", Module = "incident", TitleCol = "Title",
            ListCols = new[] { "Reference", "Title", "Severity", "DiscoveredAt", "IsPersonalDataBreach", "Status", "Owner" },
            SearchCols = new[] { "Reference", "Title" },
            Cols = new()
            {
                S("Reference", 40) with { ReadOnly = true, Label = "Incident ID", Group = "Incident" }, S("Title", 300) with { Required = true, Group = "Incident" },
                DT("DiscoveredAt") with { Required = true, Label = "Date/time became aware", Group = "Incident", Help = "DPDP Rule 7 timelines run from becoming aware" },
                DT("OccurredAt") with { Label = "Date/time of occurrence (if known)", Group = "Incident" },
                O("DetectionSource", new[] { "SIEM alert", "EDR alert", "Employee report", "Customer complaint", "Vendor notification", "Law enforcement / CERT-In", "Audit", "Other" }, 60) with { Group = "Incident" },
                O("Severity", new[] { "Critical", "High", "Medium", "Low" }, 20) with { Group = "Incident" },
                T("Description") with { Group = "Incident" }, T("SystemsAffected") with { Group = "Impact" }, T("DataCategoriesAffected") with { Group = "Impact" },
                I("AffectedCount") with { Label = "Estimated affected individuals", Group = "Impact" }, S("Location", 200) with { Label = "Location of occurrence", Group = "Impact" }, T("LikelyImpact") with { Group = "Impact" },
                B("IsPersonalDataBreach") with { Label = "Personal data breach (DPDP)", Group = "Classification" },
                B("CertInReportable") with { Label = "Reportable to CERT-In", Group = "Classification" },
                B("SectoralReportable") with { Label = "Reportable to sectoral regulator", Group = "Classification" }, S("SectoralRegulator", 100) with { Group = "Classification" },
                T("RootCause") with { Group = "Response" }, T("PersonResponsible") with { Label = "Findings on person who caused breach", Group = "Response" },
                T("ContainmentActions") with { Group = "Response" }, T("Remediation") with { Group = "Response" }, T("PreventiveMeasures") with { Label = "Measures to prevent recurrence", Group = "Response" },
                S("Owner", 150) with { Label = "Incident owner", Group = "Response" },
                O("Status", new[] { "Open", "Contained", "Investigating", "Notified", "Closed" }, 30) with { Group = "Response" },
                O("LegalApproval", Opt.Approval, 20) with { ReadOnly = true, Group = "Approvals" }, O("ManagementApproval", Opt.Approval, 20) with { ReadOnly = true, Group = "Approvals" },
                DT("ClosedAt") with { ReadOnly = true, Group = "Approvals" }, B("IsDrill") with { Label = "Mock drill (exercise)", Group = "Classification" }
            }
        },
        new EntityDef
        {
            Key = "incidentNotifications", Table = "IncidentNotifications", Label = "Breach Notifications", Module = "incident", ParentKey = "incidents", ParentCol = "IncidentId", TitleCol = "RecipientName",
            ListCols = new[] { "RecipientName", "Basis", "DueAt", "SentAt", "Status", "Method" },
            Cols = new()
            {
                L("IncidentId") with { Hidden = true, Required = true }, S("RuleKey", 40) with { Hidden = true },
                S("RecipientName", 200) with { Required = true, Label = "Recipient" }, S("Basis", 300) with { Label = "Legal basis" },
                DT("DueAt"), DT("SentAt"), O("Status", new[] { "Pending", "Sent", "Not required", "Extension requested" }, 30),
                O("Method", new[] { "Email", "Portal", "User account / app", "SMS", "Letter", "Phone" }, 40), S("SubmissionReference", 200), T("Content"), T("Notes")
            }
        },
        new EntityDef
        {
            Key = "incidentEvents", Table = "IncidentEvents", Label = "Incident Timeline", Module = "incident", ParentKey = "incidents", ParentCol = "IncidentId", TitleCol = "EventText",
            ListCols = new[] { "EventAt", "EventText", "CreatedBy" },
            Cols = new() { L("IncidentId") with { Hidden = true, Required = true }, DT("EventAt") with { Required = true }, T("EventText") with { Required = true, Label = "Event" }, L("EvidenceId") with { Hidden = true } }
        },
        new EntityDef
        {
            Key = "actions", Table = "Actions", Label = "Remediation Actions", Module = "roadmap", TitleCol = "Title",
            ListCols = new[] { "GapId", "RequirementRef", "Title", "RiskRating", "Priority", "Owner", "TargetDate", "Status" },
            SearchCols = new[] { "GapId", "Title", "RequirementRef" },
            ExtraIndexes = new[] { "Status", "SourceType,SourceId" },
            Cols = new()
            {
                S("GapId", 30) with { ReadOnly = true, Label = "Gap ID" }, S("RequirementRef", 100) with { Label = "Requirement reference" },
                S("Title", 300) with { Required = true, Label = "Recommended action" }, T("Details"),
                S("SourceType", 30) with { Hidden = true }, L("SourceId") with { Hidden = true },
                O("RiskRating", Opt.Risk, 20), O("Priority", Opt.Priority, 30), S("Department", 100) with { Ref = "departments", Label = "Responsible department" },
                S("Owner", 150) with { Label = "Responsible employee" }, D("TargetDate"), O("Status", Opt.ActionStatus, 30),
                O("Plan", new[] { "30-day", "60-day", "90-day", "Custom" }, 20), T("SupportingEvidence"),
                O("Approval", Opt.Approval, 20) with { ReadOnly = true }, S("ApprovedBy", 150) with { ReadOnly = true }, DT("CompletedAt") with { ReadOnly = true }
            }
        },
        new EntityDef
        {
            Key = "reports", Table = "Reports", Label = "Reports", Module = "report", GenericWrite = false, TitleCol = "Title",
            ListCols = new[] { "Title", "ReportType", "Version", "Status", "CreatedBy", "CreatedAt" },
            Cols = new()
            {
                S("ReportType", 60), S("Title", 300), S("Version", 20), O("Status", new[] { "Draft", "Approved", "Superseded" }, 20), L("TemplateId"), L("AssessmentId"),
                T("ContentJson") with { Hidden = true }, S("ApprovedBy", 150), DT("ApprovedAt"), D("PeriodFrom"), D("PeriodTo")
            }
        },
        new EntityDef
        {
            Key = "reportFiles", Table = "ReportFiles", Label = "Report Exports", Module = "report", ParentKey = "reports", ParentCol = "ReportId", GenericWrite = false, SoftDelete = false, TitleCol = "FileName",
            ListCols = new[] { "Format", "FileName", "SizeBytes", "Sha256", "CreatedBy", "CreatedAt" },
            Cols = new() { L("ReportId"), S("Format", 10), S("FileName", 260), L("SizeBytes"), S("Sha256", 64), S("StoragePath", 500) with { Hidden = true } }
        },
        new EntityDef
        {
            Key = "reportTemplates", Table = "ReportTemplates", Label = "Report Templates", Module = "template", OrgScoped = false, GenericWrite = false,
            ListCols = new[] { "Name", "ReportType", "CoverDesign", "IsDefault" },
            Cols = new() { L("OrganizationId"), S("Name", 200), S("ReportType", 60), S("CoverDesign", 50), S("LogoPlacement", 30), S("Watermark", 60), S("FontFamily", 50), S("PrimaryColor", 10), S("SecondaryColor", 10), T("SectionsJson"), B("IsDefault"), B("IsSystem") }
        },
        new EntityDef
        {
            Key = "files", Table = "StoredFiles", Label = "Files", Module = "org", OrgScoped = false, GenericWrite = false, SoftDelete = false, TitleCol = "FileName",
            Cols = new() { L("OrganizationId"), S("Purpose", 40), S("FileName", 260), S("ContentType", 150), L("SizeBytes"), S("Sha256", 64), S("StoragePath", 500) }
        },
        new EntityDef
        {
            Key = "apiKeys", Table = "ApiKeys", Label = "Integration API Keys", Module = "admin", GenericWrite = false, SoftDelete = false,
            ListCols = new[] { "Name", "Prefix", "IsActive", "LastUsedAt", "CreatedAt" },
            Cols = new() { S("Name", 150), S("Prefix", 12), S("KeyHash", 64) with { Hidden = true }, B("IsActive"), DT("LastUsedAt"), S("Scopes", 200) }
        },
    };

    public static readonly Dictionary<string, EntityDef> ByKey = All.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
    public static EntityDef Get(string key) => ByKey.TryGetValue(key, out var d) ? d : throw new KeyNotFoundException($"Unknown entity '{key}'");
}
