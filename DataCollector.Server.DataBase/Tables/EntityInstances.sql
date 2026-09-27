CREATE TABLE [dbo].[EntityInstances]
(
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [EntityConfigId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,

    CONSTRAINT FK_EntityInstances_EntityConfigs
        FOREIGN KEY (EntityConfigId)
            REFERENCES EntityConfigs (Id)
)