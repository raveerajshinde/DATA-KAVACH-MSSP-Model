-- DataKavach initial schema (Sqlite). Generated from Data/Schema.cs.
CREATE TABLE "Users" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Email" TEXT,
  "FullName" TEXT,
  "PasswordHash" TEXT,
  "Role" TEXT,
  "Department" TEXT,
  "Phone" TEXT,
  "IsActive" INTEGER,
  "MfaEnabled" INTEGER,
  "MfaSecret" TEXT,
  "FailedAttempts" INTEGER,
  "LockoutUntil" TIMESTAMP,
  "LastLoginAt" TIMESTAMP,
  "MustChangePassword" INTEGER,
  "PasswordChangedAt" TIMESTAMP,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0
)
-- go
CREATE UNIQUE INDEX "UX_Users_Email" ON "Users" ("Email")
-- go
CREATE TABLE "RolePermissions" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Role" TEXT,
  "Permission" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT
)
-- go
CREATE UNIQUE INDEX "UX_RolePermissions_Role_Permission" ON "RolePermissions" ("Role", "Permission")
-- go
CREATE TABLE "AuditLogs" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "At" TIMESTAMP,
  "UserId" INTEGER,
  "UserEmail" TEXT,
  "OrganizationId" INTEGER,
  "Action" TEXT,
  "Entity" TEXT,
  "EntityId" INTEGER,
  "Details" TEXT,
  "Ip" TEXT,
  "PrevHash" TEXT,
  "Hash" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT
)
-- go
CREATE INDEX "IX_AuditLogs_At" ON "AuditLogs" ("At")
-- go
CREATE INDEX "IX_AuditLogs_Entity_EntityId" ON "AuditLogs" ("Entity", "EntityId")
-- go
CREATE INDEX "IX_AuditLogs_OrganizationId" ON "AuditLogs" ("OrganizationId")
-- go
CREATE TABLE "Settings" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER,
  "SettingKey" TEXT,
  "SettingValue" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT
)
-- go
CREATE UNIQUE INDEX "UX_Settings_OrganizationId_SettingKey" ON "Settings" ("OrganizationId", "SettingKey")
-- go
CREATE TABLE "Organizations" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Name" TEXT,
  "LegalName" TEXT,
  "RegisteredAddress" TEXT,
  "City" TEXT,
  "State" TEXT,
  "Cin" TEXT,
  "Gstin" TEXT,
  "Industry" TEXT,
  "Website" TEXT,
  "Email" TEXT,
  "Phone" TEXT,
  "EmployeeCount" INTEGER,
  "LocationCount" INTEGER,
  "DpoName" TEXT,
  "DpoDesignation" TEXT,
  "DpoEmail" TEXT,
  "DpoPhone" TEXT,
  "PrivacyContactEmail" TEXT,
  "GrievanceUrl" TEXT,
  "GrievanceResponseDays" INTEGER,
  "IsSignificantDataFiduciary" INTEGER,
  "ProcessesChildrenData" INTEGER,
  "ProcessesGuardianData" INTEGER,
  "ReliesOnConsent" INTEGER,
  "UsesProcessors" INTEGER,
  "CrossBorderTransfers" INTEGER,
  "UsesConsentManager" INTEGER,
  "ThirdScheduleClass" TEXT,
  "IsStateInstrumentality" INTEGER,
  "ResearchExemption" INTEGER,
  "CertInApplicable" INTEGER,
  "PreparedBy" TEXT,
  "PreparedFor" TEXT,
  "AssessmentDate" DATE,
  "PeriodFrom" DATE,
  "PeriodTo" DATE,
  "ReportReference" TEXT,
  "ReportVersion" TEXT,
  "Confidentiality" TEXT,
  "LogoFileId" INTEGER,
  "ConsultantLogoFileId" INTEGER,
  "ConsultantName" TEXT,
  "PrimaryColor" TEXT,
  "SecondaryColor" TEXT,
  "FontFamily" TEXT,
  "HeaderText" TEXT,
  "FooterText" TEXT,
  "Watermark" TEXT,
  "CoverDesign" TEXT,
  "LogoPlacement" TEXT,
  "SignatoryName" TEXT,
  "SignatoryTitle" TEXT,
  "ReviewerName" TEXT,
  "ReviewerTitle" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0
)
-- go
CREATE TABLE "Departments" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Name" TEXT,
  "HeadName" TEXT,
  "HeadEmail" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Departments_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Departments_OrganizationId" ON "Departments" ("OrganizationId")
