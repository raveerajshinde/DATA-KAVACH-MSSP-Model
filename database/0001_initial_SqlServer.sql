-- DataKavach initial schema (SqlServer). Generated from Data/Schema.cs.
CREATE TABLE [Users] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY,
  [Email] NVARCHAR(200),
  [FullName] NVARCHAR(200),
  [PasswordHash] NVARCHAR(500),
  [Role] NVARCHAR(50),
  [Department] NVARCHAR(100),
  [Phone] NVARCHAR(50),
  [IsActive] BIT,
  [MfaEnabled] BIT,
  [MfaSecret] NVARCHAR(1000),
  [FailedAttempts] INT,
  [LockoutUntil] DATETIME2(3),
  [LastLoginAt] DATETIME2(3),
  [MustChangePassword] BIT,
  [PasswordChangedAt] DATETIME2(3),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Users_IsDeleted] DEFAULT 0
)
-- go
CREATE UNIQUE INDEX [UX_Users_Email] ON [Users] ([Email])
-- go
CREATE TABLE [RolePermissions] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RolePermissions] PRIMARY KEY,
  [Role] NVARCHAR(50),
  [Permission] NVARCHAR(100),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200)
)
-- go
CREATE UNIQUE INDEX [UX_RolePermissions_Role_Permission] ON [RolePermissions] ([Role], [Permission])
-- go
CREATE TABLE [AuditLogs] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AuditLogs] PRIMARY KEY,
  [At] DATETIME2(3),
  [UserId] BIGINT,
  [UserEmail] NVARCHAR(200),
  [OrganizationId] BIGINT,
  [Action] NVARCHAR(50),
  [Entity] NVARCHAR(60),
  [EntityId] BIGINT,
  [Details] NVARCHAR(MAX),
  [Ip] NVARCHAR(64),
  [PrevHash] NVARCHAR(64),
  [Hash] NVARCHAR(64),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200)
)
-- go
CREATE INDEX [IX_AuditLogs_At] ON [AuditLogs] ([At])
-- go
CREATE INDEX [IX_AuditLogs_Entity_EntityId] ON [AuditLogs] ([Entity], [EntityId])
-- go
CREATE INDEX [IX_AuditLogs_OrganizationId] ON [AuditLogs] ([OrganizationId])
-- go
CREATE TABLE [Settings] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Settings] PRIMARY KEY,
  [OrganizationId] BIGINT,
  [SettingKey] NVARCHAR(100),
  [SettingValue] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200)
)
-- go
CREATE UNIQUE INDEX [UX_Settings_OrganizationId_SettingKey] ON [Settings] ([OrganizationId], [SettingKey])
-- go
CREATE TABLE [Organizations] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Organizations] PRIMARY KEY,
  [Name] NVARCHAR(200),
  [LegalName] NVARCHAR(200),
  [RegisteredAddress] NVARCHAR(MAX),
  [City] NVARCHAR(100),
  [State] NVARCHAR(100),
  [Cin] NVARCHAR(30),
  [Gstin] NVARCHAR(20),
  [Industry] NVARCHAR(100),
  [Website] NVARCHAR(200),
  [Email] NVARCHAR(200),
  [Phone] NVARCHAR(50),
  [EmployeeCount] INT,
  [LocationCount] INT,
  [DpoName] NVARCHAR(200),
  [DpoDesignation] NVARCHAR(200),
  [DpoEmail] NVARCHAR(200),
  [DpoPhone] NVARCHAR(50),
  [PrivacyContactEmail] NVARCHAR(200),
  [GrievanceUrl] NVARCHAR(200),
  [GrievanceResponseDays] INT,
  [IsSignificantDataFiduciary] BIT,
  [ProcessesChildrenData] BIT,
  [ProcessesGuardianData] BIT,
  [ReliesOnConsent] BIT,
  [UsesProcessors] BIT,
  [CrossBorderTransfers] BIT,
  [UsesConsentManager] BIT,
  [ThirdScheduleClass] NVARCHAR(100),
  [IsStateInstrumentality] BIT,
  [ResearchExemption] BIT,
  [CertInApplicable] BIT,
  [PreparedBy] NVARCHAR(200),
  [PreparedFor] NVARCHAR(200),
  [AssessmentDate] DATE,
  [PeriodFrom] DATE,
  [PeriodTo] DATE,
  [ReportReference] NVARCHAR(100),
  [ReportVersion] NVARCHAR(30),
  [Confidentiality] NVARCHAR(50),
  [LogoFileId] BIGINT,
  [ConsultantLogoFileId] BIGINT,
  [ConsultantName] NVARCHAR(200),
  [PrimaryColor] NVARCHAR(10),
  [SecondaryColor] NVARCHAR(10),
  [FontFamily] NVARCHAR(50),
  [HeaderText] NVARCHAR(200),
  [FooterText] NVARCHAR(200),
  [Watermark] NVARCHAR(60),
  [CoverDesign] NVARCHAR(50),
  [LogoPlacement] NVARCHAR(30),
  [SignatoryName] NVARCHAR(200),
  [SignatoryTitle] NVARCHAR(200),
  [ReviewerName] NVARCHAR(200),
  [ReviewerTitle] NVARCHAR(200),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Organizations_IsDeleted] DEFAULT 0
)
-- go
CREATE TABLE [Departments] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Departments] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Name] NVARCHAR(200),
  [HeadName] NVARCHAR(200),
  [HeadEmail] NVARCHAR(200),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Departments_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Departments_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Departments_OrganizationId] ON [Departments] ([OrganizationId])
