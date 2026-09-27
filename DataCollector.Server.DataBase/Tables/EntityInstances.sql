CREATE TABLE [dbo].[EntityInstances]
(
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [EntityConfigId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,

    CONSTRAINT FK_EntityInstances_EntityConfigs
        FOREIGN KEY (EntityConfigId)
            REFERENCES EntityConfigs (Id)
)
GO

CREATE INDEX IX_EntityInstances_EntityConfigId_CreatedAt
    ON [dbo].[EntityInstances] ([EntityConfigId], [CreatedAt])
