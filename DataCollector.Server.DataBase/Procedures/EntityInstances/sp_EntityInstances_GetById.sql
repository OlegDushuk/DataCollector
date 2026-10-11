-- Повертає два набори: запис і його значення (ідентифікатори - зовнішні GUID).
CREATE PROCEDURE [dbo].[sp_EntityInstances_GetById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT i.[Id], c.[Id] AS [EntityConfigId], i.[CreatedAt], i.[UpdatedAt]
    FROM [dbo].[EntityInstances] i
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = i.[ConfigPkey]
    WHERE i.[Id] = @Id;

    SELECT
        i.[Id] AS [EntityInstanceId],
        p.[Id] AS [EntityPropertyConfigId],
        v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
    FROM [dbo].[EntityInstances] i
    INNER JOIN [dbo].[EntityPropertyInstances] v ON v.[InstancePkey] = i.[Pkey]
    INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey]
    WHERE i.[Id] = @Id;
END