-- go
CREATE TABLE [Requirements] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Requirements] PRIMARY KEY,
  [Code] NVARCHAR(20),
  [Module] NVARCHAR(100),
  [Title] NVARCHAR(300),
  [ActSection] NVARCHAR(100),
  [Rule] NVARCHAR(100),
  [LegalReference] NVARCHAR(300),
  [Description] NVARCHAR(MAX),
  [Question] NVARCHAR(MAX),
  [Guidance] NVARCHAR(MAX),
  [EvidenceRequired] NVARCHAR(MAX),
  [Applicability] NVARCHAR(40),
  [EffectiveFrom] DATE,
  [DefaultRisk] NVARCHAR(20),
  [PenaltyReference] NVARCHAR(200),
  [IsActive] BIT,
  [SortOrder] INT,
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Requirements_IsDeleted] DEFAULT 0
)
-- go
CREATE INDEX [IX_Requirements_Code] ON [Requirements] ([Code])
-- go
CREATE TABLE [Assessments] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Assessments] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Name] NVARCHAR(200),
  [PeriodFrom] DATE,
  [PeriodTo] DATE,
  [Status] NVARCHAR(30),
  [Owner] NVARCHAR(200),
  [Scope] NVARCHAR(MAX),
  [Notes] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Assessments_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Assessments_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Assessments_OrganizationId] ON [Assessments] ([OrganizationId])
