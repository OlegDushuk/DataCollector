-- Пакетне оновлення записів (імпорт): для переданих пар (запис, поле) старе значення замінюється новим,
-- рядок без жодного значення очищає поле. Інші поля записів не змінюються.
-- Виконується в транзакції клієнта (імпорт викликає процедуру частинами), помилка відкочує все.
-- Помилки: 50404 - модель або якийсь із записів не знайдено.
CREATE PROCEDURE [dbo].[sp_EntityInstances_UpdateMany]
    @EntityConfigId UNIQUEIDENTIFIER,
    @Instances [dbo].[InstanceList] READONLY,
    @Values [dbo].[InstanceValueList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @EntityConfigId);

    IF @ConfigPkey IS NULL
        THROW 50404, N'Модель не знайдена', 1;

    DECLARE @Map TABLE ([Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, [Pkey] BIGINT NOT NULL, [UpdatedAt] DATETIME2 NULL);

    INSERT INTO @Map ([Id], [Pkey], [UpdatedAt])
    SELECT x.[Id], i.[Pkey], x.[UpdatedAt]
    FROM @Instances x
    INNER JOIN [dbo].[EntityInstances] i ON i.[Id] = x.[Id] AND i.[ConfigPkey] = @ConfigPkey;

    IF (SELECT COUNT(*) FROM @Map) <> (SELECT COUNT(*) FROM @Instances)
        THROW 50404, N'Частину записів не знайдено - можливо, їх видалили під час імпорту', 1;

    DECLARE @Source TABLE (
        [InstancePkey] BIGINT NOT NULL,
        [PropertyPkey] INT NOT NULL,
        [ValueText] NVARCHAR(4000) NULL,
        [ValueNumber] DECIMAL(38, 10) NULL,
        [ValueBool] BIT NULL,
        [ValueDate] DATETIME2 NULL,
        PRIMARY KEY ([InstancePkey], [PropertyPkey])
    );

    INSERT INTO @Source
    SELECT m.[Pkey], p.[Pkey], v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
    FROM @Values v
    INNER JOIN @Map m ON m.[Id] = v.[InstanceId]
    INNER JOIN [dbo].[EntityPropertyConfigs] p
        ON p.[Id] = v.[EntityPropertyConfigId] AND p.[ConfigPkey] = @ConfigPkey;

    BEGIN TRANSACTION;

    UPDATE i
    SET [UpdatedAt] = COALESCE(m.[UpdatedAt], SYSUTCDATETIME())
    FROM [dbo].[EntityInstances] i
    INNER JOIN @Map m ON m.[Pkey] = i.[Pkey];

    -- Заміна = видалити старі значення переданих полів і вставити непорожні нові.
    DELETE v
    FROM [dbo].[EntityPropertyInstances] v
    INNER JOIN @Source s ON s.[InstancePkey] = v.[InstancePkey] AND s.[PropertyPkey] = v.[PropertyPkey];

    INSERT INTO [dbo].[EntityPropertyInstances]
        ([InstancePkey], [PropertyPkey], [ValueText], [ValueNumber], [ValueBool], [ValueDate])
    SELECT [InstancePkey], [PropertyPkey], [ValueText], [ValueNumber], [ValueBool], [ValueDate]
    FROM @Source
    WHERE [ValueText] IS NOT NULL
       OR [ValueNumber] IS NOT NULL
       OR [ValueBool] IS NOT NULL
       OR [ValueDate] IS NOT NULL;

    COMMIT TRANSACTION;
END
