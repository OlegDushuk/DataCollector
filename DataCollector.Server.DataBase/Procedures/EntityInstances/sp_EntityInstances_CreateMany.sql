-- Пакетне створення записів зі значеннями (імпорт). Порожні значення не зберігаються,
-- значення полів, що не належать моделі, ігноруються.
-- Виконується в транзакції клієнта (імпорт викликає процедуру частинами), помилка відкочує все.
-- Помилки: 50404 - модель не знайдена.
CREATE PROCEDURE [dbo].[sp_EntityInstances_CreateMany]
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

    DECLARE @Map TABLE ([Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, [Pkey] BIGINT NOT NULL);

    BEGIN TRANSACTION;

    -- Записи в хронологічному порядку: внутрішній ключ зростає разом з датою створення.
    INSERT INTO [dbo].[EntityInstances] ([Id], [ConfigPkey], [CreatedAt])
    OUTPUT inserted.[Id], inserted.[Pkey] INTO @Map ([Id], [Pkey])
    SELECT [Id], @ConfigPkey, [CreatedAt]
    FROM @Instances
    ORDER BY [CreatedAt], [Id];

    INSERT INTO [dbo].[EntityPropertyInstances]
        ([InstancePkey], [PropertyPkey], [ValueText], [ValueNumber], [ValueBool], [ValueDate])
    SELECT m.[Pkey], p.[Pkey], v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
    FROM @Values v
    INNER JOIN @Map m ON m.[Id] = v.[InstanceId]
    INNER JOIN [dbo].[EntityPropertyConfigs] p
        ON p.[Id] = v.[EntityPropertyConfigId] AND p.[ConfigPkey] = @ConfigPkey
    WHERE v.[ValueText] IS NOT NULL
       OR v.[ValueNumber] IS NOT NULL
       OR v.[ValueBool] IS NOT NULL
       OR v.[ValueDate] IS NOT NULL;

    COMMIT TRANSACTION;
END