-- go
CREATE TABLE [AssessmentItems] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AssessmentItems] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [AssessmentId] BIGINT,
  [RequirementId] BIGINT,
  [RequirementCode] NVARCHAR(20),
  [Status] NVARCHAR(30),
  [RiskRating] NVARCHAR(20),
  [IsApplicable] BIT,
  [ApplicabilityNote] NVARCHAR(300),
  [Response] NVARCHAR(MAX),
  [GapDescription] NVARCHAR(MAX),
  [Recommendation] NVARCHAR(MAX),
  [Department] NVARCHAR(100),
  [Owner] NVARCHAR(150),
  [TargetDate] DATE,
  [AssessedBy] NVARCHAR(150),
  [AssessedAt] DATETIME2(3),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_AssessmentItems_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_AssessmentItems_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_AssessmentItems_Assessments] FOREIGN KEY ([AssessmentId]) REFERENCES [Assessments] ([Id])
)
-- go
CREATE INDEX [IX_AssessmentItems_OrganizationId] ON [AssessmentItems] ([OrganizationId])
-- go
CREATE INDEX [IX_AssessmentItems_AssessmentId] ON [AssessmentItems] ([AssessmentId])
-- go
CREATE INDEX [IX_AssessmentItems_RequirementId] ON [AssessmentItems] ([RequirementId])
-- go
CREATE INDEX [IX_AssessmentItems_Status] ON [AssessmentItems] ([Status])
-- go
CREATE TABLE [Evidence] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Evidence] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Title] NVARCHAR(300),
  [Description] NVARCHAR(MAX),
  [Category] NVARCHAR(100),
  [LinkedEntity] NVARCHAR(50),
  [LinkedId] BIGINT,
  [CurrentVersion] INT,
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Evidence_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Evidence_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Evidence_OrganizationId] ON [Evidence] ([OrganizationId])
-- go
CREATE INDEX [IX_Evidence_LinkedEntity_LinkedId] ON [Evidence] ([LinkedEntity], [LinkedId])
-- go
CREATE TABLE [EvidenceVersions] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_EvidenceVersions] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [EvidenceId] BIGINT,
  [Version] INT,
  [FileName] NVARCHAR(260),
  [ContentType] NVARCHAR(150),
  [SizeBytes] BIGINT,
  [Sha256] NVARCHAR(64),
  [StoragePath] NVARCHAR(500),
  [Comment] NVARCHAR(500),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  CONSTRAINT [FK_EvidenceVersions_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_EvidenceVersions_Evidence] FOREIGN KEY ([EvidenceId]) REFERENCES [Evidence] ([Id])
)
-- go
CREATE INDEX [IX_EvidenceVersions_OrganizationId] ON [EvidenceVersions] ([OrganizationId])
-- go
CREATE INDEX [IX_EvidenceVersions_EvidenceId] ON [EvidenceVersions] ([EvidenceId])
-- go
CREATE TABLE [DataInventory] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DataInventory] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [ProcessingActivity] NVARCHAR(300),
  [Department] NVARCHAR(100),
  [BusinessOwner] NVARCHAR(150),
  [PrincipalCategory] NVARCHAR(300),
  [DataCategories] NVARCHAR(MAX),
  [IncludesChildrenData] BIT,
  [CollectionSource] NVARCHAR(300),
  [Purpose] NVARCHAR(MAX),
  [LegalGround] NVARCHAR(120),
  [ApproxVolume] NVARCHAR(100),
  [StorageLocation] NVARCHAR(300),
  [SystemName] NVARCHAR(300),
  [CloudProvider] NVARCHAR(150),
  [AccessRoles] NVARCHAR(500),
  [InternalRecipients] NVARCHAR(500),
  [ExternalProcessors] NVARCHAR(500),
  [CrossBorder] BIT,
  [CrossBorderDetails] NVARCHAR(500),
  [RetentionPeriod] NVARCHAR(200),
  [ErasureMethod] NVARCHAR(300),
  [SecuritySafeguards] NVARCHAR(MAX),
  [LastReviewed] NVARCHAR(30),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_DataInventory_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_DataInventory_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_DataInventory_OrganizationId] ON [DataInventory] ([OrganizationId])
-- go
CREATE TABLE [DataFlows] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DataFlows] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Name] NVARCHAR(300),
  [SourceSystem] NVARCHAR(300),
  [DestinationSystem] NVARCHAR(300),
  [DestinationType] NVARCHAR(100),
  [DataCategories] NVARCHAR(500),
  [TransferMethod] NVARCHAR(100),
  [Encrypted] BIT,
  [CrossBorder] BIT,
  [Country] NVARCHAR(100),
  [Frequency] NVARCHAR(100),
  [Processor] NVARCHAR(200),
  [Notes] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_DataFlows_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_DataFlows_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_DataFlows_OrganizationId] ON [DataFlows] ([OrganizationId])
