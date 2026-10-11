-- Змінює ключ, назву та правила поля. Тип поля не змінюється.
-- Правила вже перевірені доменною моделлю; збережені значення записів не перевіряються повторно.
-- Помилки: 50404 - поле не знайдене, 50409 - ключ зайнятий іншим полем цієї моделі.
CREATE PROCEDURE [dbo].[sp_EntityPropertyConfigs_Update]
    @Id UNIQUEIDENTIFIER,
    @Key NVARCHAR(32),
    @Name NVARCHAR(128),
    @IsRequired BIT,
    @DefaultValue NVARCHAR(4000),
    @Settings NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Pkey INT, @ConfigPkey INT;

    SELECT @Pkey = [Pkey], @ConfigPkey = [ConfigPkey]
    FROM [dbo].[EntityPropertyConfigs]
    WHERE [Id] = @Id;

    IF @Pkey IS NULL
        THROW 50404, N'Поле не знайдене', 1;

    IF EXISTS (
        SELECT 1 FROM [dbo].[EntityPropertyConfigs]
        WHERE [ConfigPkey] = @ConfigPkey AND [Key] = @Key AND [Pkey] <> @Pkey)
        THROW 50409, N'Поле з таким ключем вже існує', 1;

    UPDATE [dbo].[EntityPropertyConfigs]
    SET [Key] = @Key,
        [Name] = @Name,
        [IsRequired] = @IsRequired,
        [DefaultValue] = @DefaultValue,
        [Settings] = @Settings
    WHERE [Pkey] = @Pkey;
END
