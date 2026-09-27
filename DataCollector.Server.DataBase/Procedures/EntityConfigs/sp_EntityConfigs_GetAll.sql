-- Список моделей з кількістю полів і записів.
CREATE PROCEDURE [dbo].[sp_EntityConfigs_GetAll]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.[Id],
        c.[Key],
        c.[Name],
        c.[CreatedAt],
        (SELECT COUNT(*) FROM [dbo].[EntityPropertyConfigs] p WHERE p.[EntityConfigId] = c.[Id]) AS [PropertyCount],
        (SELECT COUNT(*) FROM [dbo].[EntityInstances] i WHERE i.[EntityConfigId] = c.[Id]) AS [RecordCount]
    FROM [dbo].[EntityConfigs] c
    ORDER BY c.[CreatedAt] DESC;
END