-- go
CREATE TABLE "Requirements" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Code" TEXT,
  "Module" TEXT,
  "Title" TEXT,
  "ActSection" TEXT,
  "Rule" TEXT,
  "LegalReference" TEXT,
  "Description" TEXT,
  "Question" TEXT,
  "Guidance" TEXT,
  "EvidenceRequired" TEXT,
  "Applicability" TEXT,
  "EffectiveFrom" DATE,
  "DefaultRisk" TEXT,
  "PenaltyReference" TEXT,
  "IsActive" INTEGER,
  "SortOrder" INTEGER,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0
)
-- go
CREATE INDEX "IX_Requirements_Code" ON "Requirements" ("Code")
-- go
CREATE TABLE "Assessments" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Name" TEXT,
  "PeriodFrom" DATE,
  "PeriodTo" DATE,
  "Status" TEXT,
  "Owner" TEXT,
  "Scope" TEXT,
  "Notes" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Assessments_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Assessments_OrganizationId" ON "Assessments" ("OrganizationId")
-- go
CREATE TABLE "AssessmentItems" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "AssessmentId" INTEGER,
  "RequirementId" INTEGER,
  "RequirementCode" TEXT,
  "Status" TEXT,
  "RiskRating" TEXT,
  "IsApplicable" INTEGER,
  "ApplicabilityNote" TEXT,
  "Response" TEXT,
  "GapDescription" TEXT,
  "Recommendation" TEXT,
  "Department" TEXT,
  "Owner" TEXT,
  "TargetDate" DATE,
  "AssessedBy" TEXT,
  "AssessedAt" TIMESTAMP,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_AssessmentItems_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_AssessmentItems_Assessments" FOREIGN KEY ("AssessmentId") REFERENCES "Assessments" ("Id")
)
-- go
CREATE INDEX "IX_AssessmentItems_OrganizationId" ON "AssessmentItems" ("OrganizationId")
-- go
CREATE INDEX "IX_AssessmentItems_AssessmentId" ON "AssessmentItems" ("AssessmentId")
-- go
CREATE INDEX "IX_AssessmentItems_RequirementId" ON "AssessmentItems" ("RequirementId")
-- go
CREATE INDEX "IX_AssessmentItems_Status" ON "AssessmentItems" ("Status")
-- go
CREATE TABLE "Evidence" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Title" TEXT,
  "Description" TEXT,
  "Category" TEXT,
  "LinkedEntity" TEXT,
  "LinkedId" INTEGER,
  "CurrentVersion" INTEGER,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Evidence_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Evidence_OrganizationId" ON "Evidence" ("OrganizationId")
-- go
CREATE INDEX "IX_Evidence_LinkedEntity_LinkedId" ON "Evidence" ("LinkedEntity", "LinkedId")
-- go
CREATE TABLE "EvidenceVersions" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "EvidenceId" INTEGER,
  "Version" INTEGER,
  "FileName" TEXT,
  "ContentType" TEXT,
  "SizeBytes" INTEGER,
  "Sha256" TEXT,
  "StoragePath" TEXT,
  "Comment" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  CONSTRAINT "FK_EvidenceVersions_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_EvidenceVersions_Evidence" FOREIGN KEY ("EvidenceId") REFERENCES "Evidence" ("Id")
)
-- go
CREATE INDEX "IX_EvidenceVersions_OrganizationId" ON "EvidenceVersions" ("OrganizationId")
-- go
CREATE INDEX "IX_EvidenceVersions_EvidenceId" ON "EvidenceVersions" ("EvidenceId")
-- go
CREATE TABLE "DataInventory" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "ProcessingActivity" TEXT,
  "Department" TEXT,
  "BusinessOwner" TEXT,
  "PrincipalCategory" TEXT,
  "DataCategories" TEXT,
  "IncludesChildrenData" INTEGER,
  "CollectionSource" TEXT,
  "Purpose" TEXT,
  "LegalGround" TEXT,
  "ApproxVolume" TEXT,
  "StorageLocation" TEXT,
  "SystemName" TEXT,
  "CloudProvider" TEXT,
  "AccessRoles" TEXT,
  "InternalRecipients" TEXT,
  "ExternalProcessors" TEXT,
  "CrossBorder" INTEGER,
  "CrossBorderDetails" TEXT,
  "RetentionPeriod" TEXT,
  "ErasureMethod" TEXT,
  "SecuritySafeguards" TEXT,
  "LastReviewed" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_DataInventory_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_DataInventory_OrganizationId" ON "DataInventory" ("OrganizationId")
