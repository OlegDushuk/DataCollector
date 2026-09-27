CREATE TYPE [dbo].[PropertyValueList]
AS TABLE (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [EntityPropertyConfigId] UNIQUEIDENTIFIER NOT NULL,
    [Value] NVARCHAR(4000) NULL,
    [CreatedAt] DATETIME2 NOT NULL
);