-- go
CREATE TABLE [Dpias] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Dpias] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [ProcessingActivity] NVARCHAR(300),
  [Trigger] NVARCHAR(120),
  [Purpose] NVARCHAR(MAX),
  [RightsDescription] NVARCHAR(MAX),
  [DataCategories] NVARCHAR(MAX),
  [PrincipalCategories] NVARCHAR(MAX),
  [Volume] NVARCHAR(200),
  [Systems] NVARCHAR(MAX),
  [Necessity] NVARCHAR(MAX),
  [Status] NVARCHAR(30),
  [OverallResidualRisk] NVARCHAR(20),
  [Owner] NVARCHAR(150),
  [Approver] NVARCHAR(150),
  [Conclusion] NVARCHAR(MAX),
  [ApprovedAt] DATETIME2(3),
  [ApprovedBy] NVARCHAR(150),
  [NextReview] DATE,
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Dpias_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Dpias_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Dpias_OrganizationId] ON [Dpias] ([OrganizationId])
-- go
CREATE TABLE [DpiaRisks] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DpiaRisks] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [DpiaId] BIGINT,
  [RiskType] NVARCHAR(100),
  [Description] NVARCHAR(MAX),
  [Likelihood] NVARCHAR(2),
  [Impact] NVARCHAR(2),
  [InherentScore] INT,
  [InherentRating] NVARCHAR(20),
  [ExistingControls] NVARCHAR(MAX),
  [ResidualLikelihood] NVARCHAR(2),
  [ResidualImpact] NVARCHAR(2),
  [ResidualScore] INT,
  [ResidualRating] NVARCHAR(20),
  [MitigationPlan] NVARCHAR(MAX),
  [Owner] NVARCHAR(150),
  [DueDate] DATE,
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_DpiaRisks_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_DpiaRisks_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_DpiaRisks_Dpias] FOREIGN KEY ([DpiaId]) REFERENCES [Dpias] ([Id])
)
-- go
CREATE INDEX [IX_DpiaRisks_OrganizationId] ON [DpiaRisks] ([OrganizationId])
-- go
CREATE INDEX [IX_DpiaRisks_DpiaId] ON [DpiaRisks] ([DpiaId])
-- go
CREATE TABLE [ConsentNotices] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ConsentNotices] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Title] NVARCHAR(300),
  [Version] NVARCHAR(20),
  [Language] NVARCHAR(40),
  [Purposes] NVARCHAR(MAX),
  [DataItems] NVARCHAR(MAX),
  [NoticeText] NVARCHAR(MAX),
  [WithdrawalLink] NVARCHAR(300),
  [GrievanceContact] NVARCHAR(300),
  [BoardComplaintInfo] NVARCHAR(300),
  [CollectionPoint] NVARCHAR(300),
  [Status] NVARCHAR(20),
  [PublishedAt] DATETIME2(3),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ConsentNotices_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_ConsentNotices_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_ConsentNotices_OrganizationId] ON [ConsentNotices] ([OrganizationId])
-- go
CREATE TABLE [ConsentRecords] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ConsentRecords] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [PrincipalRef] NVARCHAR(200),
  [IdentifierType] NVARCHAR(40),
  [NoticeId] BIGINT,
  [NoticeVersion] NVARCHAR(20),
  [Purpose] NVARCHAR(300),
  [Language] NVARCHAR(40),
  [Status] NVARCHAR(20),
  [GivenAt] DATETIME2(3),
  [WithdrawnAt] DATETIME2(3),
  [Channel] NVARCHAR(40),
  [ConsentManagerId] NVARCHAR(100),
  [EvidenceRef] NVARCHAR(300),
  [IsParentalConsent] BIT,
  [SourceIp] NVARCHAR(64),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ConsentRecords_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_ConsentRecords_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_ConsentRecords_OrganizationId] ON [ConsentRecords] ([OrganizationId])
-- go
CREATE INDEX [IX_ConsentRecords_PrincipalRef] ON [ConsentRecords] ([PrincipalRef])
-- go
CREATE TABLE [ConsentEvents] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ConsentEvents] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [ConsentRecordId] BIGINT,
  [EventType] NVARCHAR(50),
  [Details] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  CONSTRAINT [FK_ConsentEvents_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_ConsentEvents_ConsentRecords] FOREIGN KEY ([ConsentRecordId]) REFERENCES [ConsentRecords] ([Id])
)
-- go
CREATE INDEX [IX_ConsentEvents_OrganizationId] ON [ConsentEvents] ([OrganizationId])
-- go
CREATE INDEX [IX_ConsentEvents_ConsentRecordId] ON [ConsentEvents] ([ConsentRecordId])
-- go
CREATE TABLE [Policies] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Policies] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Title] NVARCHAR(300),
  [TemplateKey] NVARCHAR(60),
  [Version] NVARCHAR(20),
  [Source] NVARCHAR(60),
  [Status] NVARCHAR(60),
  [Content] NVARCHAR(MAX),
  [BasedOnPolicyId] BIGINT,
  [OriginalFileName] NVARCHAR(260),
  [ApprovedBy] NVARCHAR(150),
  [ApprovedAt] DATETIME2(3),
  [ReviewNotes] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Policies_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Policies_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Policies_OrganizationId] ON [Policies] ([OrganizationId])