-- go
CREATE TABLE "DataFlows" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Name" TEXT,
  "SourceSystem" TEXT,
  "DestinationSystem" TEXT,
  "DestinationType" TEXT,
  "DataCategories" TEXT,
  "TransferMethod" TEXT,
  "Encrypted" INTEGER,
  "CrossBorder" INTEGER,
  "Country" TEXT,
  "Frequency" TEXT,
  "Processor" TEXT,
  "Notes" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_DataFlows_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_DataFlows_OrganizationId" ON "DataFlows" ("OrganizationId")
-- go
CREATE TABLE "Dpias" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "ProcessingActivity" TEXT,
  "Trigger" TEXT,
  "Purpose" TEXT,
  "RightsDescription" TEXT,
  "DataCategories" TEXT,
  "PrincipalCategories" TEXT,
  "Volume" TEXT,
  "Systems" TEXT,
  "Necessity" TEXT,
  "Status" TEXT,
  "OverallResidualRisk" TEXT,
  "Owner" TEXT,
  "Approver" TEXT,
  "Conclusion" TEXT,
  "ApprovedAt" TIMESTAMP,
  "ApprovedBy" TEXT,
  "NextReview" DATE,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Dpias_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Dpias_OrganizationId" ON "Dpias" ("OrganizationId")
-- go
CREATE TABLE "DpiaRisks" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "DpiaId" INTEGER,
  "RiskType" TEXT,
  "Description" TEXT,
  "Likelihood" TEXT,
  "Impact" TEXT,
  "InherentScore" INTEGER,
  "InherentRating" TEXT,
  "ExistingControls" TEXT,
  "ResidualLikelihood" TEXT,
  "ResidualImpact" TEXT,
  "ResidualScore" INTEGER,
  "ResidualRating" TEXT,
  "MitigationPlan" TEXT,
  "Owner" TEXT,
  "DueDate" DATE,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_DpiaRisks_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_DpiaRisks_Dpias" FOREIGN KEY ("DpiaId") REFERENCES "Dpias" ("Id")
)
-- go
CREATE INDEX "IX_DpiaRisks_OrganizationId" ON "DpiaRisks" ("OrganizationId")
-- go
CREATE INDEX "IX_DpiaRisks_DpiaId" ON "DpiaRisks" ("DpiaId")
-- go
CREATE TABLE "ConsentNotices" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Title" TEXT,
  "Version" TEXT,
  "Language" TEXT,
  "Purposes" TEXT,
  "DataItems" TEXT,
  "NoticeText" TEXT,
  "WithdrawalLink" TEXT,
  "GrievanceContact" TEXT,
  "BoardComplaintInfo" TEXT,
  "CollectionPoint" TEXT,
  "Status" TEXT,
  "PublishedAt" TIMESTAMP,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_ConsentNotices_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_ConsentNotices_OrganizationId" ON "ConsentNotices" ("OrganizationId")
-- go
CREATE TABLE "ConsentRecords" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "PrincipalRef" TEXT,
  "IdentifierType" TEXT,
  "NoticeId" INTEGER,
  "NoticeVersion" TEXT,
  "Purpose" TEXT,
  "Language" TEXT,
  "Status" TEXT,
  "GivenAt" TIMESTAMP,
  "WithdrawnAt" TIMESTAMP,
  "Channel" TEXT,
  "ConsentManagerId" TEXT,
  "EvidenceRef" TEXT,
  "IsParentalConsent" INTEGER,
  "SourceIp" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_ConsentRecords_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_ConsentRecords_OrganizationId" ON "ConsentRecords" ("OrganizationId")
