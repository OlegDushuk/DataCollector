-- Створює модель разом з її полями в одній транзакції.
-- Помилки: 50409 - модель з таким ключем вже існує.
CREATE PROCEDURE [dbo].[sp_EntityConfigs_Create]
    @Id UNIQUEIDENTIFIER,
    @Key NVARCHAR(32),
    @Name NVARCHAR(128),
    @CreatedAt DATETIME2,
    @Properties [dbo].[EntityPropertyList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Key] = @Key)
        THROW 50409, N'Модель з таким ключем вже існує', 1;

    BEGIN TRANSACTION;

    INSERT INTO [dbo].[EntityConfigs] ([Id], [Key], [Name], [CreatedAt])
    VALUES (@Id, @Key, @Name, @CreatedAt);

    DECLARE @Pkey INT = SCOPE_IDENTITY();

    INSERT INTO [dbo].[EntityPropertyConfigs]
        ([Id], [ConfigPkey], [Key], [Name], [DataType], [CreatedAt], [IsRequired], [DefaultValue], [Settings])
    SELECT [Id], @Pkey, [Key], [Name], [DataType], [CreatedAt], ISNULL([IsRequired], 0), [DefaultValue], [Settings]
    FROM @Properties;

    COMMIT TRANSACTION;
END
