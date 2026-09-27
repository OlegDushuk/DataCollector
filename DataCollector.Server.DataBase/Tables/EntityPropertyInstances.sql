CREATE TABLE [dbo].[EntityPropertyInstances]
(
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [EntityInstanceId] UNIQUEIDENTIFIER NOT NULL,
    [EntityPropertyConfigId] UNIQUEIDENTIFIER NOT NULL,
    [Value] NVARCHAR(4000) NULL,
    [CreatedAt] DATETIME2 NOT NULL,

    CONSTRAINT FK_EntityPropertyInstances_EntityInstances
        FOREIGN KEY (EntityInstanceId)
            REFERENCES EntityInstances (Id),

    CONSTRAINT FK_EntityPropertyInstances_EntityPropertyConfigs
        FOREIGN KEY (EntityPropertyConfigId)
            REFERENCES EntityPropertyConfigs (Id),

    CONSTRAINT UQ_EntityPropertyInstances_Instance_Property
        UNIQUE ([EntityInstanceId], [EntityPropertyConfigId])
)
GO

CREATE INDEX IX_EntityPropertyInstances_EntityPropertyConfigId
    ON [dbo].[EntityPropertyInstances] ([EntityPropertyConfigId])
    INCLUDE ([EntityInstanceId])
