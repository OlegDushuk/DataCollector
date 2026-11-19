/*
================================================================================
  Експериментальне дослідження моделі зберігання даних DataCollector (розділ 3)
================================================================================

  Що робить скрипт
  ----------------
  1. Створює ОКРЕМУ базу [DataCollector_Experiment]. Робочу базу системи не змінює.
     Якщо тестова база вже є (повторний запуск) - видаляє і створює її заново.
  2. Створює в ній таблиці записів системи з тими самими колонками, обмеженнями
     й індексами, що в DataCollector.Server.DataBase, і ще дві таблиці для порівняння:
       - typed  - модель системи: EAV з типізованими колонками і фільтрованими індексами;
       - string - класичний EAV: усі значення одним рядком NVARCHAR;
       - json   - запис цілком у JSON-документі (NVARCHAR(MAX) + JSON_VALUE).
     Четвертий варіант - typed з вимкненими фільтрованими індексами - вимірюється
     на тих самих таблицях після ALTER INDEX ... DISABLE.
  3. Генерує модель «Замовлення» (8 полів усіх типів) на 10 000, 100 000 і 1 000 000
     записів. Значення детерміновані (залежать лише від номера запису), тож повторний
     запуск дає ті самі дані. Час запису кожного варіанта теж вимірюється.
  4. Виконує 5 типових запитів системи (та сама будова SQL, що генерує DataAccess:
     EXISTS по значеннях, сторінка OFFSET/FETCH, LEFT JOIN для агрегації, OPTION (RECOMPILE))
     для кожного обсягу і варіанта: 1 прогрівний + 10 вимірюваних запусків.
     Вимірюється час (мс), процесорний час (мс) і логічні читання сторінок.
     Результати запитів не передаються в SSMS (пишуться в табличні змінні) - міряється робота СУБД.
  5. Перевіряє, що всі варіанти повертають однаковий результат (стовпець «Збіг»).
  6. В кінці виводить ОДНУ таблицю з підсумками.

  Як запустити
  ------------
  - SSMS: File > Open > цей файл, підключення до вашого SQL Server (той самий, що для DataCollector),
    кнопка Execute (F5). Потрібні права на створення бази даних (локальний адміністратор SQL Server).
  - Тривалість: орієнтовно 20-60 хвилин. Прогрес видно на вкладці Messages.
  - Місце на диску: орієнтовно 3-4 ГБ (дані + журнал транзакцій).
  - Поки скрипт працює, бажано не навантажувати компʼютер іншими задачами.

  Що надіслати
  ------------
  - Таблицю результатів (остання сітка на вкладці Results): правою кнопкою по сітці >
    «Save Results As...» > тип CSV > збережіть у папку Desktop\Claude як experiment-results.csv.
  - Окремо напишіть: модель процесора, обсяг оперативної памʼяті, тип диска (SSD / HDD).

  Після дослідження тестову базу можна видалити - команда в самому кінці файлу (закоментована).
*/

    SET NOCOUNT ON;
    USE [master];
GO

IF DB_ID(N'DataCollector_Experiment') IS NOT NULL
    BEGIN
        ALTER DATABASE [DataCollector_Experiment] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        DROP DATABASE [DataCollector_Experiment];
    END
GO

CREATE DATABASE [DataCollector_Experiment];
GO

ALTER DATABASE [DataCollector_Experiment] SET RECOVERY SIMPLE;
GO

USE [DataCollector_Experiment];
GO

CREATE SCHEMA [exp];
GO

/* ---------------------------------------------------------------------------
   Таблиці системи (як у DataCollector.Server.DataBase/Tables)
   Відмінності, що не впливають на запити: у моделей і полів лише потрібні колонки,
   ключ запису задається явно (без IDENTITY), щоб у JSON-варіанті був той самий ключ.
--------------------------------------------------------------------------- */

CREATE TABLE [dbo].[EntityConfigs]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_EntityConfigs_Id DEFAULT (NEWID()),
    [Key] NVARCHAR(64) NOT NULL,
    [Name] NVARCHAR(128) NOT NULL,
    [Records] INT NOT NULL,

    CONSTRAINT PK_EntityConfigs PRIMARY KEY CLUSTERED ([Pkey])
);
GO

CREATE TABLE [dbo].[EntityPropertyConfigs]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_EntityPropertyConfigs_Id DEFAULT (NEWID()),
    [ConfigPkey] INT NOT NULL,
    [Key] NVARCHAR(32) NOT NULL,
    [DataType] INT NOT NULL,    -- 0 Text, 1 Number, 2 Boolean, 3 Date, 4 DateTime, 5 Enum

    CONSTRAINT PK_EntityPropertyConfigs PRIMARY KEY CLUSTERED ([Pkey]),
    CONSTRAINT UQ_EntityPropertyConfigs_ConfigPkey_Key UNIQUE NONCLUSTERED ([ConfigPkey], [Key]),
    CONSTRAINT FK_EntityPropertyConfigs_EntityConfigs FOREIGN KEY ([ConfigPkey]) REFERENCES [dbo].[EntityConfigs] ([Pkey])
);
GO

CREATE TABLE [dbo].[EntityInstances]
(
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ConfigPkey] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [RowVersion] ROWVERSION NOT NULL,

    CONSTRAINT PK_EntityInstances PRIMARY KEY CLUSTERED ([Pkey]),
    CONSTRAINT UQ_EntityInstances_Id UNIQUE NONCLUSTERED ([Id]),
    CONSTRAINT FK_EntityInstances_EntityConfigs FOREIGN KEY ([ConfigPkey]) REFERENCES [dbo].[EntityConfigs] ([Pkey])
);
GO

CREATE INDEX IX_EntityInstances_ConfigPkey_CreatedAt
    ON [dbo].[EntityInstances] ([ConfigPkey], [CreatedAt]);
GO

-- Варіант typed: значення в колонці свого типу (копія таблиці системи з усіма індексами).
CREATE TABLE [dbo].[EntityPropertyInstances]
(
    [InstancePkey] BIGINT NOT NULL,
    [PropertyPkey] INT NOT NULL,
    [ValueText] NVARCHAR(4000) NULL,
    [ValueNumber] DECIMAL(38, 10) SPARSE NULL,
    [ValueBool] BIT NULL,
    [ValueDate] DATETIME2 SPARSE NULL,

    CONSTRAINT PK_EntityPropertyInstances PRIMARY KEY CLUSTERED ([InstancePkey], [PropertyPkey]),
    CONSTRAINT FK_EntityPropertyInstances_EntityInstances FOREIGN KEY ([InstancePkey]) REFERENCES [dbo].[EntityInstances] ([Pkey]),
    CONSTRAINT FK_EntityPropertyInstances_EntityPropertyConfigs FOREIGN KEY ([PropertyPkey]) REFERENCES [dbo].[EntityPropertyConfigs] ([Pkey]),
    CONSTRAINT CK_EntityPropertyInstances_SingleValue CHECK (
        CASE WHEN [ValueText]   IS NULL THEN 0 ELSE 1 END
            + CASE WHEN [ValueNumber] IS NULL THEN 0 ELSE 1 END
            + CASE WHEN [ValueBool]   IS NULL THEN 0 ELSE 1 END
            + CASE WHEN [ValueDate]   IS NULL THEN 0 ELSE 1 END = 1)
);
GO

