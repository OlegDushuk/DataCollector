CREATE TABLE [dbo].[EntityPropertyConfigs]
(
    [Id] UNIQUEIDENTIFIER PRIMARY KEY,
    [EntityConfigId] UNIQUEIDENTIFIER NOT NULL,
    [Key] NVARCHAR(32) NOT NULL,
    [Name] NVARCHAR(32) NOT NULL,
    [DataType] INT NOT NULL DEFAULT 0,
    [CreatedAt] DATETIME2 NOT NULL,

    CONSTRAINT FK_EntityPropertyConfigs_EntityConfigs
        FOREIGN KEY (EntityConfigId)
            REFERENCES EntityConfigs (Id),

    CONSTRAINT UQ_EntityConfigId_Key
        UNIQUE ([EntityConfigId], [Key])
)