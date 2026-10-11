-- Додає поля до існуючої моделі.
-- Помилки: 50404 - модель не знайдена, 50409 - поле з таким ключем вже існує.
CREATE PROCEDURE [dbo].[sp_EntityPropertyConfigs_CreateMany]
    @Properties [dbo].[EntityPropertyList] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 FROM @Properties p
        WHERE NOT EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] c WHERE c.[Id] = p.[EntityConfigId]))
        THROW 50404, N'Модель не знайдена', 1;

    IF EXISTS (
        SELECT 1 FROM @Properties p
        INNER JOIN [dbo].[EntityConfigs] c ON c.[Id] = p.[EntityConfigId]
        INNER JOIN [dbo].[EntityPropertyConfigs] e ON e.[ConfigPkey] = c.[Pkey] AND e.[Key] = p.[Key])
        THROW 50409, N'Поле з таким ключем вже існує', 1;

    INSERT INTO [dbo].[EntityPropertyConfigs] ([Id], [ConfigPkey], [Key], [Name], [DataType], [CreatedAt])
    SELECT p.[Id], c.[Pkey], p.[Key], p.[Name], p.[DataType], p.[CreatedAt]
    FROM @Properties p
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Id] = p.[EntityConfigId];
END