CREATE INDEX IX_EntityPropertyInstances_PropertyPkey ON [dbo].[EntityPropertyInstances] ([PropertyPkey]);
GO
CREATE INDEX IX_EntityPropertyInstances_Number ON [dbo].[EntityPropertyInstances] ([PropertyPkey], [ValueNumber]) WHERE [ValueNumber] IS NOT NULL;
GO
CREATE INDEX IX_EntityPropertyInstances_Bool ON [dbo].[EntityPropertyInstances] ([PropertyPkey], [ValueBool]) WHERE [ValueBool] IS NOT NULL;
GO
CREATE INDEX IX_EntityPropertyInstances_Date ON [dbo].[EntityPropertyInstances] ([PropertyPkey], [ValueDate]) WHERE [ValueDate] IS NOT NULL;
GO

-- Варіант string: класичний EAV - значення будь-якого типу зберігається текстом.
-- Індекс за значенням неможливий без обрізання (ключ індексу до 1700 байт), тож лише за полем - як у системі.
CREATE TABLE [dbo].[EavStringValues]
(
    [InstancePkey] BIGINT NOT NULL,
    [PropertyPkey] INT NOT NULL,
    [Value] NVARCHAR(4000) NOT NULL,

    CONSTRAINT PK_EavStringValues PRIMARY KEY CLUSTERED ([InstancePkey], [PropertyPkey]),
    CONSTRAINT FK_EavStringValues_EntityInstances FOREIGN KEY ([InstancePkey]) REFERENCES [dbo].[EntityInstances] ([Pkey]),
    CONSTRAINT FK_EavStringValues_EntityPropertyConfigs FOREIGN KEY ([PropertyPkey]) REFERENCES [dbo].[EntityPropertyConfigs] ([Pkey])
);
GO

CREATE INDEX IX_EavStringValues_PropertyPkey ON [dbo].[EavStringValues] ([PropertyPkey]);
GO

-- Варіант json: запис з усіма значеннями в одному JSON-документі.
-- Індекс за полем JSON у SQL Server потребує обчислюваної колонки, тобто зміни схеми (DDL) для кожного поля,
-- що суперечить моделі зі змінною схемою, тож індекси лише за моделлю і датою створення - як у записів системи.
CREATE TABLE [dbo].[JsonInstances]
(
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ConfigPkey] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [RowVersion] ROWVERSION NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL,

    CONSTRAINT PK_JsonInstances PRIMARY KEY CLUSTERED ([Pkey]),
    CONSTRAINT UQ_JsonInstances_Id UNIQUE NONCLUSTERED ([Id]),
    CONSTRAINT FK_JsonInstances_EntityConfigs FOREIGN KEY ([ConfigPkey]) REFERENCES [dbo].[EntityConfigs] ([Pkey]),
    CONSTRAINT CK_JsonInstances_Data CHECK (ISJSON([Data]) = 1)
);
GO

CREATE INDEX IX_JsonInstances_ConfigPkey_CreatedAt ON [dbo].[JsonInstances] ([ConfigPkey], [CreatedAt]);
GO

/* ---------------------------------------------------------------------------
   Службові таблиці експерименту
--------------------------------------------------------------------------- */

CREATE TABLE [exp].[Queries]
(
    [Variant] NVARCHAR(10) NOT NULL,
    [Code] NVARCHAR(4) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Sql] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT PK_Queries PRIMARY KEY ([Variant], [Code])
);
GO

CREATE TABLE [exp].[Results]
(
    [Id] INT IDENTITY(1, 1) PRIMARY KEY,
    [Size] INT NOT NULL,
    [Variant] NVARCHAR(20) NOT NULL,
    [Code] NVARCHAR(4) NOT NULL,
    [Iteration] INT NOT NULL,
    [ElapsedMs] DECIMAL(18, 3) NOT NULL,
    [CpuMs] INT NOT NULL,
    [LogicalReads] BIGINT NOT NULL,
    [Check1] DECIMAL(38, 10) NULL,
    [Check2] DECIMAL(38, 10) NULL
);
GO

CREATE TABLE [exp].[Loads]
(
    [Size] INT NOT NULL,
    [Variant] NVARCHAR(20) NOT NULL,
    [ElapsedMs] DECIMAL(18, 3) NOT NULL,
    CONSTRAINT PK_Loads PRIMARY KEY ([Size], [Variant])
);
GO

CREATE TABLE [exp].[Info]
(
    [Name] NVARCHAR(100) NOT NULL PRIMARY KEY,
    [Value] NVARCHAR(4000) NULL
);
GO

/* ---------------------------------------------------------------------------
   Генерація моделі «Замовлення» на @Size записів
   Псевдовипадкові значення: h(k) = (N * A_k + B_k) mod 4294967291 - залежать лише від номера запису N.
--------------------------------------------------------------------------- */

