IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] nvarchar(450) NULL,
        [Action] nvarchar(max) NOT NULL,
        [RecordType] nvarchar(128) NOT NULL,
        [RecordId] nvarchar(450) NOT NULL,
        [PreviousStatus] nvarchar(max) NULL,
        [NewStatus] nvarchar(max) NULL,
        [Reason] nvarchar(max) NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_AspNetUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [SupplierProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [CompanyName] nvarchar(256) NOT NULL,
        [TaxNumber] nvarchar(128) NOT NULL,
        [RegistrationNumber] nvarchar(128) NOT NULL,
        [ContactName] nvarchar(256) NOT NULL,
        [ContactEmail] nvarchar(256) NOT NULL,
        [ContactPhone] nvarchar(64) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [ReviewReason] nvarchar(max) NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        [ReviewedAt] datetimeoffset NULL,
        CONSTRAINT [PK_SupplierProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SupplierProfiles_Status] CHECK ([Status] IN (N'Pending', N'Approved', N'Rejected', N'Suspended')),
        CONSTRAINT [FK_SupplierProfiles_AspNetUsers_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_SupplierProfiles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [Tenders] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Terms] nvarchar(max) NOT NULL,
        [SubmissionStart] datetimeoffset NOT NULL,
        [SubmissionDeadline] datetimeoffset NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_Tenders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Tenders_Status] CHECK ([Status] IN (N'Draft', N'Published', N'Open', N'Closed', N'UnderEvaluation', N'AwaitingManagerReview', N'AwaitingCommitteeDecision', N'Awarded', N'Cancelled')),
        CONSTRAINT [FK_Tenders_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [PurchaseRequisitions] (
        [Id] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NOT NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [Priority] nvarchar(64) NOT NULL,
        [TargetDate] date NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [SubmittedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PurchaseRequisitions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PurchaseRequisitions_Status] CHECK ([Status] IN (N'Draft', N'Submitted', N'UnderReview', N'Approved', N'Rejected', N'Returned', N'ConvertedToTender')),
        CONSTRAINT [FK_PurchaseRequisitions_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_PurchaseRequisitions_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [SupplierDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [SupplierProfileId] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(max) NOT NULL,
        [OriginalName] nvarchar(512) NOT NULL,
        [StorageReference] nvarchar(2048) NOT NULL,
        [ContentType] nvarchar(256) NOT NULL,
        [Size] bigint NOT NULL,
        [ExpiresOn] date NULL,
        [UploadedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SupplierDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierDocuments_SupplierProfiles_SupplierProfileId] FOREIGN KEY ([SupplierProfileId]) REFERENCES [SupplierProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [AiBidAnalyses] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [RequestedByUserId] nvarchar(450) NOT NULL,
        [ModelName] nvarchar(256) NOT NULL,
        [InputReference] nvarchar(2048) NOT NULL,
        [AnalysisJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AiBidAnalyses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiBidAnalyses_AspNetUsers_RequestedByUserId] FOREIGN KEY ([RequestedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_AiBidAnalyses_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [CommitteeAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_CommitteeAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CommitteeAssignments_Id_TenderId] UNIQUE ([Id], [TenderId]),
        CONSTRAINT [CK_CommitteeAssignments_Kind] CHECK ([Kind] IN (N'Evaluation', N'TopCommittee')),
        CONSTRAINT [FK_CommitteeAssignments_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [EvaluationCriteria] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Kind] nvarchar(64) NOT NULL,
        [Weight] decimal(18,6) NOT NULL,
        [MaxScore] decimal(18,6) NULL,
        [ApprovedByUserId] nvarchar(450) NULL,
        [ApprovedAt] datetimeoffset NULL,
        CONSTRAINT [PK_EvaluationCriteria] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EvaluationCriteria_Id_TenderId] UNIQUE ([Id], [TenderId]),
        CONSTRAINT [CK_EvaluationCriteria_Kind] CHECK ([Kind] IN (N'Technical', N'Financial')),
        CONSTRAINT [FK_EvaluationCriteria_AspNetUsers_ApprovedByUserId] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_EvaluationCriteria_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [TenderSuppliers] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [SupplierProfileId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TenderSuppliers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TenderSuppliers_Id_TenderId] UNIQUE ([Id], [TenderId]),
        CONSTRAINT [FK_TenderSuppliers_SupplierProfiles_SupplierProfileId] FOREIGN KEY ([SupplierProfileId]) REFERENCES [SupplierProfiles] ([Id]),
        CONSTRAINT [FK_TenderSuppliers_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [PurchaseRequisitionItems] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseRequisitionId] uniqueidentifier NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Quantity] decimal(18,6) NOT NULL,
        [Specifications] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_PurchaseRequisitionItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseRequisitionItems_PurchaseRequisitions_PurchaseRequisitionId] FOREIGN KEY ([PurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [PurchaseRequisitionReviews] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseRequisitionId] uniqueidentifier NOT NULL,
        [ReviewerUserId] nvarchar(450) NOT NULL,
        [Outcome] nvarchar(64) NOT NULL,
        [Comments] nvarchar(max) NOT NULL,
        [ReviewedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PurchaseRequisitionReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PurchaseRequisitionReviews_Outcome] CHECK ([Outcome] IN (N'Draft', N'Submitted', N'UnderReview', N'Approved', N'Rejected', N'Returned', N'ConvertedToTender')),
        CONSTRAINT [FK_PurchaseRequisitionReviews_AspNetUsers_ReviewerUserId] FOREIGN KEY ([ReviewerUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_PurchaseRequisitionReviews_PurchaseRequisitions_PurchaseRequisitionId] FOREIGN KEY ([PurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [TenderPurchaseRequisitions] (
        [TenderId] uniqueidentifier NOT NULL,
        [PurchaseRequisitionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TenderPurchaseRequisitions] PRIMARY KEY ([TenderId], [PurchaseRequisitionId]),
        CONSTRAINT [FK_TenderPurchaseRequisitions_PurchaseRequisitions_PurchaseRequisitionId] FOREIGN KEY ([PurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]),
        CONSTRAINT [FK_TenderPurchaseRequisitions_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [CommitteeMembers] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [CommitteeAssignmentId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Position] nvarchar(128) NOT NULL,
        [EffectiveFrom] datetimeoffset NULL,
        [EffectiveTo] datetimeoffset NULL,
        CONSTRAINT [PK_CommitteeMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CommitteeMembers_Id_TenderId] UNIQUE ([Id], [TenderId]),
        CONSTRAINT [FK_CommitteeMembers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_CommitteeMembers_CommitteeAssignments_CommitteeAssignmentId_TenderId] FOREIGN KEY ([CommitteeAssignmentId], [TenderId]) REFERENCES [CommitteeAssignments] ([Id], [TenderId]),
        CONSTRAINT [FK_CommitteeMembers_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [Bids] (
        [Id] uniqueidentifier NOT NULL,
        [TenderSupplierId] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [DeliveryTimeframe] nvarchar(max) NOT NULL,
        [TechnicalDetails] nvarchar(max) NOT NULL,
        [Currency] nvarchar(16) NOT NULL,
        [PaymentTerms] nvarchar(max) NOT NULL,
        [OfferValidUntil] date NULL,
        [SubmittedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Bids] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Bids_Id_TenderId] UNIQUE ([Id], [TenderId]),
        CONSTRAINT [CK_Bids_Status] CHECK ([Status] IN (N'Draft', N'Submitted', N'UnderReview', N'Compliant', N'NonCompliant', N'Accepted', N'Rejected', N'Withdrawn')),
        CONSTRAINT [FK_Bids_TenderSuppliers_TenderSupplierId_TenderId] FOREIGN KEY ([TenderSupplierId], [TenderId]) REFERENCES [TenderSuppliers] ([Id], [TenderId]),
        CONSTRAINT [FK_Bids_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [Invitations] (
        [Id] uniqueidentifier NOT NULL,
        [TenderSupplierId] uniqueidentifier NOT NULL,
        [SentByUserId] nvarchar(450) NOT NULL,
        [SentAt] datetimeoffset NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [RespondedAt] datetimeoffset NULL,
        [ExpiresAt] datetimeoffset NULL,
        CONSTRAINT [PK_Invitations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Invitations_Status] CHECK ([Status] IN (N'Pending', N'Accepted', N'Declined', N'Expired')),
        CONSTRAINT [FK_Invitations_AspNetUsers_SentByUserId] FOREIGN KEY ([SentByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_Invitations_TenderSuppliers_TenderSupplierId] FOREIGN KEY ([TenderSupplierId]) REFERENCES [TenderSuppliers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [BidCostComponents] (
        [Id] uniqueidentifier NOT NULL,
        [BidId] uniqueidentifier NOT NULL,
        [CostType] nvarchar(128) NOT NULL,
        [Amount] decimal(18,6) NOT NULL,
        [Currency] nvarchar(16) NOT NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_BidCostComponents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BidCostComponents_Bids_BidId] FOREIGN KEY ([BidId]) REFERENCES [Bids] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [BidDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [BidId] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(max) NOT NULL,
        [OriginalName] nvarchar(512) NOT NULL,
        [StorageReference] nvarchar(2048) NOT NULL,
        [ContentType] nvarchar(256) NOT NULL,
        [Size] bigint NOT NULL,
        [UploadedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_BidDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BidDocuments_Bids_BidId] FOREIGN KEY ([BidId]) REFERENCES [Bids] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [BidItems] (
        [Id] uniqueidentifier NOT NULL,
        [BidId] uniqueidentifier NOT NULL,
        [PurchaseRequisitionItemId] uniqueidentifier NULL,
        [Description] nvarchar(max) NOT NULL,
        [OfferedQuantity] decimal(18,6) NOT NULL,
        [UnitPrice] decimal(18,6) NOT NULL,
        [Specifications] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_BidItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BidItems_Bids_BidId] FOREIGN KEY ([BidId]) REFERENCES [Bids] ([Id]),
        CONSTRAINT [FK_BidItems_PurchaseRequisitionItems_PurchaseRequisitionItemId] FOREIGN KEY ([PurchaseRequisitionItemId]) REFERENCES [PurchaseRequisitionItems] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [ClarificationRequests] (
        [Id] uniqueidentifier NOT NULL,
        [SenderUserId] nvarchar(450) NOT NULL,
        [RecipientUserId] nvarchar(450) NOT NULL,
        [PurchaseRequisitionId] uniqueidentifier NULL,
        [TenderId] uniqueidentifier NULL,
        [BidId] uniqueidentifier NULL,
        [Subject] nvarchar(256) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClarificationRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ClarificationRequests_OneRelatedRecord] CHECK ((CASE WHEN [PurchaseRequisitionId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TenderId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [BidId] IS NULL THEN 0 ELSE 1 END) = 1),
        CONSTRAINT [CK_ClarificationRequests_Status] CHECK ([Status] IN (N'Pending', N'Answered', N'Resolved')),
        CONSTRAINT [FK_ClarificationRequests_AspNetUsers_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ClarificationRequests_AspNetUsers_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ClarificationRequests_Bids_BidId] FOREIGN KEY ([BidId]) REFERENCES [Bids] ([Id]),
        CONSTRAINT [FK_ClarificationRequests_PurchaseRequisitions_PurchaseRequisitionId] FOREIGN KEY ([PurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]),
        CONSTRAINT [FK_ClarificationRequests_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [ComplianceReviews] (
        [Id] uniqueidentifier NOT NULL,
        [BidId] uniqueidentifier NOT NULL,
        [ReviewerUserId] nvarchar(450) NOT NULL,
        [Outcome] nvarchar(64) NOT NULL,
        [Reasons] nvarchar(max) NULL,
        [ReviewedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ComplianceReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ComplianceReviews_NonCompliantReason] CHECK ([Outcome] <> N'NonCompliant' OR ([Reasons] IS NOT NULL AND LEN(LTRIM(RTRIM([Reasons]))) > 0)),
        CONSTRAINT [CK_ComplianceReviews_Outcome] CHECK ([Outcome] IN (N'Compliant', N'NonCompliant', N'PendingClarification')),
        CONSTRAINT [FK_ComplianceReviews_AspNetUsers_ReviewerUserId] FOREIGN KEY ([ReviewerUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ComplianceReviews_Bids_BidId] FOREIGN KEY ([BidId]) REFERENCES [Bids] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [EvaluationReports] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [PreparedByUserId] nvarchar(450) NOT NULL,
        [RecommendedBidId] uniqueidentifier NULL,
        [TechnicalSummary] nvarchar(max) NOT NULL,
        [FinancialSummary] nvarchar(max) NOT NULL,
        [Observations] nvarchar(max) NOT NULL,
        [NonCompliance] nvarchar(max) NOT NULL,
        [Comparison] nvarchar(max) NOT NULL,
        [FinalResult] nvarchar(max) NOT NULL,
        [Recommendation] nvarchar(max) NOT NULL,
        [SubmittedAt] datetimeoffset NULL,
        CONSTRAINT [PK_EvaluationReports] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EvaluationReports_Id_TenderId] UNIQUE ([Id], [TenderId]),
        CONSTRAINT [FK_EvaluationReports_AspNetUsers_PreparedByUserId] FOREIGN KEY ([PreparedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_EvaluationReports_Bids_RecommendedBidId_TenderId] FOREIGN KEY ([RecommendedBidId], [TenderId]) REFERENCES [Bids] ([Id], [TenderId]),
        CONSTRAINT [FK_EvaluationReports_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [FinancialEvaluations] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [BidId] uniqueidentifier NOT NULL,
        [EvaluationCriterionId] uniqueidentifier NOT NULL,
        [RecordedByUserId] nvarchar(450) NOT NULL,
        [EvaluatorUserId] nvarchar(450) NULL,
        [Score] decimal(18,6) NOT NULL,
        [Notes] nvarchar(max) NOT NULL,
        [EvaluatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_FinancialEvaluations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinancialEvaluations_AspNetUsers_EvaluatorUserId] FOREIGN KEY ([EvaluatorUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_FinancialEvaluations_AspNetUsers_RecordedByUserId] FOREIGN KEY ([RecordedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_FinancialEvaluations_Bids_BidId_TenderId] FOREIGN KEY ([BidId], [TenderId]) REFERENCES [Bids] ([Id], [TenderId]),
        CONSTRAINT [FK_FinancialEvaluations_EvaluationCriteria_EvaluationCriterionId_TenderId] FOREIGN KEY ([EvaluationCriterionId], [TenderId]) REFERENCES [EvaluationCriteria] ([Id], [TenderId]),
        CONSTRAINT [FK_FinancialEvaluations_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [RecipientUserId] nvarchar(450) NOT NULL,
        [PurchaseRequisitionId] uniqueidentifier NULL,
        [TenderId] uniqueidentifier NULL,
        [BidId] uniqueidentifier NULL,
        [Subject] nvarchar(256) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ReadAt] datetimeoffset NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_AspNetUsers_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_Notifications_Bids_BidId] FOREIGN KEY ([BidId]) REFERENCES [Bids] ([Id]),
        CONSTRAINT [FK_Notifications_PurchaseRequisitions_PurchaseRequisitionId] FOREIGN KEY ([PurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]),
        CONSTRAINT [FK_Notifications_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [TechnicalEvaluations] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [BidId] uniqueidentifier NOT NULL,
        [EvaluationCriterionId] uniqueidentifier NOT NULL,
        [RecordedByUserId] nvarchar(450) NOT NULL,
        [EvaluatorUserId] nvarchar(450) NULL,
        [Score] decimal(18,6) NOT NULL,
        [Notes] nvarchar(max) NOT NULL,
        [NonCompliance] nvarchar(max) NULL,
        [EvaluatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TechnicalEvaluations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TechnicalEvaluations_AspNetUsers_EvaluatorUserId] FOREIGN KEY ([EvaluatorUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_TechnicalEvaluations_AspNetUsers_RecordedByUserId] FOREIGN KEY ([RecordedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_TechnicalEvaluations_Bids_BidId_TenderId] FOREIGN KEY ([BidId], [TenderId]) REFERENCES [Bids] ([Id], [TenderId]),
        CONSTRAINT [FK_TechnicalEvaluations_EvaluationCriteria_EvaluationCriterionId_TenderId] FOREIGN KEY ([EvaluationCriterionId], [TenderId]) REFERENCES [EvaluationCriteria] ([Id], [TenderId]),
        CONSTRAINT [FK_TechnicalEvaluations_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [ClarificationResponses] (
        [Id] uniqueidentifier NOT NULL,
        [ClarificationRequestId] uniqueidentifier NOT NULL,
        [ResponderUserId] nvarchar(450) NOT NULL,
        [Response] nvarchar(max) NOT NULL,
        [RespondedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClarificationResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClarificationResponses_AspNetUsers_ResponderUserId] FOREIGN KEY ([ResponderUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ClarificationResponses_ClarificationRequests_ClarificationRequestId] FOREIGN KEY ([ClarificationRequestId]) REFERENCES [ClarificationRequests] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [TenderApprovals] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [EvaluationReportId] uniqueidentifier NOT NULL,
        [Stage] nvarchar(64) NOT NULL,
        [Decision] nvarchar(64) NOT NULL,
        [ActorUserId] nvarchar(450) NULL,
        [CommitteeAssignmentId] uniqueidentifier NULL,
        [Comments] nvarchar(max) NOT NULL,
        [Justification] nvarchar(max) NOT NULL,
        [DecidedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenderApprovals] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TenderApprovals_Id_TenderId] UNIQUE ([Id], [TenderId]),
        CONSTRAINT [CK_TenderApprovals_Decision] CHECK ([Decision] IN (N'RecommendationPending', N'ManagerApproved', N'ManagerRejected', N'ReturnedForReview', N'CommitteeApproved', N'CommitteeRejected')),
        CONSTRAINT [CK_TenderApprovals_Stage] CHECK ([Stage] IN (N'Manager', N'TopCommittee')),
        CONSTRAINT [FK_TenderApprovals_AspNetUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_TenderApprovals_CommitteeAssignments_CommitteeAssignmentId_TenderId] FOREIGN KEY ([CommitteeAssignmentId], [TenderId]) REFERENCES [CommitteeAssignments] ([Id], [TenderId]),
        CONSTRAINT [FK_TenderApprovals_EvaluationReports_EvaluationReportId_TenderId] FOREIGN KEY ([EvaluationReportId], [TenderId]) REFERENCES [EvaluationReports] ([Id], [TenderId]),
        CONSTRAINT [FK_TenderApprovals_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [CommitteeVotes] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [TenderApprovalId] uniqueidentifier NOT NULL,
        [CommitteeMemberId] uniqueidentifier NOT NULL,
        [Decision] nvarchar(max) NOT NULL,
        [Justification] nvarchar(max) NOT NULL,
        [VotedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CommitteeVotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommitteeVotes_CommitteeMembers_CommitteeMemberId_TenderId] FOREIGN KEY ([CommitteeMemberId], [TenderId]) REFERENCES [CommitteeMembers] ([Id], [TenderId]),
        CONSTRAINT [FK_CommitteeVotes_TenderApprovals_TenderApprovalId_TenderId] FOREIGN KEY ([TenderApprovalId], [TenderId]) REFERENCES [TenderApprovals] ([Id], [TenderId]),
        CONSTRAINT [FK_CommitteeVotes_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE TABLE [TenderAwards] (
        [Id] uniqueidentifier NOT NULL,
        [TenderId] uniqueidentifier NOT NULL,
        [BidId] uniqueidentifier NOT NULL,
        [TenderApprovalId] uniqueidentifier NOT NULL,
        [RecordedByUserId] nvarchar(450) NOT NULL,
        [AwardedAt] datetimeoffset NOT NULL,
        [ContractReference] nvarchar(512) NULL,
        CONSTRAINT [PK_TenderAwards] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenderAwards_AspNetUsers_RecordedByUserId] FOREIGN KEY ([RecordedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_TenderAwards_Bids_BidId_TenderId] FOREIGN KEY ([BidId], [TenderId]) REFERENCES [Bids] ([Id], [TenderId]),
        CONSTRAINT [FK_TenderAwards_TenderApprovals_TenderApprovalId_TenderId] FOREIGN KEY ([TenderApprovalId], [TenderId]) REFERENCES [TenderApprovals] ([Id], [TenderId]),
        CONSTRAINT [FK_TenderAwards_Tenders_TenderId] FOREIGN KEY ([TenderId]) REFERENCES [Tenders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_AiBidAnalyses_RequestedByUserId] ON [AiBidAnalyses] ([RequestedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_AiBidAnalyses_TenderId] ON [AiBidAnalyses] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorUserId] ON [AuditLogs] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_RecordType_RecordId_OccurredAt] ON [AuditLogs] ([RecordType], [RecordId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_BidCostComponents_BidId] ON [BidCostComponents] ([BidId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_BidDocuments_BidId] ON [BidDocuments] ([BidId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_BidItems_BidId] ON [BidItems] ([BidId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_BidItems_PurchaseRequisitionItemId] ON [BidItems] ([PurchaseRequisitionItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Bids_TenderId] ON [Bids] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Bids_TenderSupplierId_TenderId] ON [Bids] ([TenderSupplierId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ClarificationRequests_BidId] ON [ClarificationRequests] ([BidId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ClarificationRequests_PurchaseRequisitionId] ON [ClarificationRequests] ([PurchaseRequisitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ClarificationRequests_RecipientUserId] ON [ClarificationRequests] ([RecipientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ClarificationRequests_SenderUserId] ON [ClarificationRequests] ([SenderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ClarificationRequests_TenderId] ON [ClarificationRequests] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ClarificationResponses_ClarificationRequestId] ON [ClarificationResponses] ([ClarificationRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ClarificationResponses_ResponderUserId] ON [ClarificationResponses] ([ResponderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_CommitteeAssignments_TenderId] ON [CommitteeAssignments] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_CommitteeMembers_CommitteeAssignmentId_TenderId] ON [CommitteeMembers] ([CommitteeAssignmentId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_CommitteeMembers_TenderId] ON [CommitteeMembers] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_CommitteeMembers_UserId] ON [CommitteeMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_CommitteeVotes_CommitteeMemberId_TenderId] ON [CommitteeVotes] ([CommitteeMemberId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_CommitteeVotes_TenderApprovalId_TenderId] ON [CommitteeVotes] ([TenderApprovalId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_CommitteeVotes_TenderId] ON [CommitteeVotes] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ComplianceReviews_BidId] ON [ComplianceReviews] ([BidId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_ComplianceReviews_ReviewerUserId] ON [ComplianceReviews] ([ReviewerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_EvaluationCriteria_ApprovedByUserId] ON [EvaluationCriteria] ([ApprovedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_EvaluationCriteria_TenderId] ON [EvaluationCriteria] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_EvaluationReports_PreparedByUserId] ON [EvaluationReports] ([PreparedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_EvaluationReports_RecommendedBidId_TenderId] ON [EvaluationReports] ([RecommendedBidId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_EvaluationReports_TenderId] ON [EvaluationReports] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_FinancialEvaluations_BidId_TenderId] ON [FinancialEvaluations] ([BidId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_FinancialEvaluations_EvaluationCriterionId_TenderId] ON [FinancialEvaluations] ([EvaluationCriterionId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_FinancialEvaluations_EvaluatorUserId] ON [FinancialEvaluations] ([EvaluatorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_FinancialEvaluations_RecordedByUserId] ON [FinancialEvaluations] ([RecordedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_FinancialEvaluations_TenderId] ON [FinancialEvaluations] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Invitations_SentByUserId] ON [Invitations] ([SentByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Invitations_TenderSupplierId] ON [Invitations] ([TenderSupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Notifications_BidId] ON [Notifications] ([BidId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Notifications_PurchaseRequisitionId] ON [Notifications] ([PurchaseRequisitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Notifications_RecipientUserId_CreatedAt] ON [Notifications] ([RecipientUserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Notifications_TenderId] ON [Notifications] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequisitionItems_PurchaseRequisitionId] ON [PurchaseRequisitionItems] ([PurchaseRequisitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequisitionReviews_PurchaseRequisitionId] ON [PurchaseRequisitionReviews] ([PurchaseRequisitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequisitionReviews_ReviewerUserId] ON [PurchaseRequisitionReviews] ([ReviewerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequisitions_CreatedByUserId] ON [PurchaseRequisitions] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequisitions_DepartmentId_Status] ON [PurchaseRequisitions] ([DepartmentId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_SupplierDocuments_SupplierProfileId] ON [SupplierDocuments] ([SupplierProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_SupplierProfiles_ReviewedByUserId] ON [SupplierProfiles] ([ReviewedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_SupplierProfiles_UserId] ON [SupplierProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TechnicalEvaluations_BidId_TenderId] ON [TechnicalEvaluations] ([BidId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TechnicalEvaluations_EvaluationCriterionId_TenderId] ON [TechnicalEvaluations] ([EvaluationCriterionId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TechnicalEvaluations_EvaluatorUserId] ON [TechnicalEvaluations] ([EvaluatorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TechnicalEvaluations_RecordedByUserId] ON [TechnicalEvaluations] ([RecordedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TechnicalEvaluations_TenderId] ON [TechnicalEvaluations] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderApprovals_ActorUserId] ON [TenderApprovals] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderApprovals_CommitteeAssignmentId_TenderId] ON [TenderApprovals] ([CommitteeAssignmentId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderApprovals_EvaluationReportId_TenderId] ON [TenderApprovals] ([EvaluationReportId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderApprovals_TenderId] ON [TenderApprovals] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderAwards_BidId_TenderId] ON [TenderAwards] ([BidId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderAwards_RecordedByUserId] ON [TenderAwards] ([RecordedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderAwards_TenderApprovalId_TenderId] ON [TenderAwards] ([TenderApprovalId], [TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderAwards_TenderId] ON [TenderAwards] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderPurchaseRequisitions_PurchaseRequisitionId] ON [TenderPurchaseRequisitions] ([PurchaseRequisitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Tenders_CreatedByUserId] ON [Tenders] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_Tenders_Status_SubmissionDeadline] ON [Tenders] ([Status], [SubmissionDeadline]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderSuppliers_SupplierProfileId] ON [TenderSuppliers] ([SupplierProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    CREATE INDEX [IX_TenderSuppliers_TenderId] ON [TenderSuppliers] ([TenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112224814_InitialIdentityAndProcurement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112224814_InitialIdentityAndProcurement', N'10.0.15');
END;

COMMIT;
GO