-- go
CREATE INDEX "IX_ConsentRecords_PrincipalRef" ON "ConsentRecords" ("PrincipalRef")
-- go
CREATE TABLE "ConsentEvents" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "ConsentRecordId" INTEGER,
  "EventType" TEXT,
  "Details" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  CONSTRAINT "FK_ConsentEvents_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_ConsentEvents_ConsentRecords" FOREIGN KEY ("ConsentRecordId") REFERENCES "ConsentRecords" ("Id")
)
-- go
CREATE INDEX "IX_ConsentEvents_OrganizationId" ON "ConsentEvents" ("OrganizationId")
-- go
CREATE INDEX "IX_ConsentEvents_ConsentRecordId" ON "ConsentEvents" ("ConsentRecordId")
-- go
CREATE TABLE "Policies" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Title" TEXT,
  "TemplateKey" TEXT,
  "Version" TEXT,
  "Source" TEXT,
  "Status" TEXT,
  "Content" TEXT,
  "BasedOnPolicyId" INTEGER,
  "OriginalFileName" TEXT,
  "ApprovedBy" TEXT,
  "ApprovedAt" TIMESTAMP,
  "ReviewNotes" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Policies_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Policies_OrganizationId" ON "Policies" ("OrganizationId")
-- go
CREATE TABLE "PolicyRecommendations" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "PolicyId" INTEGER,
  "Topic" TEXT,
  "ExistingClause" TEXT,
  "RecommendedModification" TEXT,
  "Reason" TEXT,
  "LegalReference" TEXT,
  "RevisedClause" TEXT,
  "ApprovalStatus" TEXT,
  "ReviewedBy" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_PolicyRecommendations_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_PolicyRecommendations_Policies" FOREIGN KEY ("PolicyId") REFERENCES "Policies" ("Id")
)
-- go
CREATE INDEX "IX_PolicyRecommendations_OrganizationId" ON "PolicyRecommendations" ("OrganizationId")
-- go
CREATE INDEX "IX_PolicyRecommendations_PolicyId" ON "PolicyRecommendations" ("PolicyId")
-- go
CREATE TABLE "Vendors" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Name" TEXT,
  "Category" TEXT,
  "ContactPerson" TEXT,
  "ContactEmail" TEXT,
  "Services" TEXT,
  "IsDataProcessor" INTEGER,
  "PersonalDataAccessed" TEXT,
  "ProcessingPurpose" TEXT,
  "StorageLocation" TEXT,
  "CrossBorder" INTEGER,
  "SubProcessors" TEXT,
  "SecurityCertifications" TEXT,
  "ContractStatus" TEXT,
  "DpaStatus" TEXT,
  "ContractExpiry" DATE,
  "SecurityClause" INTEGER,
  "BreachNotificationClause" INTEGER,
  "BreachNotificationHours" INTEGER,
  "ErasureClause" INTEGER,
  "AuditRights" INTEGER,
  "RetentionRequirements" TEXT,
  "RiskRating" TEXT,
  "LastAssessed" DATE,
  "NextReview" DATE,
  "Notes" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Vendors_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Vendors_OrganizationId" ON "Vendors" ("OrganizationId")
-- go
CREATE TABLE "VendorAssessments" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "VendorId" INTEGER,
  "AssessmentDate" DATE,
  "Assessor" TEXT,
  "Score" INTEGER,
  "RiskRating" TEXT,
  "Findings" TEXT,
  "Actions" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_VendorAssessments_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_VendorAssessments_Vendors" FOREIGN KEY ("VendorId") REFERENCES "Vendors" ("Id")
)
-- go
CREATE INDEX "IX_VendorAssessments_OrganizationId" ON "VendorAssessments" ("OrganizationId")
-- go
CREATE INDEX "IX_VendorAssessments_VendorId" ON "VendorAssessments" ("VendorId")
-- go
CREATE TABLE "SecurityControls" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "ControlKey" TEXT,
  "Domain" TEXT,
  "ControlName" TEXT,
  "RuleReference" TEXT,
  "Expectation" TEXT,
  "Status" TEXT,
  "ToolsMapped" TEXT,
  "ConfigurationEvidence" TEXT,
  "Validated" INTEGER,
  "ValidatedBy" TEXT,
  "ValidatedAt" TIMESTAMP,
  "RiskRating" TEXT,
  "Gap" TEXT,
  "Recommendation" TEXT,
  "Owner" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_SecurityControls_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_SecurityControls_OrganizationId" ON "SecurityControls" ("OrganizationId")
