SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.RoadmapProjects', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoadmapProjects (
        Id UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Slug NVARCHAR(50) NOT NULL,
        Version INT NOT NULL CONSTRAINT DF_RoadmapProjects_Version DEFAULT (0),
        CONSTRAINT PK_RoadmapProjects PRIMARY KEY CLUSTERED (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoadmapProjects_Slug' AND object_id = OBJECT_ID(N'dbo.RoadmapProjects'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_RoadmapProjects_Slug ON dbo.RoadmapProjects (Slug);
END;

IF OBJECT_ID(N'dbo.RoadmapProducts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoadmapProducts (
        Id UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_RoadmapProducts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoadmapProducts_RoadmapProjects_ProjectId
            FOREIGN KEY (ProjectId) REFERENCES dbo.RoadmapProjects (Id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoadmapProducts_ProjectId' AND object_id = OBJECT_ID(N'dbo.RoadmapProducts'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RoadmapProducts_ProjectId ON dbo.RoadmapProducts (ProjectId);
END;

IF OBJECT_ID(N'dbo.RoadmapDemands', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoadmapDemands (
        Id UNIQUEIDENTIFIER NOT NULL,
        ItemType NVARCHAR(50) NOT NULL,
        ParentDemandId UNIQUEIDENTIFIER NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NULL,
        ProjectId UNIQUEIDENTIFIER NULL,
        QuarterYear INT NOT NULL,
        QuarterNumber INT NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Type NVARCHAR(50) NOT NULL,
        Classification NVARCHAR(100) NOT NULL,
        SortOrder INT NOT NULL,
        Observation NVARCHAR(2000) NULL,
        DeprioritizationReason NVARCHAR(50) NULL,
        ReplacementDemandId UNIQUEIDENTIFIER NULL,
        JiraIssue NVARCHAR(100) NULL,
        IssueLinksJson NVARCHAR(MAX) NULL,
        Hours DECIMAL(18, 2) NULL,
        Customers NVARCHAR(MAX) NULL,
        IsBlocked BIT NOT NULL CONSTRAINT DF_RoadmapDemands_IsBlocked DEFAULT (0),
        BlockedReason NVARCHAR(500) NULL,
        PromisedDate DATE NULL,
        DeliveryDate DATE NULL,
        ProblemClarity INT NULL,
        HasNoKpi BIT NOT NULL CONSTRAINT DF_RoadmapDemands_HasNoKpi DEFAULT (0),
        NoKpiClassification NVARCHAR(50) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_RoadmapDemands_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NULL,
        Version INT NOT NULL CONSTRAINT DF_RoadmapDemands_Version DEFAULT (0),
        CONSTRAINT PK_RoadmapDemands PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoadmapDemands_RoadmapProjects_ProjectId
            FOREIGN KEY (ProjectId) REFERENCES dbo.RoadmapProjects (Id),
        CONSTRAINT FK_RoadmapDemands_RoadmapDemands_ParentDemandId
            FOREIGN KEY (ParentDemandId) REFERENCES dbo.RoadmapDemands (Id),
        CONSTRAINT FK_RoadmapDemands_RoadmapDemands_ReplacementDemandId
            FOREIGN KEY (ReplacementDemandId) REFERENCES dbo.RoadmapDemands (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoadmapDemands_ProjectId_QuarterYear_QuarterNumber_SortOrder' AND object_id = OBJECT_ID(N'dbo.RoadmapDemands'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RoadmapDemands_ProjectId_QuarterYear_QuarterNumber_SortOrder
        ON dbo.RoadmapDemands (ProjectId, QuarterYear, QuarterNumber, SortOrder);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoadmapDemands_ParentDemandId' AND object_id = OBJECT_ID(N'dbo.RoadmapDemands'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RoadmapDemands_ParentDemandId ON dbo.RoadmapDemands (ParentDemandId);
END;

IF OBJECT_ID(N'dbo.RoadmapDemandProducts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoadmapDemandProducts (
        Id UNIQUEIDENTIFIER NOT NULL,
        DemandId UNIQUEIDENTIFIER NOT NULL,
        ProductId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_RoadmapDemandProducts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoadmapDemandProducts_RoadmapDemands_DemandId
            FOREIGN KEY (DemandId) REFERENCES dbo.RoadmapDemands (Id) ON DELETE CASCADE,
        CONSTRAINT FK_RoadmapDemandProducts_RoadmapProducts_ProductId
            FOREIGN KEY (ProductId) REFERENCES dbo.RoadmapProducts (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoadmapDemandProducts_DemandId_ProductId' AND object_id = OBJECT_ID(N'dbo.RoadmapDemandProducts'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_RoadmapDemandProducts_DemandId_ProductId
        ON dbo.RoadmapDemandProducts (DemandId, ProductId);
END;

IF OBJECT_ID(N'dbo.RoadmapDemandProjects', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoadmapDemandProjects (
        Id UNIQUEIDENTIFIER NOT NULL,
        DemandId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_RoadmapDemandProjects PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoadmapDemandProjects_RoadmapDemands_DemandId
            FOREIGN KEY (DemandId) REFERENCES dbo.RoadmapDemands (Id) ON DELETE CASCADE,
        CONSTRAINT FK_RoadmapDemandProjects_RoadmapProjects_ProjectId
            FOREIGN KEY (ProjectId) REFERENCES dbo.RoadmapProjects (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoadmapDemandProjects_DemandId_ProjectId' AND object_id = OBJECT_ID(N'dbo.RoadmapDemandProjects'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_RoadmapDemandProjects_DemandId_ProjectId
        ON dbo.RoadmapDemandProjects (DemandId, ProjectId);
END;

IF OBJECT_ID(N'dbo.RoadmapDemandDependencies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoadmapDemandDependencies (
        Id UNIQUEIDENTIFIER NOT NULL,
        DemandId UNIQUEIDENTIFIER NOT NULL,
        DependsOnDemandId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_RoadmapDemandDependencies PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoadmapDemandDependencies_RoadmapDemands_DemandId
            FOREIGN KEY (DemandId) REFERENCES dbo.RoadmapDemands (Id) ON DELETE CASCADE,
        CONSTRAINT FK_RoadmapDemandDependencies_RoadmapDemands_DependsOnDemandId
            FOREIGN KEY (DependsOnDemandId) REFERENCES dbo.RoadmapDemands (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoadmapDemandDependencies_DemandId_DependsOnDemandId' AND object_id = OBJECT_ID(N'dbo.RoadmapDemandDependencies'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_RoadmapDemandDependencies_DemandId_DependsOnDemandId
        ON dbo.RoadmapDemandDependencies (DemandId, DependsOnDemandId);
END;

IF OBJECT_ID(N'dbo.RoadmapCapacities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoadmapCapacities (
        Id UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL,
        QuarterYear INT NOT NULL,
        QuarterNumber INT NOT NULL,
        CapacityHours DECIMAL(10, 2) NOT NULL,
        Observation NVARCHAR(2000) NULL,
        Version INT NOT NULL CONSTRAINT DF_RoadmapCapacities_Version DEFAULT (0),
        CONSTRAINT PK_RoadmapCapacities PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoadmapCapacities_RoadmapProjects_ProjectId
            FOREIGN KEY (ProjectId) REFERENCES dbo.RoadmapProjects (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoadmapCapacities_ProjectId_QuarterYear_QuarterNumber' AND object_id = OBJECT_ID(N'dbo.RoadmapCapacities'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_RoadmapCapacities_ProjectId_QuarterYear_QuarterNumber
        ON dbo.RoadmapCapacities (ProjectId, QuarterYear, QuarterNumber);
END;

IF OBJECT_ID(N'dbo.Kpis', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Kpis (
        Id UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NULL,
        Name NVARCHAR(200) NOT NULL,
        Type NVARCHAR(50) NOT NULL,
        Lever NVARCHAR(50) NOT NULL,
        Objective NVARCHAR(50) NOT NULL,
        Description NVARCHAR(2000) NULL,
        Calculation NVARCHAR(500) NULL,
        Target DECIMAL(18, 4) NULL,
        CurrentValue DECIMAL(18, 4) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Kpis_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NULL,
        Version INT NOT NULL CONSTRAINT DF_Kpis_Version DEFAULT (0),
        CONSTRAINT PK_Kpis PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Kpis_RoadmapProjects_ProjectId
            FOREIGN KEY (ProjectId) REFERENCES dbo.RoadmapProjects (Id)
    );
END;

IF COL_LENGTH(N'dbo.Kpis', N'ProjectId') IS NOT NULL
   AND EXISTS (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.Kpis')
         AND name = N'ProjectId'
         AND is_nullable = 0
   )
BEGIN
    ALTER TABLE dbo.Kpis DROP CONSTRAINT FK_Kpis_RoadmapProjects_ProjectId;
    ALTER TABLE dbo.Kpis ALTER COLUMN ProjectId UNIQUEIDENTIFIER NULL;
    ALTER TABLE dbo.Kpis ADD CONSTRAINT FK_Kpis_RoadmapProjects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.RoadmapProjects (Id);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Kpis_ProjectId' AND object_id = OBJECT_ID(N'dbo.Kpis'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Kpis_ProjectId ON dbo.Kpis (ProjectId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Kpis_ProjectId_Name' AND object_id = OBJECT_ID(N'dbo.Kpis'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_Kpis_ProjectId_Name ON dbo.Kpis (ProjectId, Name);
END;

IF OBJECT_ID(N'dbo.DemandKpiLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DemandKpiLinks (
        Id UNIQUEIDENTIFIER NOT NULL,
        DemandId UNIQUEIDENTIFIER NOT NULL,
        KpiId UNIQUEIDENTIFIER NOT NULL,
        ImpactType NVARCHAR(50) NOT NULL,
        EstimatedImpact DECIMAL(18, 4) NULL,
        ConfidenceLevel NVARCHAR(50) NOT NULL,
        Observation NVARCHAR(1000) NULL,
        MeasurementReferenceUrl NVARCHAR(2000) NULL,
        CONSTRAINT PK_DemandKpiLinks PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_DemandKpiLinks_RoadmapDemands_DemandId
            FOREIGN KEY (DemandId) REFERENCES dbo.RoadmapDemands (Id) ON DELETE CASCADE,
        CONSTRAINT FK_DemandKpiLinks_Kpis_KpiId
            FOREIGN KEY (KpiId) REFERENCES dbo.Kpis (Id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DemandKpiLinks_DemandId_KpiId' AND object_id = OBJECT_ID(N'dbo.DemandKpiLinks'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_DemandKpiLinks_DemandId_KpiId ON dbo.DemandKpiLinks (DemandId, KpiId);
END;

IF COL_LENGTH(N'dbo.DemandKpiLinks', N'MeasurementReferenceUrl') IS NULL
BEGIN
    ALTER TABLE dbo.DemandKpiLinks ADD MeasurementReferenceUrl NVARCHAR(2000) NULL;
END;

IF OBJECT_ID(N'dbo.KpiMeasurements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KpiMeasurements (
        Id UNIQUEIDENTIFIER NOT NULL,
        KpiId UNIQUEIDENTIFIER NOT NULL,
        DemandId UNIQUEIDENTIFIER NULL,
        MeasuredValue DECIMAL(18, 4) NOT NULL,
        MeasurementDate DATE NOT NULL,
        Result NVARCHAR(50) NOT NULL,
        Observation NVARCHAR(2000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_KpiMeasurements_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_KpiMeasurements PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_KpiMeasurements_Kpis_KpiId
            FOREIGN KEY (KpiId) REFERENCES dbo.Kpis (Id) ON DELETE CASCADE,
        CONSTRAINT FK_KpiMeasurements_RoadmapDemands_DemandId
            FOREIGN KEY (DemandId) REFERENCES dbo.RoadmapDemands (Id) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_KpiMeasurements_KpiId' AND object_id = OBJECT_ID(N'dbo.KpiMeasurements'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_KpiMeasurements_KpiId ON dbo.KpiMeasurements (KpiId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_KpiMeasurements_DemandId' AND object_id = OBJECT_ID(N'dbo.KpiMeasurements'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_KpiMeasurements_DemandId ON dbo.KpiMeasurements (DemandId);
END;

IF OBJECT_ID(N'dbo.DemandTradeOffs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DemandTradeOffs (
        Id UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL,
        QuarterYear INT NOT NULL,
        QuarterNumber INT NOT NULL,
        DeprioritizedDemandId UNIQUEIDENTIFIER NOT NULL,
        ReplacementDemandId UNIQUEIDENTIFIER NULL,
        Reason NVARCHAR(50) NOT NULL,
        Observation NVARCHAR(2000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_DemandTradeOffs_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_DemandTradeOffs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_DemandTradeOffs_RoadmapProjects_ProjectId
            FOREIGN KEY (ProjectId) REFERENCES dbo.RoadmapProjects (Id),
        CONSTRAINT FK_DemandTradeOffs_RoadmapDemands_DeprioritizedDemandId
            FOREIGN KEY (DeprioritizedDemandId) REFERENCES dbo.RoadmapDemands (Id) ON DELETE CASCADE,
        CONSTRAINT FK_DemandTradeOffs_RoadmapDemands_ReplacementDemandId
            FOREIGN KEY (ReplacementDemandId) REFERENCES dbo.RoadmapDemands (Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DemandTradeOffs_DeprioritizedDemandId' AND object_id = OBJECT_ID(N'dbo.DemandTradeOffs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DemandTradeOffs_DeprioritizedDemandId ON dbo.DemandTradeOffs (DeprioritizedDemandId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DemandTradeOffs_ProjectId_QuarterYear_QuarterNumber' AND object_id = OBJECT_ID(N'dbo.DemandTradeOffs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DemandTradeOffs_ProjectId_QuarterYear_QuarterNumber
        ON dbo.DemandTradeOffs (ProjectId, QuarterYear, QuarterNumber);
END;