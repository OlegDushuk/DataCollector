CREATE TABLE [dbo].[EntityConfigs]
(
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [CreatedAt] DATETIME2 NOT NULL,
    [Key] NVARCHAR(32) NOT NULL,
    [Name] NVARCHAR(32) NOT NULL,

    CONSTRAINT UQ_EntityConfigs_Key UNIQUE ([Key]),
    CONSTRAINT UQ_EntityConfigs_Name UNIQUE ([Name])
)
