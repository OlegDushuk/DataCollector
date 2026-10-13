-- Дашборди: іменовані набори віджетів (таблиць, зведень, діаграм, показників) для перегляду на одному екрані.
CREATE TABLE [dbo].[Dashboards]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(128) NOT NULL,
    [Description] NVARCHAR(512) NULL,
    -- Автооновлення перегляду, секунди; 0 - вимкнено.
    [RefreshSeconds] INT NOT NULL CONSTRAINT DF_Dashboards_RefreshSeconds DEFAULT (0),
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,

    CONSTRAINT PK_Dashboards
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_Dashboards_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT CK_Dashboards_RefreshSeconds
        CHECK ([RefreshSeconds] >= 0)
)