CREATE PROCEDURE [exp].[Generate] @Size INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @C INT, @Base BIGINT, @t0 DATETIME2(7), @t1 DATETIME2(7), @InstancesMs DECIMAL(18, 3), @Msg NVARCHAR(200);

    INSERT INTO [dbo].[EntityConfigs] ([Key], [Name], [Records]) VALUES (CONCAT(N'orders_', @Size), N'Замовлення', @Size);
    SET @C = SCOPE_IDENTITY();

    INSERT INTO [dbo].[EntityPropertyConfigs] ([ConfigPkey], [Key], [DataType])
    VALUES (@C, N'customer', 0), (@C, N'price', 1), (@C, N'paid', 2), (@C, N'status', 5),
           (@C, N'deliveryDate', 3), (@C, N'orderedAt', 4), (@C, N'quantity', 1), (@C, N'comment', 0);

    DECLARE @Customer INT, @Price INT, @Paid INT, @Status INT, @Delivery INT, @Ordered INT, @Qty INT, @Comment INT;
    SELECT @Customer = MAX(CASE WHEN [Key] = N'customer' THEN [Pkey] END),
           @Price    = MAX(CASE WHEN [Key] = N'price' THEN [Pkey] END),
           @Paid     = MAX(CASE WHEN [Key] = N'paid' THEN [Pkey] END),
           @Status   = MAX(CASE WHEN [Key] = N'status' THEN [Pkey] END),
           @Delivery = MAX(CASE WHEN [Key] = N'deliveryDate' THEN [Pkey] END),
           @Ordered  = MAX(CASE WHEN [Key] = N'orderedAt' THEN [Pkey] END),
           @Qty      = MAX(CASE WHEN [Key] = N'quantity' THEN [Pkey] END),
           @Comment  = MAX(CASE WHEN [Key] = N'comment' THEN [Pkey] END)
    FROM [dbo].[EntityPropertyConfigs] WHERE [ConfigPkey] = @C;

    SET @Base = ISNULL((SELECT MAX([Pkey]) FROM [dbo].[EntityInstances]), 0);

    SET @Msg = CONCAT(N'Генерація ', @Size, N' записів...');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    CREATE TABLE #R
    (
        [N] BIGINT NOT NULL PRIMARY KEY,
        [CreatedAt] DATETIME2 NOT NULL,
        [Customer] NVARCHAR(100) NOT NULL,
        [Price] DECIMAL(38, 10) NOT NULL,
        [Paid] BIT NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [Delivery] DATETIME2 NOT NULL,
        [Ordered] DATETIME2 NOT NULL,
        [Qty] DECIMAL(38, 10) NOT NULL,
        [Comment] NVARCHAR(200) NULL
    );

    WITH [n] AS (
        SELECT TOP (@Size) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS BIGINT) AS [N]
        FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c
    ),
         [h] AS (
             SELECT [N],
                    CAST((N * 2654435761 + 11) % 4294967291 AS BIGINT) AS [H1],
                    CAST((N * 2246822519 + 23) % 4294967291 AS BIGINT) AS [H2],
                    CAST((N * 3266489917 + 37) % 4294967291 AS BIGINT) AS [H3],
                    CAST((N * 668265263 + 41) % 4294967291 AS BIGINT) AS [H4],
                    CAST((N * 374761393 + 53) % 4294967291 AS BIGINT) AS [H5],
                    CAST((N * 1103515245 + 67) % 4294967291 AS BIGINT) AS [H6],
                    CAST((N * 1664525 + 71) % 4294967291 AS BIGINT) AS [H7],
                    CAST((N * 22695477 + 83) % 4294967291 AS BIGINT) AS [H8],
                    -- рівномірно за рік до «зараз» (2026-11-15 12:00 UTC); записи додаються в хронологічному порядку
                    DATEADD(SECOND, CAST(N * 31536000.0 / @Size AS INT), CAST('2025-11-15T12:00:00' AS DATETIME2)) AS [CreatedAt]
             FROM [n]
         )
    INSERT INTO #R
    SELECT
        [N],
        [CreatedAt],
        CONCAT(N'Клієнт ', [H1] % 5000 + 1),
        CAST(([H2] % 9999000 + 1000) / 100.0 AS DECIMAL(38, 10)),                       -- 10,00 ... 99 999,99
        CASE WHEN [H3] % 100 < 80 THEN 1 ELSE 0 END,                                     -- 80 % оплачені
        CASE WHEN [H4] % 100 < 10 THEN N'new' WHEN [H4] % 100 < 25 THEN N'processing'
             WHEN [H4] % 100 < 45 THEN N'shipped' WHEN [H4] % 100 < 95 THEN N'done' ELSE N'cancelled' END,
        CAST(DATEADD(DAY, 1 + [H5] % 14, CAST(DATEADD(MINUTE, -([H6] % 4320), [CreatedAt]) AS DATE)) AS DATETIME2),
        DATEADD(MINUTE, -([H6] % 4320), [CreatedAt]),                                    -- оформлено до 3 діб раніше
        CAST(1 + [H7] % 50 AS DECIMAL(38, 10)),
        CASE WHEN [H8] % 100 < 30 THEN CONCAT(N'Коментар до замовлення № ', [N]) END    -- 30 % заповнені
    FROM [h];

    -- Записи (спільні для typed і string)
    SET @t0 = SYSDATETIME();
    INSERT INTO [dbo].[EntityInstances] ([Pkey], [Id], [ConfigPkey], [CreatedAt])
    SELECT @Base + [N], NEWID(), @C, [CreatedAt] FROM #R ORDER BY [N];
    SET @t1 = SYSDATETIME();
    SET @InstancesMs = DATEDIFF_BIG(MICROSECOND, @t0, @t1) / 1000.0;

    -- typed
    SET @t0 = SYSDATETIME();
    INSERT INTO [dbo].[EntityPropertyInstances] ([InstancePkey], [PropertyPkey], [ValueText], [ValueNumber], [ValueBool], [ValueDate])
    SELECT @Base + r.[N], v.[P], v.[T], v.[Num], v.[B], v.[D]
    FROM #R r
             CROSS APPLY (VALUES
                              (@Customer, r.[Customer], NULL, NULL, NULL),
                              (@Price, NULL, r.[Price], NULL, NULL),
                              (@Paid, NULL, NULL, r.[Paid], NULL),
                              (@Status, r.[Status], NULL, NULL, NULL),
                              (@Delivery, NULL, NULL, NULL, r.[Delivery]),
                              (@Ordered, NULL, NULL, NULL, r.[Ordered]),
                              (@Qty, NULL, r.[Qty], NULL, NULL),
                              (@Comment, r.[Comment], NULL, NULL, NULL)
    ) v ([P], [T], [Num], [B], [D])
    WHERE v.[T] IS NOT NULL OR v.[Num] IS NOT NULL OR v.[B] IS NOT NULL OR v.[D] IS NOT NULL
    ORDER BY r.[N], v.[P];
    SET @t1 = SYSDATETIME();
    INSERT INTO [exp].[Loads] VALUES (@Size, N'typed', @InstancesMs + DATEDIFF_BIG(MICROSECOND, @t0, @t1) / 1000.0);

    -- string
    SET @t0 = SYSDATETIME();
    INSERT INTO [dbo].[EavStringValues] ([InstancePkey], [PropertyPkey], [Value])
    SELECT @Base + r.[N], v.[P], v.[V]
    FROM #R r
             CROSS APPLY (VALUES
                              (@Customer, r.[Customer]),
                              (@Price, CONVERT(NVARCHAR(50), r.[Price])),
                              (@Paid, CASE r.[Paid] WHEN 1 THEN N'true' ELSE N'false' END),
                              (@Status, r.[Status]),
                              (@Delivery, CONVERT(NVARCHAR(30), r.[Delivery], 126)),
                              (@Ordered, CONVERT(NVARCHAR(30), r.[Ordered], 126)),
                              (@Qty, CONVERT(NVARCHAR(50), r.[Qty])),
                              (@Comment, r.[Comment])
    ) v ([P], [V])
    WHERE v.[V] IS NOT NULL
    ORDER BY r.[N], v.[P];
    SET @t1 = SYSDATETIME();
    INSERT INTO [exp].[Loads] VALUES (@Size, N'string', @InstancesMs + DATEDIFF_BIG(MICROSECOND, @t0, @t1) / 1000.0);

    -- json (запис і значення - одним рядком)
    SET @t0 = SYSDATETIME();
    INSERT INTO [dbo].[JsonInstances] ([Pkey], [Id], [ConfigPkey], [CreatedAt], [Data])
    SELECT @Base + r.[N], NEWID(), @C, r.[CreatedAt],
           CONCAT(N'{"customer":"', STRING_ESCAPE(r.[Customer], 'json'),
                  N'","price":', CONVERT(NVARCHAR(50), r.[Price]),
                  N',"paid":', CASE r.[Paid] WHEN 1 THEN N'true' ELSE N'false' END,
                  N',"status":"', r.[Status],
                  N'","deliveryDate":"', CONVERT(NVARCHAR(30), r.[Delivery], 126),
                  N'","orderedAt":"', CONVERT(NVARCHAR(30), r.[Ordered], 126),
                  N'","quantity":', CONVERT(NVARCHAR(50), r.[Qty]),
                  CASE WHEN r.[Comment] IS NOT NULL THEN CONCAT(N',"comment":"', STRING_ESCAPE(r.[Comment], 'json'), N'"') END,
                  N'}')
    FROM #R r
    ORDER BY r.[N];
    SET @t1 = SYSDATETIME();
    INSERT INTO [exp].[Loads] VALUES (@Size, N'json', DATEDIFF_BIG(MICROSECOND, @t0, @t1) / 1000.0);

    DROP TABLE #R;
    CHECKPOINT;
