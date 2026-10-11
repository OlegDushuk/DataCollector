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
        (SELECT COUNT(*) FROM [dbo].[EntityPropertyConfigs] p WHERE p.[ConfigPkey] = c.[Pkey]) AS [PropertyCount],
        (SELECT COUNT(*) FROM [dbo].[EntityInstances] i WHERE i.[ConfigPkey] = c.[Pkey]) AS [RecordCount]
    FROM [dbo].[EntityConfigs] c
    ORDER BY c.[CreatedAt] DESC;
END
