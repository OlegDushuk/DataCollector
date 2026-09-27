-- Помилки: 50404 - модель не знайдена, 50409 - ключ або назва зайняті іншою моделлю.
CREATE PROCEDURE [dbo].[sp_EntityConfigs_Update]
    @Id UNIQUEIDENTIFIER,
    @Key NVARCHAR(32),
    @Name NVARCHAR(32)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Id] = @Id)
        THROW 50404, N'Модель не знайдена', 1;

    IF EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Key] = @Key AND [Id] <> @Id)
        THROW 50409, N'Модель з таким ключем вже існує', 1;

    IF EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Name] = @Name AND [Id] <> @Id)
        THROW 50409, N'Модель з такою назвою вже існує', 1;

    UPDATE [dbo].[EntityConfigs]
    SET [Key] = @Key,
        [Name] = @Name
    WHERE [Id] = @Id;
END