END
GO

/* ---------------------------------------------------------------------------
   Вимірювання: усі запити варіанта @Sql на моделі з @Size записів
   @Variant - під яким імʼям записати результати (typed_noidx - ті самі запити typed без фільтрованих індексів)
--------------------------------------------------------------------------- */

CREATE PROCEDURE [exp].[Measure] @Size INT, @Sql NVARCHAR(10), @Variant NVARCHAR(20), @Iterations INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @C INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Records] = @Size);
    DECLARE @Customer INT, @Price INT, @Paid INT, @Status INT, @Delivery INT, @Ordered INT, @Qty INT, @Comment INT;
    SELECT @Customer = MAX(CASE WHEN [Key] = N'customer' THEN [Pkey] END),
           @Price    = MAX(CASE WHEN [Key] = N'price' THEN [Pkey] END),
           @Paid     = MAX(CASE WHEN [Key] = N'paid' THEN [Pkey] END),
           @Status   = MAX(CASE WHEN [Key] = N'status' THEN [Pkey] END),
           @Delivery = MAX(CASE WHEN [Key] = N'deliveryDate' THEN [Pkey] END),
           @Ordered  = MAX(CASE WHEN [Key] = N'orderedAt' THEN [Pkey] END),
           @Qty      = MAX(CASE WHEN [Key] = N'quantity' THEN [Pkey] END),
           @Comment  = MAX(CASE WHEN [Key] = N'comment' THEN [Pkey] END)
    FROM [dbo].[EntityPropertyConfigs] WHERE [ConfigPkey] = @C;

    DECLARE @Params NVARCHAR(500) = N'@C INT, @Customer INT, @Price INT, @Paid INT, @Status INT, @Delivery INT, @Ordered INT, @Qty INT, @Comment INT, @Check1 DECIMAL(38, 10) OUTPUT, @Check2 DECIMAL(38, 10) OUTPUT';
    DECLARE @Code NVARCHAR(4), @Text NVARCHAR(MAX), @i INT, @Check1 DECIMAL(38, 10), @Check2 DECIMAL(38, 10);
    DECLARE @t0 DATETIME2(7), @t1 DATETIME2(7), @r0 BIGINT, @r1 BIGINT, @c0 INT, @c1 INT, @Msg NVARCHAR(200);

    DECLARE [q] CURSOR LOCAL FAST_FORWARD FOR
        SELECT [Code], [Sql] FROM [exp].[Queries] WHERE [Variant] = @Sql ORDER BY [Code];
    OPEN [q];
    FETCH NEXT FROM [q] INTO @Code, @Text;

    WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @Msg = CONCAT(N'  ', @Variant, N' / ', @Size, N' записів / ', @Code);
            RAISERROR(@Msg, 0, 1) WITH NOWAIT;

            SET @i = 0;   -- 0 - прогрівний запуск, не записується
            WHILE @i <= @Iterations
                BEGIN
                    SELECT @r0 = [logical_reads], @c0 = [cpu_time] FROM sys.dm_exec_requests WHERE [session_id] = @@SPID;
                    SET @t0 = SYSDATETIME();

                    EXEC sp_executesql @Text, @Params,
                         @C = @C, @Customer = @Customer, @Price = @Price, @Paid = @Paid, @Status = @Status,
                         @Delivery = @Delivery, @Ordered = @Ordered, @Qty = @Qty, @Comment = @Comment,
                         @Check1 = @Check1 OUTPUT, @Check2 = @Check2 OUTPUT;

                    SET @t1 = SYSDATETIME();
                    SELECT @r1 = [logical_reads], @c1 = [cpu_time] FROM sys.dm_exec_requests WHERE [session_id] = @@SPID;

                    IF @i > 0
                        INSERT INTO [exp].[Results] ([Size], [Variant], [Code], [Iteration], [ElapsedMs], [CpuMs], [LogicalReads], [Check1], [Check2])
                        VALUES (@Size, @Variant, @Code, @i, DATEDIFF_BIG(MICROSECOND, @t0, @t1) / 1000.0, @c1 - @c0, @r1 - @r0, @Check1, @Check2);

                    SET @i += 1;
                END

            FETCH NEXT FROM [q] INTO @Code, @Text;
        END

    CLOSE [q];
    DEALLOCATE [q];
END
GO

/* ---------------------------------------------------------------------------
   Тексти запитів (згенеровані за будовою RecordQuerySql і RecordAggregateSql)
--------------------------------------------------------------------------- */

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'typed', N'Q1', N'Фільтр за числовим полем (діапазон ціни) + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[EntityInstances] i
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Price AND v.[ValueNumber] IS NOT NULL AND v.[ValueNumber] >= 1000 AND v.[ValueNumber] <= 1500)
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT)
FROM [dbo].[EntityInstances] i

WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Price AND v.[ValueNumber] IS NOT NULL AND v.[ValueNumber] >= 1000 AND v.[ValueNumber] <= 1500)
ORDER BY i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

