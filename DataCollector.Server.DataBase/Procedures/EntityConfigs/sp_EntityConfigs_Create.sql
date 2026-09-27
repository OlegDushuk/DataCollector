-- Створює модель разом з її полями в одній транзакції.
-- Помилки: 50409 - модель з таким ключем або назвою вже існує.
CREATE PROCEDURE [dbo].[sp_EntityConfigs_Create]
    @Id UNIQUEIDENTIFIER,
    @Key NVARCHAR(32),
    @Name NVARCHAR(32),
    @CreatedAt DATETIME2,
    @Properties [dbo].[EntityPropertyList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Key] = @Key)
        THROW 50409, N'Модель з таким ключем вже існує', 1;

    IF EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Name] = @Name)
        THROW 50409, N'Модель з такою назвою вже існує', 1;

    BEGIN TRANSACTION;

    INSERT INTO [dbo].[EntityConfigs] ([Id], [Key], [Name], [CreatedAt])
    VALUES (@Id, @Key, @Name, @CreatedAt);

    INSERT INTO [dbo].[EntityPropertyConfigs] ([Id], [EntityConfigId], [Key], [Name], [DataType], [CreatedAt])
    SELECT [Id], @Id, [Key], [Name], [DataType], [CreatedAt]
    FROM @Properties;

    COMMIT TRANSACTION;
END
