CREATE TYPE [dbo].[EntityPropertyList]
AS TABLE (
             [Id] UNIQUEIDENTIFIER,
             [EntityConfigId] UNIQUEIDENTIFIER,
             [Key] NVARCHAR(32),
             [Name] NVARCHAR(128),
             [DataType] INT,
             [CreatedAt] DATETIME2,
             [IsRequired] BIT,
             [DefaultValue] NVARCHAR(4000),
             [Settings] NVARCHAR(MAX)
         );