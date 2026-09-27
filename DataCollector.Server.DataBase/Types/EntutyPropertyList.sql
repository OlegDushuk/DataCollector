CREATE TYPE [dbo].[EntityPropertyList]
AS TABLE (
             [Id] UNIQUEIDENTIFIER,
             [EntityConfigId] UNIQUEIDENTIFIER,
             [Key] NVARCHAR(32),
             [Name] NVARCHAR(32),
             [DataType] INT,
             [CreatedAt] DATETIME2
         );