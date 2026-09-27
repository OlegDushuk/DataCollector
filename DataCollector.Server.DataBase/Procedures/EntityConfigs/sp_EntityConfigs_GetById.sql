-- Повертає два набори: модель і її поля.
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

    SELECT [Id], [EntityConfigId], [Key], [Name], [DataType], [CreatedAt]
    FROM [dbo].[EntityPropertyConfigs]
    WHERE [EntityConfigId] = @Id
    ORDER BY [CreatedAt], [Key];
END