DECLARE @Values TABLE ([InstanceId] UNIQUEIDENTIFIER, [PropertyId] UNIQUEIDENTIFIER, [ValueText] NVARCHAR(4000),
    [ValueNumber] DECIMAL(38, 10), [ValueBool] BIT, [ValueDate] DATETIME2);
INSERT INTO @Values
SELECT pg.[Id], p.[Id], v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
FROM @Page pg
INNER JOIN [dbo].[EntityPropertyInstances] v ON v.[InstancePkey] = pg.[Pkey]
INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey];

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'typed', N'Q2', N'Фільтр за переліком і логічним полем + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[EntityInstances] i
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Status AND v.[ValueText] IS NOT NULL AND v.[ValueText] = N''cancelled'')
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[ValueBool] IS NOT NULL AND v.[ValueBool] = 1)
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT)
FROM [dbo].[EntityInstances] i

WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Status AND v.[ValueText] IS NOT NULL AND v.[ValueText] = N''cancelled'')
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[ValueBool] IS NOT NULL AND v.[ValueBool] = 1)
ORDER BY i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

DECLARE @Values TABLE ([InstanceId] UNIQUEIDENTIFIER, [PropertyId] UNIQUEIDENTIFIER, [ValueText] NVARCHAR(4000),
    [ValueNumber] DECIMAL(38, 10), [ValueBool] BIT, [ValueDate] DATETIME2);
INSERT INTO @Values
SELECT pg.[Id], p.[Id], v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
FROM @Page pg
INNER JOIN [dbo].[EntityPropertyInstances] v ON v.[InstancePkey] = pg.[Pkey]
INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey];

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'typed', N'Q3', N'Діапазон дат + сортування за числом + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[EntityInstances] i
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Ordered AND v.[ValueDate] IS NOT NULL AND v.[ValueDate] >= ''2026-06-01T00:00:00'' AND v.[ValueDate] <= ''2026-06-07T23:59:59'')
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT)
FROM [dbo].[EntityInstances] i
LEFT JOIN [dbo].[EntityPropertyInstances] s ON s.[InstancePkey] = i.[Pkey] AND s.[PropertyPkey] = @Price
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Ordered AND v.[ValueDate] IS NOT NULL AND v.[ValueDate] >= ''2026-06-01T00:00:00'' AND v.[ValueDate] <= ''2026-06-07T23:59:59'')
ORDER BY s.[ValueNumber] DESC, i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

DECLARE @Values TABLE ([InstanceId] UNIQUEIDENTIFIER, [PropertyId] UNIQUEIDENTIFIER, [ValueText] NVARCHAR(4000),
    [ValueNumber] DECIMAL(38, 10), [ValueBool] BIT, [ValueDate] DATETIME2);
INSERT INTO @Values
SELECT pg.[Id], p.[Id], v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
FROM @Page pg
INNER JOIN [dbo].[EntityPropertyInstances] v ON v.[InstancePkey] = pg.[Pkey]
INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey];

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'typed', N'Q4', N'Сума за місяцями (дані для діаграми)', N'DECLARE @Agg TABLE ([K0] DATETIME2 NULL, [M0] DECIMAL(38, 10) NULL);

INSERT INTO @Agg ([K0], [M0])
SELECT TOP (1001)
    CAST(DATEFROMPARTS(YEAR(CAST((j0.[ValueDate] AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((j0.[ValueDate] AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2) AS [K0],
    SUM(j1.[ValueNumber]) AS [M0]
FROM [dbo].[EntityInstances] i
LEFT JOIN [dbo].[EntityPropertyInstances] j0 ON j0.[InstancePkey] = i.[Pkey] AND j0.[PropertyPkey] = @Ordered
LEFT JOIN [dbo].[EntityPropertyInstances] j1 ON j1.[InstancePkey] = i.[Pkey] AND j1.[PropertyPkey] = @Price
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[ValueBool] IS NOT NULL AND v.[ValueBool] = 1)
GROUP BY CAST(DATEFROMPARTS(YEAR(CAST((j0.[ValueDate] AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((j0.[ValueDate] AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2)
ORDER BY CAST(DATEFROMPARTS(YEAR(CAST((j0.[ValueDate] AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((j0.[ValueDate] AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2)
OPTION (RECOMPILE);

SET @Check1 = (SELECT ISNULL(SUM([M0]), 0) FROM @Agg);
SET @Check2 = (SELECT COUNT(*) FROM @Agg);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'typed', N'Q5', N'Показник правила моніторингу і показник порівняння', N'-- Поточний місяць (Київ) і попередній місяць до того ж моменту; зараз = 2026-11-15 12:00 UTC.
SELECT @Check1 = SUM(j0.[ValueNumber])
FROM [dbo].[EntityInstances] i
LEFT JOIN [dbo].[EntityPropertyInstances] j0 ON j0.[InstancePkey] = i.[Pkey] AND j0.[PropertyPkey] = @Price
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[ValueBool] IS NOT NULL AND v.[ValueBool] = 1)
  AND i.[CreatedAt] >= ''2026-10-31T22:00:00''
  AND i.[CreatedAt] < ''2026-11-30T22:00:00''
OPTION (RECOMPILE);

SELECT @Check2 = SUM(j0.[ValueNumber])
FROM [dbo].[EntityInstances] i
LEFT JOIN [dbo].[EntityPropertyInstances] j0 ON j0.[InstancePkey] = i.[Pkey] AND j0.[PropertyPkey] = @Price
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EntityPropertyInstances] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[ValueBool] IS NOT NULL AND v.[ValueBool] = 1)
  AND i.[CreatedAt] >= ''2026-09-30T21:00:00''
  AND i.[CreatedAt] < ''2026-10-15T11:00:00''
OPTION (RECOMPILE);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'string', N'Q1', N'Фільтр за числовим полем (діапазон ціни) + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[EntityInstances] i
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Price AND v.[Value] IS NOT NULL AND TRY_CAST(v.[Value] AS DECIMAL(38, 10)) >= 1000 AND TRY_CAST(v.[Value] AS DECIMAL(38, 10)) <= 1500)
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT)
FROM [dbo].[EntityInstances] i

WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Price AND v.[Value] IS NOT NULL AND TRY_CAST(v.[Value] AS DECIMAL(38, 10)) >= 1000 AND TRY_CAST(v.[Value] AS DECIMAL(38, 10)) <= 1500)
ORDER BY i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

DECLARE @Values TABLE ([InstanceId] UNIQUEIDENTIFIER, [PropertyId] UNIQUEIDENTIFIER, [Value] NVARCHAR(4000));
INSERT INTO @Values
SELECT pg.[Id], p.[Id], v.[Value]
FROM @Page pg
INNER JOIN [dbo].[EavStringValues] v ON v.[InstancePkey] = pg.[Pkey]
INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey];

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'string', N'Q2', N'Фільтр за переліком і логічним полем + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[EntityInstances] i
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Status AND v.[Value] IS NOT NULL AND v.[Value] = N''cancelled'')
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[Value] IS NOT NULL AND v.[Value] = N''true'')
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT)
FROM [dbo].[EntityInstances] i

WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Status AND v.[Value] IS NOT NULL AND v.[Value] = N''cancelled'')
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[Value] IS NOT NULL AND v.[Value] = N''true'')
ORDER BY i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

DECLARE @Values TABLE ([InstanceId] UNIQUEIDENTIFIER, [PropertyId] UNIQUEIDENTIFIER, [Value] NVARCHAR(4000));
INSERT INTO @Values
SELECT pg.[Id], p.[Id], v.[Value]
FROM @Page pg
INNER JOIN [dbo].[EavStringValues] v ON v.[InstancePkey] = pg.[Pkey]
INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey];

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'string', N'Q3', N'Діапазон дат + сортування за числом + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[EntityInstances] i
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Ordered AND v.[Value] IS NOT NULL AND TRY_CONVERT(DATETIME2, v.[Value], 126) >= ''2026-06-01T00:00:00'' AND TRY_CONVERT(DATETIME2, v.[Value], 126) <= ''2026-06-07T23:59:59'')
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT)
FROM [dbo].[EntityInstances] i
LEFT JOIN [dbo].[EavStringValues] s ON s.[InstancePkey] = i.[Pkey] AND s.[PropertyPkey] = @Price
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Ordered AND v.[Value] IS NOT NULL AND TRY_CONVERT(DATETIME2, v.[Value], 126) >= ''2026-06-01T00:00:00'' AND TRY_CONVERT(DATETIME2, v.[Value], 126) <= ''2026-06-07T23:59:59'')
ORDER BY TRY_CAST(s.[Value] AS DECIMAL(38, 10)) DESC, i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

DECLARE @Values TABLE ([InstanceId] UNIQUEIDENTIFIER, [PropertyId] UNIQUEIDENTIFIER, [Value] NVARCHAR(4000));
INSERT INTO @Values
SELECT pg.[Id], p.[Id], v.[Value]
FROM @Page pg
INNER JOIN [dbo].[EavStringValues] v ON v.[InstancePkey] = pg.[Pkey]
INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey];

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'string', N'Q4', N'Сума за місяцями (дані для діаграми)', N'DECLARE @Agg TABLE ([K0] DATETIME2 NULL, [M0] DECIMAL(38, 10) NULL);

