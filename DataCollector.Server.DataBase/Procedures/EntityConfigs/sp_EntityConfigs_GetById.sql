-- Повертає два набори: модель і її поля (ідентифікатори - зовнішні GUID).
CREATE PROCEDURE [dbo].[sp_EntityConfigs_GetById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.[Id],
        c.[Key],
        c.[Name],
        c.[CreatedAt]
    FROM [dbo].[EntityConfigs] c
    WHERE c.[Id] = @Id;

    SELECT p.[Id], c.[Id] AS [EntityConfigId], p.[Key], p.[Name], p.[DataType], p.[CreatedAt],
           p.[IsRequired], p.[DefaultValue], p.[Settings]
    FROM [dbo].[EntityPropertyConfigs] p
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = p.[ConfigPkey]
    WHERE c.[Id] = @Id
    ORDER BY p.[CreatedAt], p.[Key];
END
