CREATE PROCEDURE [dbo].[sp_EntityPropertyConfigs_CreateMany]
    @Properties [dbo].[EntityPropertyList] READONLY
AS
BEGIN
    INSERT INTO [dbo].[EntityPropertyConfigs] ([Id], [EntityConfigId], [Key], [Name], [DataType], [CreatedAt])
    SELECT [Id], [EntityConfigId], [Key], [Name], [DataType], [CreatedAt] FROM @Properties
END