-- go
CREATE TABLE [PolicyRecommendations] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PolicyRecommendations] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [PolicyId] BIGINT,
  [Topic] NVARCHAR(200),
  [ExistingClause] NVARCHAR(MAX),
  [RecommendedModification] NVARCHAR(MAX),
  [Reason] NVARCHAR(MAX),
  [LegalReference] NVARCHAR(200),
  [RevisedClause] NVARCHAR(MAX),
  [ApprovalStatus] NVARCHAR(20),
  [ReviewedBy] NVARCHAR(150),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_PolicyRecommendations_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_PolicyRecommendations_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_PolicyRecommendations_Policies] FOREIGN KEY ([PolicyId]) REFERENCES [Policies] ([Id])
)
-- go
CREATE INDEX [IX_PolicyRecommendations_OrganizationId] ON [PolicyRecommendations] ([OrganizationId])
-- go
CREATE INDEX [IX_PolicyRecommendations_PolicyId] ON [PolicyRecommendations] ([PolicyId])
-- go
CREATE TABLE [Vendors] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Vendors] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Name] NVARCHAR(200),
  [Category] NVARCHAR(60),
  [ContactPerson] NVARCHAR(150),
  [ContactEmail] NVARCHAR(200),
  [Services] NVARCHAR(MAX),
  [IsDataProcessor] BIT,
  [PersonalDataAccessed] NVARCHAR(MAX),
  [ProcessingPurpose] NVARCHAR(MAX),
  [StorageLocation] NVARCHAR(300),
  [CrossBorder] BIT,
  [SubProcessors] NVARCHAR(MAX),
  [SecurityCertifications] NVARCHAR(300),
  [ContractStatus] NVARCHAR(40),
  [DpaStatus] NVARCHAR(40),
  [ContractExpiry] DATE,
  [SecurityClause] BIT,
  [BreachNotificationClause] BIT,
  [BreachNotificationHours] INT,
  [ErasureClause] BIT,
  [AuditRights] BIT,
  [RetentionRequirements] NVARCHAR(300),
  [RiskRating] NVARCHAR(20),
  [LastAssessed] DATE,
  [NextReview] DATE,
  [Notes] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Vendors_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Vendors_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Vendors_OrganizationId] ON [Vendors] ([OrganizationId])
-- go
CREATE TABLE [VendorAssessments] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_VendorAssessments] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [VendorId] BIGINT,
  [AssessmentDate] DATE,
  [Assessor] NVARCHAR(150),
  [Score] INT,
  [RiskRating] NVARCHAR(20),
  [Findings] NVARCHAR(MAX),
  [Actions] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_VendorAssessments_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_VendorAssessments_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_VendorAssessments_Vendors] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id])
)
-- go
CREATE INDEX [IX_VendorAssessments_OrganizationId] ON [VendorAssessments] ([OrganizationId])
-- go
CREATE INDEX [IX_VendorAssessments_VendorId] ON [VendorAssessments] ([VendorId])
-- go
CREATE TABLE [SecurityControls] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SecurityControls] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [ControlKey] NVARCHAR(20),
  [Domain] NVARCHAR(100),
  [ControlName] NVARCHAR(200),
  [RuleReference] NVARCHAR(100),
  [Expectation] NVARCHAR(MAX),
  [Status] NVARCHAR(30),
  [ToolsMapped] NVARCHAR(500),
  [ConfigurationEvidence] NVARCHAR(MAX),
  [Validated] BIT,
  [ValidatedBy] NVARCHAR(150),
  [ValidatedAt] DATETIME2(3),
  [RiskRating] NVARCHAR(20),
  [Gap] NVARCHAR(MAX),
  [Recommendation] NVARCHAR(MAX),
  [Owner] NVARCHAR(150),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_SecurityControls_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_SecurityControls_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_SecurityControls_OrganizationId] ON [SecurityControls] ([OrganizationId])
-- go
CREATE TABLE [RightsRequests] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RightsRequests] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Reference] NVARCHAR(40),
  [RequestType] NVARCHAR(60),
  [PrincipalName] NVARCHAR(200),
  [PrincipalContact] NVARCHAR(200),
  [PrincipalIdentifier] NVARCHAR(200),
  [Channel] NVARCHAR(40),
  [ReceivedAt] DATETIME2(3),
  [Details] NVARCHAR(MAX),
  [VerificationStatus] NVARCHAR(20),
  [VerifiedAt] DATETIME2(3),
  [AssignedTo] NVARCHAR(150),
  [DueAt] DATETIME2(3),
  [Status] NVARCHAR(40),
  [Response] NVARCHAR(MAX),
  [RejectionReason] NVARCHAR(MAX),
  [ClosedAt] DATETIME2(3),
  [ClosureEvidence] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_RightsRequests_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_RightsRequests_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_RightsRequests_OrganizationId] ON [RightsRequests] ([OrganizationId])
