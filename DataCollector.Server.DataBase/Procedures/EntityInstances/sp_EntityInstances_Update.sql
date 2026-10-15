-- Оновлює значення переданих полів запису: нове значення вставляється або замінює старе,
-- рядок без жодного значення видаляє значення поля. Повертає нову версію запису.
-- @ExpectedVersion - версія, яку бачив клієнт (оптимістична конкурентність): якщо запис відтоді змінився,
-- зміна відхиляється. NULL - без перевірки.
-- Помилки: 50404 - запис не знайдений, 50409 - запис змінено після того, як його прочитали.
CREATE PROCEDURE [dbo].[sp_EntityInstances_Update]
    @Id UNIQUEIDENTIFIER,
    @UpdatedAt DATETIME2,
    @ExpectedVersion BIGINT = NULL,
    @Values [dbo].[PropertyValueList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Pkey BIGINT, @ConfigPkey INT;

    SELECT @Pkey = [Pkey], @ConfigPkey = [ConfigPkey]
    FROM [dbo].[EntityInstances]
    WHERE [Id] = @Id;

    IF @Pkey IS NULL
        THROW 50404, N'Запис не знайдений', 1;

    BEGIN TRANSACTION;

    -- Перевірка версії і зміна - одна інструкція: між ними ніхто не встигне змінити запис.
    UPDATE [dbo].[EntityInstances]
    SET [UpdatedAt] = @UpdatedAt
    WHERE [Pkey] = @Pkey
      AND (@ExpectedVersion IS NULL OR [RowVersion] = CAST(@ExpectedVersion AS BINARY(8)));

    IF @@ROWCOUNT = 0
        THROW 50409, N'Запис змінено іншим користувачем після того, як ви його відкрили. Оновіть дані й повторіть зміну', 1;

    WITH [Target] AS (
        SELECT *
        FROM [dbo].[EntityPropertyInstances]
        WHERE [InstancePkey] = @Pkey
    ),
    [Source] AS (
        SELECT p.[Pkey] AS [PropertyPkey], v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
        FROM @Values v
        INNER JOIN [dbo].[EntityPropertyConfigs] p
            ON p.[Id] = v.[EntityPropertyConfigId] AND p.[ConfigPkey] = @ConfigPkey
    )
    MERGE [Target] AS t
    USING [Source] AS s
        ON t.[PropertyPkey] = s.[PropertyPkey]
    WHEN MATCHED AND s.[ValueText] IS NULL AND s.[ValueNumber] IS NULL
                 AND s.[ValueBool] IS NULL AND s.[ValueDate] IS NULL THEN
        DELETE
    WHEN MATCHED THEN
        UPDATE SET
            t.[ValueText] = s.[ValueText],
            t.[ValueNumber] = s.[ValueNumber],
            t.[ValueBool] = s.[ValueBool],
            t.[ValueDate] = s.[ValueDate]
    WHEN NOT MATCHED BY TARGET AND (s.[ValueText] IS NOT NULL OR s.[ValueNumber] IS NOT NULL
                                    OR s.[ValueBool] IS NOT NULL OR s.[ValueDate] IS NOT NULL) THEN
        INSERT ([InstancePkey], [PropertyPkey], [ValueText], [ValueNumber], [ValueBool], [ValueDate])
        VALUES (@Pkey, s.[PropertyPkey], s.[ValueText], s.[ValueNumber], s.[ValueBool], s.[ValueDate]);

    COMMIT TRANSACTION;

    SELECT CAST([RowVersion] AS BIGINT) AS [Version]
    FROM [dbo].[EntityInstances]
    WHERE [Pkey] = @Pkey;
END