-- go
CREATE TABLE "RightsRequests" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Reference" TEXT,
  "RequestType" TEXT,
  "PrincipalName" TEXT,
  "PrincipalContact" TEXT,
  "PrincipalIdentifier" TEXT,
  "Channel" TEXT,
  "ReceivedAt" TIMESTAMP,
  "Details" TEXT,
  "VerificationStatus" TEXT,
  "VerifiedAt" TIMESTAMP,
  "AssignedTo" TEXT,
  "DueAt" TIMESTAMP,
  "Status" TEXT,
  "Response" TEXT,
  "RejectionReason" TEXT,
  "ClosedAt" TIMESTAMP,
  "ClosureEvidence" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_RightsRequests_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_RightsRequests_OrganizationId" ON "RightsRequests" ("OrganizationId")
-- go
CREATE TABLE "RequestEvents" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "RequestId" INTEGER,
  "Action" TEXT,
  "Notes" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  CONSTRAINT "FK_RequestEvents_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_RequestEvents_RightsRequests" FOREIGN KEY ("RequestId") REFERENCES "RightsRequests" ("Id")
)
-- go
CREATE INDEX "IX_RequestEvents_OrganizationId" ON "RequestEvents" ("OrganizationId")
-- go
CREATE INDEX "IX_RequestEvents_RequestId" ON "RequestEvents" ("RequestId")
-- go
CREATE TABLE "Incidents" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Reference" TEXT,
  "Title" TEXT,
  "DiscoveredAt" TIMESTAMP,
  "OccurredAt" TIMESTAMP,
  "DetectionSource" TEXT,
  "Severity" TEXT,
  "Description" TEXT,
  "SystemsAffected" TEXT,
  "DataCategoriesAffected" TEXT,
  "AffectedCount" INTEGER,
  "Location" TEXT,
  "LikelyImpact" TEXT,
  "IsPersonalDataBreach" INTEGER,
  "CertInReportable" INTEGER,
  "SectoralReportable" INTEGER,
  "SectoralRegulator" TEXT,
  "RootCause" TEXT,
  "PersonResponsible" TEXT,
  "ContainmentActions" TEXT,
  "Remediation" TEXT,
  "PreventiveMeasures" TEXT,
  "Owner" TEXT,
  "Status" TEXT,
  "LegalApproval" TEXT,
  "ManagementApproval" TEXT,
  "ClosedAt" TIMESTAMP,
  "IsDrill" INTEGER,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Incidents_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Incidents_OrganizationId" ON "Incidents" ("OrganizationId")