-- go
CREATE TABLE [RequestEvents] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RequestEvents] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [RequestId] BIGINT,
  [Action] NVARCHAR(100),
  [Notes] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  CONSTRAINT [FK_RequestEvents_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_RequestEvents_RightsRequests] FOREIGN KEY ([RequestId]) REFERENCES [RightsRequests] ([Id])
)
-- go
CREATE INDEX [IX_RequestEvents_OrganizationId] ON [RequestEvents] ([OrganizationId])
-- go
CREATE INDEX [IX_RequestEvents_RequestId] ON [RequestEvents] ([RequestId])
-- go
CREATE TABLE [Incidents] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Incidents] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Reference] NVARCHAR(40),
  [Title] NVARCHAR(300),
  [DiscoveredAt] DATETIME2(3),
  [OccurredAt] DATETIME2(3),
  [DetectionSource] NVARCHAR(60),
  [Severity] NVARCHAR(20),
  [Description] NVARCHAR(MAX),
  [SystemsAffected] NVARCHAR(MAX),
  [DataCategoriesAffected] NVARCHAR(MAX),
  [AffectedCount] INT,
  [Location] NVARCHAR(200),
  [LikelyImpact] NVARCHAR(MAX),
  [IsPersonalDataBreach] BIT,
  [CertInReportable] BIT,
  [SectoralReportable] BIT,
  [SectoralRegulator] NVARCHAR(100),
  [RootCause] NVARCHAR(MAX),
  [PersonResponsible] NVARCHAR(MAX),
  [ContainmentActions] NVARCHAR(MAX),
  [Remediation] NVARCHAR(MAX),
  [PreventiveMeasures] NVARCHAR(MAX),
  [Owner] NVARCHAR(150),
  [Status] NVARCHAR(30),
  [LegalApproval] NVARCHAR(20),
  [ManagementApproval] NVARCHAR(20),
  [ClosedAt] DATETIME2(3),
  [IsDrill] BIT,
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Incidents_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Incidents_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Incidents_OrganizationId] ON [Incidents] ([OrganizationId])
-- go
CREATE TABLE [IncidentNotifications] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_IncidentNotifications] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [IncidentId] BIGINT,
  [RuleKey] NVARCHAR(40),
  [RecipientName] NVARCHAR(200),
  [Basis] NVARCHAR(300),
  [DueAt] DATETIME2(3),
  [SentAt] DATETIME2(3),
  [Status] NVARCHAR(30),
  [Method] NVARCHAR(40),
  [SubmissionReference] NVARCHAR(200),
  [Content] NVARCHAR(MAX),
  [Notes] NVARCHAR(MAX),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_IncidentNotifications_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_IncidentNotifications_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_IncidentNotifications_Incidents] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id])
)
-- go
CREATE INDEX [IX_IncidentNotifications_OrganizationId] ON [IncidentNotifications] ([OrganizationId])
-- go
CREATE INDEX [IX_IncidentNotifications_IncidentId] ON [IncidentNotifications] ([IncidentId])
-- go
CREATE TABLE [IncidentEvents] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_IncidentEvents] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [IncidentId] BIGINT,
  [EventAt] DATETIME2(3),
  [EventText] NVARCHAR(MAX),
  [EvidenceId] BIGINT,
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_IncidentEvents_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_IncidentEvents_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_IncidentEvents_Incidents] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id])
)
-- go
CREATE INDEX [IX_IncidentEvents_OrganizationId] ON [IncidentEvents] ([OrganizationId])
-- go
CREATE INDEX [IX_IncidentEvents_IncidentId] ON [IncidentEvents] ([IncidentId])
-- go
CREATE TABLE [Actions] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Actions] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [GapId] NVARCHAR(30),
  [RequirementRef] NVARCHAR(100),
  [Title] NVARCHAR(300),
  [Details] NVARCHAR(MAX),
  [SourceType] NVARCHAR(30),
  [SourceId] BIGINT,
  [RiskRating] NVARCHAR(20),
  [Priority] NVARCHAR(30),
  [Department] NVARCHAR(100),
  [Owner] NVARCHAR(150),
  [TargetDate] DATE,
  [Status] NVARCHAR(30),
  [Plan] NVARCHAR(20),
  [SupportingEvidence] NVARCHAR(MAX),
  [Approval] NVARCHAR(20),
  [ApprovedBy] NVARCHAR(150),
  [CompletedAt] DATETIME2(3),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Actions_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Actions_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Actions_OrganizationId] ON [Actions] ([OrganizationId])