INSERT INTO @Agg ([K0], [M0])
SELECT TOP (1001)
    CAST(DATEFROMPARTS(YEAR(CAST((TRY_CONVERT(DATETIME2, j0.[Value], 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((TRY_CONVERT(DATETIME2, j0.[Value], 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2) AS [K0],
    SUM(TRY_CAST(j1.[Value] AS DECIMAL(38, 10))) AS [M0]
FROM [dbo].[EntityInstances] i
LEFT JOIN [dbo].[EavStringValues] j0 ON j0.[InstancePkey] = i.[Pkey] AND j0.[PropertyPkey] = @Ordered
LEFT JOIN [dbo].[EavStringValues] j1 ON j1.[InstancePkey] = i.[Pkey] AND j1.[PropertyPkey] = @Price
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[Value] IS NOT NULL AND v.[Value] = N''true'')
GROUP BY CAST(DATEFROMPARTS(YEAR(CAST((TRY_CONVERT(DATETIME2, j0.[Value], 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((TRY_CONVERT(DATETIME2, j0.[Value], 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2)
ORDER BY CAST(DATEFROMPARTS(YEAR(CAST((TRY_CONVERT(DATETIME2, j0.[Value], 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((TRY_CONVERT(DATETIME2, j0.[Value], 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2)
OPTION (RECOMPILE);

SET @Check1 = (SELECT ISNULL(SUM([M0]), 0) FROM @Agg);
SET @Check2 = (SELECT COUNT(*) FROM @Agg);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'string', N'Q5', N'Показник правила моніторингу і показник порівняння', N'-- Поточний місяць (Київ) і попередній місяць до того ж моменту; зараз = 2026-11-15 12:00 UTC.
SELECT @Check1 = SUM(TRY_CAST(j0.[Value] AS DECIMAL(38, 10)))
FROM [dbo].[EntityInstances] i
LEFT JOIN [dbo].[EavStringValues] j0 ON j0.[InstancePkey] = i.[Pkey] AND j0.[PropertyPkey] = @Price
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[Value] IS NOT NULL AND v.[Value] = N''true'')
  AND i.[CreatedAt] >= ''2026-10-31T22:00:00''
  AND i.[CreatedAt] < ''2026-11-30T22:00:00''
OPTION (RECOMPILE);

SELECT @Check2 = SUM(TRY_CAST(j0.[Value] AS DECIMAL(38, 10)))
FROM [dbo].[EntityInstances] i
LEFT JOIN [dbo].[EavStringValues] j0 ON j0.[InstancePkey] = i.[Pkey] AND j0.[PropertyPkey] = @Price
WHERE i.[ConfigPkey] = @C
  AND EXISTS (SELECT 1 FROM [dbo].[EavStringValues] v WHERE v.[InstancePkey] = i.[Pkey] AND v.[PropertyPkey] = @Paid AND v.[Value] IS NOT NULL AND v.[Value] = N''true'')
  AND i.[CreatedAt] >= ''2026-09-30T21:00:00''
  AND i.[CreatedAt] < ''2026-10-15T11:00:00''
OPTION (RECOMPILE);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'json', N'Q1', N'Фільтр за числовим полем (діапазон ціни) + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[JsonInstances] i
WHERE i.[ConfigPkey] = @C
  AND TRY_CAST(JSON_VALUE(i.[Data], N''$.price'') AS DECIMAL(38, 10)) >= 1000 AND TRY_CAST(JSON_VALUE(i.[Data], N''$.price'') AS DECIMAL(38, 10)) <= 1500
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version], [Data])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT), i.[Data]
FROM [dbo].[JsonInstances] i

WHERE i.[ConfigPkey] = @C
  AND TRY_CAST(JSON_VALUE(i.[Data], N''$.price'') AS DECIMAL(38, 10)) >= 1000 AND TRY_CAST(JSON_VALUE(i.[Data], N''$.price'') AS DECIMAL(38, 10)) <= 1500
ORDER BY i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'json', N'Q2', N'Фільтр за переліком і логічним полем + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[JsonInstances] i
WHERE i.[ConfigPkey] = @C
  AND JSON_VALUE(i.[Data], N''$.status'') = N''cancelled''
  AND JSON_VALUE(i.[Data], N''$.paid'') = N''true''
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version], [Data])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT), i.[Data]
FROM [dbo].[JsonInstances] i

WHERE i.[ConfigPkey] = @C
  AND JSON_VALUE(i.[Data], N''$.status'') = N''cancelled''
  AND JSON_VALUE(i.[Data], N''$.paid'') = N''true''
ORDER BY i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'json', N'Q3', N'Діапазон дат + сортування за числом + сторінка', N'DECLARE @Total INT;
DECLARE @Page TABLE (
    [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
    [Pkey] BIGINT NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [Version] BIGINT NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL);

SELECT @Total = COUNT(*)
FROM [dbo].[JsonInstances] i
WHERE i.[ConfigPkey] = @C
  AND TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) >= ''2026-06-01T00:00:00'' AND TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) <= ''2026-06-07T23:59:59''
OPTION (RECOMPILE);

INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt], [Version], [Data])
SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt], CAST(i.[RowVersion] AS BIGINT), i.[Data]
FROM [dbo].[JsonInstances] i

WHERE i.[ConfigPkey] = @C
  AND TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) >= ''2026-06-01T00:00:00'' AND TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) <= ''2026-06-07T23:59:59''
ORDER BY TRY_CAST(JSON_VALUE(i.[Data], N''$.price'') AS DECIMAL(38, 10)) DESC, i.[CreatedAt] DESC, i.[Pkey] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
OPTION (RECOMPILE);

SET @Check1 = @Total;
SET @Check2 = (SELECT ISNULL(SUM([Pkey]), 0) FROM @Page);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'json', N'Q4', N'Сума за місяцями (дані для діаграми)', N'DECLARE @Agg TABLE ([K0] DATETIME2 NULL, [M0] DECIMAL(38, 10) NULL);

INSERT INTO @Agg ([K0], [M0])
SELECT TOP (1001)
    CAST(DATEFROMPARTS(YEAR(CAST((TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2) AS [K0],
    SUM(TRY_CAST(JSON_VALUE(i.[Data], N''$.price'') AS DECIMAL(38, 10))) AS [M0]
FROM [dbo].[JsonInstances] i

WHERE i.[ConfigPkey] = @C
  AND JSON_VALUE(i.[Data], N''$.paid'') = N''true''
GROUP BY CAST(DATEFROMPARTS(YEAR(CAST((TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2)
ORDER BY CAST(DATEFROMPARTS(YEAR(CAST((TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), MONTH(CAST((TRY_CONVERT(DATETIME2, JSON_VALUE(i.[Data], N''$.orderedAt''), 126) AT TIME ZONE ''UTC'') AT TIME ZONE N''FLE Standard Time'' AS DATETIME2)), 1) AS DATETIME2)
OPTION (RECOMPILE);

SET @Check1 = (SELECT ISNULL(SUM([M0]), 0) FROM @Agg);
SET @Check2 = (SELECT COUNT(*) FROM @Agg);');

INSERT INTO [exp].[Queries] ([Variant], [Code], [Name], [Sql]) VALUES (N'json', N'Q5', N'Показник правила моніторингу і показник порівняння', N'-- Поточний місяць (Київ) і попередній місяць до того ж моменту; зараз = 2026-11-15 12:00 UTC.
SELECT @Check1 = SUM(TRY_CAST(JSON_VALUE(i.[Data], N''$.price'') AS DECIMAL(38, 10)))
FROM [dbo].[JsonInstances] i

WHERE i.[ConfigPkey] = @C
  AND JSON_VALUE(i.[Data], N''$.paid'') = N''true''
  AND i.[CreatedAt] >= ''2026-10-31T22:00:00''
  AND i.[CreatedAt] < ''2026-11-30T22:00:00''
OPTION (RECOMPILE);

SELECT @Check2 = SUM(TRY_CAST(JSON_VALUE(i.[Data], N''$.price'') AS DECIMAL(38, 10)))
FROM [dbo].[JsonInstances] i

WHERE i.[ConfigPkey] = @C
  AND JSON_VALUE(i.[Data], N''$.paid'') = N''true''
  AND i.[CreatedAt] >= ''2026-09-30T21:00:00''
  AND i.[CreatedAt] < ''2026-10-15T11:00:00''
OPTION (RECOMPILE);');
GO

/* ---------------------------------------------------------------------------
   1. Дані
--------------------------------------------------------------------------- */

EXEC [exp].[Generate] 10000;
EXEC [exp].[Generate] 100000;
EXEC [exp].[Generate] 1000000;
GO

RAISERROR(N'Оновлення статистики...', 0, 1) WITH NOWAIT;
UPDATE STATISTICS [dbo].[EntityInstances] WITH FULLSCAN;
UPDATE STATISTICS [dbo].[EntityPropertyInstances] WITH FULLSCAN;
UPDATE STATISTICS [dbo].[EavStringValues] WITH FULLSCAN;
UPDATE STATISTICS [dbo].[JsonInstances] WITH FULLSCAN;
GO

/* ---------------------------------------------------------------------------
   2. Відомості про сервер (умови експерименту)
--------------------------------------------------------------------------- */

INSERT INTO [exp].[Info] VALUES (N'Версія SQL Server', REPLACE(REPLACE(@@VERSION, CHAR(10), N' '), CHAR(13), N' '));
INSERT INTO [exp].[Info] VALUES (N'Редакція', CAST(SERVERPROPERTY('Edition') AS NVARCHAR(200)));
INSERT INTO [exp].[Info] VALUES (N'Рівень сумісності бази', (SELECT CAST([compatibility_level] AS NVARCHAR(10)) FROM sys.databases WHERE [name] = DB_NAME()));
INSERT INTO [exp].[Info] VALUES (N'max server memory (MB)', (SELECT CAST([value_in_use] AS NVARCHAR(20)) FROM sys.configurations WHERE [name] = N'max server memory (MB)'));
INSERT INTO [exp].[Info] VALUES (N'max degree of parallelism', (SELECT CAST([value_in_use] AS NVARCHAR(20)) FROM sys.configurations WHERE [name] = N'max degree of parallelism'));
BEGIN TRY
    EXEC sp_executesql N'
        INSERT INTO [exp].[Info]
        SELECT N''Логічних процесорів'', CAST([cpu_count] AS NVARCHAR(10)) FROM sys.dm_os_sys_info
        UNION ALL
        SELECT N''Оперативна памʼять ПК (МБ)'', CAST([physical_memory_kb] / 1024 AS NVARCHAR(20)) FROM sys.dm_os_sys_info;';
END TRY
BEGIN CATCH
    INSERT INTO [exp].[Info] VALUES (N'Процесор і памʼять', N'немає прав VIEW SERVER STATE - вкажіть вручну');
END CATCH
INSERT INTO [exp].[Info] VALUES (N'Дата запуску', CONVERT(NVARCHAR(30), SYSDATETIME(), 120));
GO

/* ---------------------------------------------------------------------------
   3. Вимірювання
--------------------------------------------------------------------------- */

RAISERROR(N'Вимірювання: typed, string, json...', 0, 1) WITH NOWAIT;
EXEC [exp].[Measure] 10000,   N'typed',  N'typed';
EXEC [exp].[Measure] 100000,  N'typed',  N'typed';
EXEC [exp].[Measure] 1000000, N'typed',  N'typed';
EXEC [exp].[Measure] 10000,   N'string', N'string';
EXEC [exp].[Measure] 100000,  N'string', N'string';
EXEC [exp].[Measure] 1000000, N'string', N'string';
EXEC [exp].[Measure] 10000,   N'json',   N'json';
EXEC [exp].[Measure] 100000,  N'json',   N'json';
EXEC [exp].[Measure] 1000000, N'json',   N'json';
GO

-- typed без фільтрованих індексів: ті самі запити, індекси за значеннями вимкнено
RAISERROR(N'Вимірювання: typed без фільтрованих індексів...', 0, 1) WITH NOWAIT;
ALTER INDEX IX_EntityPropertyInstances_Number ON [dbo].[EntityPropertyInstances] DISABLE;
ALTER INDEX IX_EntityPropertyInstances_Bool ON [dbo].[EntityPropertyInstances] DISABLE;
ALTER INDEX IX_EntityPropertyInstances_Date ON [dbo].[EntityPropertyInstances] DISABLE;
GO

EXEC [exp].[Measure] 10000,   N'typed', N'typed_noidx';
EXEC [exp].[Measure] 100000,  N'typed', N'typed_noidx';
EXEC [exp].[Measure] 1000000, N'typed', N'typed_noidx';
GO

ALTER INDEX IX_EntityPropertyInstances_Number ON [dbo].[EntityPropertyInstances] REBUILD;
ALTER INDEX IX_EntityPropertyInstances_Bool ON [dbo].[EntityPropertyInstances] REBUILD;
ALTER INDEX IX_EntityPropertyInstances_Date ON [dbo].[EntityPropertyInstances] REBUILD;
GO

/* ---------------------------------------------------------------------------
   4. Підсумок - ОДНА таблиця (збережіть її як CSV)
   Kind: query - запит; load - запис даних; size - обсяг зберігання; info - умови експерименту.
--------------------------------------------------------------------------- */

WITH [s] AS (
    SELECT DISTINCT
        r.[Size], r.[Variant], r.[Code],
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY r.[ElapsedMs]) OVER (PARTITION BY r.[Size], r.[Variant], r.[Code]) AS [MedianMs],
        MIN(r.[ElapsedMs]) OVER (PARTITION BY r.[Size], r.[Variant], r.[Code]) AS [MinMs],
        MAX(r.[ElapsedMs]) OVER (PARTITION BY r.[Size], r.[Variant], r.[Code]) AS [MaxMs],
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY r.[CpuMs]) OVER (PARTITION BY r.[Size], r.[Variant], r.[Code]) AS [MedianCpuMs],
        AVG(r.[LogicalReads]) OVER (PARTITION BY r.[Size], r.[Variant], r.[Code]) AS [LogicalReads],
        MIN(r.[Check1]) OVER (PARTITION BY r.[Size], r.[Variant], r.[Code]) AS [Check1],
        MIN(r.[Check2]) OVER (PARTITION BY r.[Size], r.[Variant], r.[Code]) AS [Check2]
    FROM [exp].[Results] r
),
     [sizes] AS (
         SELECT N'typed' AS [Variant], SUM(ps.[used_page_count]) * 8 / 1024.0 AS [MB]
         FROM sys.dm_db_partition_stats ps
         WHERE ps.[object_id] IN (OBJECT_ID(N'dbo.EntityInstances'), OBJECT_ID(N'dbo.EntityPropertyInstances'))
         UNION ALL
         SELECT N'string', SUM(ps.[used_page_count]) * 8 / 1024.0
         FROM sys.dm_db_partition_stats ps
         WHERE ps.[object_id] IN (OBJECT_ID(N'dbo.EntityInstances'), OBJECT_ID(N'dbo.EavStringValues'))
         UNION ALL
         SELECT N'json', SUM(ps.[used_page_count]) * 8 / 1024.0
         FROM sys.dm_db_partition_stats ps
         WHERE ps.[object_id] = OBJECT_ID(N'dbo.JsonInstances')
     )
SELECT
    N'query' AS [Kind], s.[Size], s.[Variant], s.[Code], q.[Name],
    CAST(s.[MedianMs] AS DECIMAL(18, 3)) AS [MedianMs], s.[MinMs], s.[MaxMs],
    CAST(s.[MedianCpuMs] AS DECIMAL(18, 1)) AS [MedianCpuMs], s.[LogicalReads],
    CAST(s.[MedianMs] / NULLIF(t.[MedianMs], 0) AS DECIMAL(18, 2)) AS [VsTyped],
    CASE WHEN s.[Check1] = t.[Check1] AND ISNULL(s.[Check2], -1) = ISNULL(t.[Check2], -1) THEN N'так' ELSE N'НІ' END AS [SameResult],
    s.[Check1], s.[Check2],
    CAST(NULL AS DECIMAL(18, 2)) AS [Value], CAST(NULL AS NVARCHAR(4000)) AS [Note]
FROM [s] s
         JOIN [exp].[Queries] q ON q.[Variant] = N'typed' AND q.[Code] = s.[Code]
         JOIN [s] t ON t.[Size] = s.[Size] AND t.[Code] = s.[Code] AND t.[Variant] = N'typed'

UNION ALL

SELECT N'load', l.[Size], l.[Variant], NULL, N'Запис даних (записів за секунду у Value)',
       l.[ElapsedMs], NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       CAST(l.[Size] / (l.[ElapsedMs] / 1000.0) AS DECIMAL(18, 2)), NULL
FROM [exp].[Loads] l

UNION ALL

SELECT N'size', 1110000, z.[Variant], NULL, N'Обсяг даних і індексів, МБ (у Value), усі 1 110 000 записів',
       NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, CAST(z.[MB] AS DECIMAL(18, 2)), NULL
FROM [sizes] z

UNION ALL

SELECT N'info', NULL, NULL, NULL, i.[Name], NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, i.[Value]
FROM [exp].[Info] i

ORDER BY [Kind] DESC, [Code], [Size], [Variant];
GO

RAISERROR(N'Готово. Збережіть таблицю з вкладки Results як CSV (див. початок файлу).', 0, 1) WITH NOWAIT;
GO

/* Видалення тестової бази після дослідження (виконайте окремо, виділивши ці рядки):

USE [master];
ALTER DATABASE [DataCollector_Experiment] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [DataCollector_Experiment];

*/