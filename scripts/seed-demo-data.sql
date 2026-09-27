/*
    Тестові дані: модель "Замовлення магазину" (ключ shop_orders) з 6 полями
    і @RecordCount записів у ній.

    Значення пишуться у тому ж нормалізованому вигляді, що й через API:
      Number  -> інваріантний формат з крапкою ("1234.50")
      Boolean -> "true" / "false"
      порожнє -> NULL

    Скрипт можна запускати повторно: якщо модель з таким ключем уже є,
    вона видаляється разом з усіма записами і створюється заново.
    Для бенчмарку просто змініть @RecordCount (наприклад, 1000000).
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RecordCount INT = 10000;
DECLARE @ConfigKey NVARCHAR(32) = N'shop_orders';
DECLARE @ConfigName NVARCHAR(32) = N'Замовлення магазину';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

-------------------------------------------------------------------------------
-- 1. Видаляємо стару версію моделі (якщо є)
-------------------------------------------------------------------------------
DECLARE @OldConfigId UNIQUEIDENTIFIER = (SELECT [Id] FROM [dbo].[EntityConfigs] WHERE [Key] = @ConfigKey);

IF @OldConfigId IS NOT NULL
BEGIN
    PRINT N'Модель ' + @ConfigKey + N' вже існує - видаляю її разом із записами...';
    EXEC [dbo].[sp_EntityConfigs_Delete] @Id = @OldConfigId;
END

-------------------------------------------------------------------------------
-- 2. Модель і 6 полів (через ту саму процедуру, що використовує API)
-------------------------------------------------------------------------------
DECLARE @ConfigId UNIQUEIDENTIFIER = NEWID();

DECLARE @PropNumber   UNIQUEIDENTIFIER = NEWID();
DECLARE @PropCustomer UNIQUEIDENTIFIER = NEWID();
DECLARE @PropQuantity UNIQUEIDENTIFIER = NEWID();
DECLARE @PropPrice    UNIQUEIDENTIFIER = NEWID();
DECLARE @PropDiscount UNIQUEIDENTIFIER = NEWID();
DECLARE @PropPaid     UNIQUEIDENTIFIER = NEWID();

DECLARE @Properties [dbo].[EntityPropertyList];

-- DataType: 0 = Text, 1 = Number, 2 = Boolean
-- CreatedAt зсунуто на мілісекунди, щоб поля показувались у цьому порядку.
INSERT INTO @Properties ([Id], [EntityConfigId], [Key], [Name], [DataType], [CreatedAt])
VALUES
    (@PropNumber,   @ConfigId, N'number',   N'Номер',        0, DATEADD(MILLISECOND, 1, @Now)),
    (@PropCustomer, @ConfigId, N'customer', N'Клієнт',       0, DATEADD(MILLISECOND, 2, @Now)),
    (@PropQuantity, @ConfigId, N'quantity', N'Кількість',    1, DATEADD(MILLISECOND, 3, @Now)),
    (@PropPrice,    @ConfigId, N'price',    N'Ціна, грн',    1, DATEADD(MILLISECOND, 4, @Now)),
    (@PropDiscount, @ConfigId, N'discount', N'Знижка, %',    1, DATEADD(MILLISECOND, 5, @Now)),
    (@PropPaid,     @ConfigId, N'paid',     N'Оплачено',     2, DATEADD(MILLISECOND, 6, @Now));

EXEC [dbo].[sp_EntityConfigs_Create]
    @Id = @ConfigId,
    @Key = @ConfigKey,
    @Name = @ConfigName,
    @CreatedAt = @Now,
    @Properties = @Properties;

PRINT N'Створено модель ' + @ConfigKey + N' (' + CAST(@ConfigId AS NVARCHAR(36)) + N')';

-------------------------------------------------------------------------------
-- 3. Генеруємо дані записів у тимчасову таблицю
-------------------------------------------------------------------------------
DROP TABLE IF EXISTS #Rows;

WITH Numbers AS (
    SELECT TOP (@RecordCount)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N]
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
    CROSS JOIN sys.all_objects c
)
SELECT
    [N],
    NEWID() AS [Id],
    -- рівномірно за останні 180 днів (& 2147483647 - невід'ємне число без ризику переповнення ABS)
    DATEADD(SECOND, -((CHECKSUM(NEWID()) & 2147483647) % (180 * 86400)), @Now) AS [CreatedAt],
    (CHECKSUM(NEWID()) & 2147483647) % 20 + 1 AS [Quantity],
    CAST(((CHECKSUM(NEWID()) & 2147483647) % 2000000 + 1000) / 100.0 AS DECIMAL(12, 2)) AS [Price],
    (CHECKSUM(NEWID()) & 2147483647) % 100 AS [DiscountRoll],
    (CHECKSUM(NEWID()) & 2147483647) % 100 AS [PaidRoll],
    (CHECKSUM(NEWID()) & 2147483647) % 12 + 1 AS [CustomerIndex]
INTO #Rows
FROM Numbers;

-------------------------------------------------------------------------------
-- 4. Записуємо записи та значення їхніх полів
-------------------------------------------------------------------------------
BEGIN TRANSACTION;

INSERT INTO [dbo].[EntityInstances] ([Id], [EntityConfigId], [CreatedAt])
SELECT [Id], @ConfigId, [CreatedAt]
FROM #Rows;

INSERT INTO [dbo].[EntityPropertyInstances] ([Id], [EntityInstanceId], [EntityPropertyConfigId], [Value], [CreatedAt])
SELECT NEWID(), r.[Id], v.[PropertyId], v.[Value], r.[CreatedAt]
FROM #Rows r
CROSS APPLY (VALUES
    (@PropNumber,   N'ORD-' + RIGHT(N'000000' + CAST(r.[N] AS NVARCHAR(10)), 6)),
    (@PropCustomer, CHOOSE(r.[CustomerIndex],
                        N'ТОВ "Альфа Трейд"', N'ФОП Коваленко І.П.', N'ТОВ "Буковина Агро"',
                        N'ПП "Волинь Сервіс"', N'ФОП Шевчук О.М.', N'ТОВ "Дніпро Логістик"',
                        N'ТОВ "Карпати Груп"', N'ФОП Мельник А.В.', N'ТОВ "Поділля Маркет"',
                        N'ПП "Луцьк Техно"', N'ТОВ "Одеса Фреш"', N'ФОП Бондар Н.С.')),
    (@PropQuantity, CAST(r.[Quantity] AS NVARCHAR(10))),
    (@PropPrice,    CONVERT(NVARCHAR(20), r.[Price])),
    -- ~20% записів без знижки, щоб у таблиці були порожні значення
    (@PropDiscount, CASE WHEN r.[DiscountRoll] < 20 THEN NULL
                         ELSE CAST(r.[DiscountRoll] % 31 AS NVARCHAR(10)) END),
    -- ~70% оплачених
    (@PropPaid,     CASE WHEN r.[PaidRoll] < 70 THEN N'true' ELSE N'false' END)
) v([PropertyId], [Value]);

COMMIT TRANSACTION;

DROP TABLE #Rows;

-------------------------------------------------------------------------------
-- 5. Перевірка
-------------------------------------------------------------------------------
SELECT
    c.[Key],
    c.[Name],
    (SELECT COUNT(*) FROM [dbo].[EntityPropertyConfigs] p WHERE p.[EntityConfigId] = c.[Id]) AS [Properties],
    (SELECT COUNT(*) FROM [dbo].[EntityInstances] i WHERE i.[EntityConfigId] = c.[Id]) AS [Records],
    (SELECT COUNT(*)
     FROM [dbo].[EntityPropertyInstances] v
     INNER JOIN [dbo].[EntityInstances] i ON i.[Id] = v.[EntityInstanceId]
     WHERE i.[EntityConfigId] = c.[Id]) AS [Values]
FROM [dbo].[EntityConfigs] c
WHERE c.[Id] = @ConfigId;