-- go
CREATE INDEX [IX_Actions_Status] ON [Actions] ([Status])
-- go
CREATE INDEX [IX_Actions_SourceType_SourceId] ON [Actions] ([SourceType], [SourceId])
-- go
CREATE TABLE [Reports] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Reports] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [ReportType] NVARCHAR(60),
  [Title] NVARCHAR(300),
  [Version] NVARCHAR(20),
  [Status] NVARCHAR(20),
  [TemplateId] BIGINT,
  [AssessmentId] BIGINT,
  [ContentJson] NVARCHAR(MAX),
  [ApprovedBy] NVARCHAR(150),
  [ApprovedAt] DATETIME2(3),
  [PeriodFrom] DATE,
  [PeriodTo] DATE,
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Reports_IsDeleted] DEFAULT 0,
  CONSTRAINT [FK_Reports_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_Reports_OrganizationId] ON [Reports] ([OrganizationId])
-- go
CREATE TABLE [ReportFiles] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ReportFiles] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [ReportId] BIGINT,
  [Format] NVARCHAR(10),
  [FileName] NVARCHAR(260),
  [SizeBytes] BIGINT,
  [Sha256] NVARCHAR(64),
  [StoragePath] NVARCHAR(500),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  CONSTRAINT [FK_ReportFiles_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
  CONSTRAINT [FK_ReportFiles_Reports] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id])
)
-- go
CREATE INDEX [IX_ReportFiles_OrganizationId] ON [ReportFiles] ([OrganizationId])
-- go
CREATE INDEX [IX_ReportFiles_ReportId] ON [ReportFiles] ([ReportId])
-- go
CREATE TABLE [ReportTemplates] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ReportTemplates] PRIMARY KEY,
  [OrganizationId] BIGINT,
  [Name] NVARCHAR(200),
  [ReportType] NVARCHAR(60),
  [CoverDesign] NVARCHAR(50),
  [LogoPlacement] NVARCHAR(30),
  [Watermark] NVARCHAR(60),
  [FontFamily] NVARCHAR(50),
  [PrimaryColor] NVARCHAR(10),
  [SecondaryColor] NVARCHAR(10),
  [SectionsJson] NVARCHAR(MAX),
  [IsDefault] BIT,
  [IsSystem] BIT,
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ReportTemplates_IsDeleted] DEFAULT 0
)
-- go
CREATE TABLE [StoredFiles] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_StoredFiles] PRIMARY KEY,
  [OrganizationId] BIGINT,
  [Purpose] NVARCHAR(40),
  [FileName] NVARCHAR(260),
  [ContentType] NVARCHAR(150),
  [SizeBytes] BIGINT,
  [Sha256] NVARCHAR(64),
  [StoragePath] NVARCHAR(500),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200)
)
-- go
CREATE TABLE [ApiKeys] (
  [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ApiKeys] PRIMARY KEY,
  [OrganizationId] BIGINT NOT NULL,
  [Name] NVARCHAR(150),
  [Prefix] NVARCHAR(12),
  [KeyHash] NVARCHAR(64),
  [IsActive] BIT,
  [LastUsedAt] DATETIME2(3),
  [Scopes] NVARCHAR(200),
  [CreatedAt] DATETIME2(3) NOT NULL,
  [CreatedBy] NVARCHAR(200),
  [UpdatedAt] DATETIME2(3),
  [UpdatedBy] NVARCHAR(200),
  CONSTRAINT [FK_ApiKeys_Org] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
)
-- go
CREATE INDEX [IX_ApiKeys_OrganizationId] ON [ApiKeys] ([OrganizationId])
-- go