-- go
CREATE TABLE "IncidentNotifications" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "IncidentId" INTEGER,
  "RuleKey" TEXT,
  "RecipientName" TEXT,
  "Basis" TEXT,
  "DueAt" TIMESTAMP,
  "SentAt" TIMESTAMP,
  "Status" TEXT,
  "Method" TEXT,
  "SubmissionReference" TEXT,
  "Content" TEXT,
  "Notes" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_IncidentNotifications_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_IncidentNotifications_Incidents" FOREIGN KEY ("IncidentId") REFERENCES "Incidents" ("Id")
)
-- go
CREATE INDEX "IX_IncidentNotifications_OrganizationId" ON "IncidentNotifications" ("OrganizationId")
-- go
CREATE INDEX "IX_IncidentNotifications_IncidentId" ON "IncidentNotifications" ("IncidentId")
-- go
CREATE TABLE "IncidentEvents" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "IncidentId" INTEGER,
  "EventAt" TIMESTAMP,
  "EventText" TEXT,
  "EvidenceId" INTEGER,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_IncidentEvents_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_IncidentEvents_Incidents" FOREIGN KEY ("IncidentId") REFERENCES "Incidents" ("Id")
)
-- go
CREATE INDEX "IX_IncidentEvents_OrganizationId" ON "IncidentEvents" ("OrganizationId")
-- go
CREATE INDEX "IX_IncidentEvents_IncidentId" ON "IncidentEvents" ("IncidentId")
-- go
CREATE TABLE "Actions" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "GapId" TEXT,
  "RequirementRef" TEXT,
  "Title" TEXT,
  "Details" TEXT,
  "SourceType" TEXT,
  "SourceId" INTEGER,
  "RiskRating" TEXT,
  "Priority" TEXT,
  "Department" TEXT,
  "Owner" TEXT,
  "TargetDate" DATE,
  "Status" TEXT,
  "Plan" TEXT,
  "SupportingEvidence" TEXT,
  "Approval" TEXT,
  "ApprovedBy" TEXT,
  "CompletedAt" TIMESTAMP,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Actions_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Actions_OrganizationId" ON "Actions" ("OrganizationId")
-- go
CREATE INDEX "IX_Actions_Status" ON "Actions" ("Status")
-- go
CREATE INDEX "IX_Actions_SourceType_SourceId" ON "Actions" ("SourceType", "SourceId")
-- go
CREATE TABLE "Reports" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "ReportType" TEXT,
  "Title" TEXT,
  "Version" TEXT,
  "Status" TEXT,
  "TemplateId" INTEGER,
  "AssessmentId" INTEGER,
  "ContentJson" TEXT,
  "ApprovedBy" TEXT,
  "ApprovedAt" TIMESTAMP,
  "PeriodFrom" DATE,
  "PeriodTo" DATE,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Reports_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_Reports_OrganizationId" ON "Reports" ("OrganizationId")
-- go
CREATE TABLE "ReportFiles" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "ReportId" INTEGER,
  "Format" TEXT,
  "FileName" TEXT,
  "SizeBytes" INTEGER,
  "Sha256" TEXT,
  "StoragePath" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  CONSTRAINT "FK_ReportFiles_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id"),
  CONSTRAINT "FK_ReportFiles_Reports" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id")
)
-- go
CREATE INDEX "IX_ReportFiles_OrganizationId" ON "ReportFiles" ("OrganizationId")
-- go
CREATE INDEX "IX_ReportFiles_ReportId" ON "ReportFiles" ("ReportId")
-- go
CREATE TABLE "ReportTemplates" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER,
  "Name" TEXT,
  "ReportType" TEXT,
  "CoverDesign" TEXT,
  "LogoPlacement" TEXT,
  "Watermark" TEXT,
  "FontFamily" TEXT,
  "PrimaryColor" TEXT,
  "SecondaryColor" TEXT,
  "SectionsJson" TEXT,
  "IsDefault" INTEGER,
  "IsSystem" INTEGER,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  "IsDeleted" INTEGER NOT NULL DEFAULT 0
)
-- go
CREATE TABLE "StoredFiles" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER,
  "Purpose" TEXT,
  "FileName" TEXT,
  "ContentType" TEXT,
  "SizeBytes" INTEGER,
  "Sha256" TEXT,
  "StoragePath" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT
)
-- go
CREATE TABLE "ApiKeys" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "OrganizationId" INTEGER NOT NULL,
  "Name" TEXT,
  "Prefix" TEXT,
  "KeyHash" TEXT,
  "IsActive" INTEGER,
  "LastUsedAt" TIMESTAMP,
  "Scopes" TEXT,
  "CreatedAt" TIMESTAMP NOT NULL,
  "CreatedBy" TEXT,
  "UpdatedAt" TIMESTAMP,
  "UpdatedBy" TEXT,
  CONSTRAINT "FK_ApiKeys_Org" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id")
)
-- go
CREATE INDEX "IX_ApiKeys_OrganizationId" ON "ApiKeys" ("OrganizationId")
-- go
