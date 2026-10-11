/*
    Тестові дані: 10 моделей різної структури (від 3 до 12 полів) по @RecordsPerModel записів у кожній.

    Моделі й поля описані даними (#Models, #Props), а значення генеруються одним універсальним запитом
    залежно від способу генерації поля:
      seq  - текст за шаблоном, '#' замінюється номером запису (ORD-000123);
      list - випадкове значення зі списку, розділеного '|';
      num  - випадкове число в діапазоні [MinValue; MaxValue] з Scale знаками після коми;
      bool - true з імовірністю TruePercent %.
    NullPercent - частка записів, у яких поле порожнє.

    Значення пишуться так само, як це робить API: у колонку свого типу, порожні не зберігаються.
    Випадковість для пари (запис, поле) береться з хешу їхніх ідентифікаторів, тож у межах одного
    запиту вона стабільна (NEWID() у SQL Server може обчислюватися повторно для кожного посилання).

    Скрипт можна запускати повторно: моделі з такими ключами видаляються разом із записами
    і створюються заново. Щоб додати модель - допишіть рядок у #Models і її поля в #Props.
*/
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- Потрібно для вставки в таблицю з фільтрованими індексами (sqlcmd за замовчуванням вимикає QUOTED_IDENTIFIER).
    SET QUOTED_IDENTIFIER ON;
    SET ANSI_NULLS ON;

    DECLARE @RecordsPerModel INT = 100000;
    DECLARE @DaysBack INT = 365;          -- дати створення записів рівномірно за останній рік
    DECLARE @Now DATETIME2 = SYSUTCDATETIME();

    DROP TABLE IF EXISTS #Models, #Props, #ListItems, #Rows;

    -------------------------------------------------------------------------------
    -- 1. Моделі
    -------------------------------------------------------------------------------
    CREATE TABLE #Models (
        [ModelIdx] INT PRIMARY KEY,
        [Key] NVARCHAR(32) NOT NULL,
        [Name] NVARCHAR(128) NOT NULL,
        [ConfigId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID());

    INSERT INTO #Models ([ModelIdx], [Key], [Name])
    VALUES
        (1,  N'shop_orders',     N'Замовлення магазину'),
        (2,  N'sensor_readings', N'Показники датчиків'),
        (3,  N'employees',       N'Співробітники'),
        (4,  N'support_tickets', N'Заявки підтримки'),
        (5,  N'lab_samples',     N'Лабораторні зразки'),
        (6,  N'deliveries',      N'Доставки'),
        (7,  N'energy_meters',   N'Показники лічильників'),
        (8,  N'student_grades',  N'Оцінки студентів'),
        (9,  N'site_visits',     N'Відвідування сайту'),
        (10, N'vehicle_trips',   N'Поїздки транспорту');

    -------------------------------------------------------------------------------
    -- 2. Поля моделей і спосіб генерації значень
    -------------------------------------------------------------------------------
    CREATE TABLE #Props (
        [ModelIdx] INT NOT NULL,
        [PropIdx] INT NOT NULL,
        [Key] NVARCHAR(32) NOT NULL,
        [Name] NVARCHAR(128) NOT NULL,
        [Gen] VARCHAR(4) NOT NULL,            -- seq | list | num | bool
        [Spec] NVARCHAR(1000) NULL,           -- шаблон (seq) або список значень (list)
        [MinValue] DECIMAL(19, 4) NULL,
        [MaxValue] DECIMAL(19, 4) NULL,
        [Scale] INT NULL,
        [TruePercent] INT NULL,
        [NullPercent] INT NOT NULL,
        [DataType] AS (CASE [Gen] WHEN 'num' THEN 1 WHEN 'bool' THEN 2 ELSE 0 END),  -- 0 Text, 1 Number, 2 Boolean
        [PropertyId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [PropertyPkey] INT NULL,
        [ItemCount] INT NULL,
        PRIMARY KEY ([ModelIdx], [PropIdx]));

    INSERT INTO #Props ([ModelIdx], [PropIdx], [Key], [Name], [Gen], [Spec], [MinValue], [MaxValue], [Scale], [TruePercent], [NullPercent])
    VALUES
        -- 1. Замовлення магазину (6 полів)
        (1, 1, N'number',       N'Номер',                    'seq',  N'ORD-#', NULL, NULL, NULL, NULL, 0),
        (1, 2, N'customer',     N'Клієнт',                   'list', N'ТОВ "Альфа Трейд"|ФОП Коваленко І.П.|ТОВ "Буковина Агро"|ПП "Волинь Сервіс"|ФОП Шевчук О.М.|ТОВ "Дніпро Логістик"|ТОВ "Карпати Груп"|ФОП Мельник А.В.|ТОВ "Поділля Маркет"|ПП "Луцьк Техно"|ТОВ "Одеса Фреш"|ФОП Бондар Н.С.', NULL, NULL, NULL, NULL, 0),
        (1, 3, N'quantity',     N'Кількість',                'num',  NULL, 1, 20, 0, NULL, 0),
        (1, 4, N'price',        N'Ціна, грн',                'num',  NULL, 10, 20000, 2, NULL, 0),
        (1, 5, N'discount',     N'Знижка, %',                'num',  NULL, 0, 30, 0, NULL, 20),
        (1, 6, N'paid',         N'Оплачено',                 'bool', NULL, NULL, NULL, NULL, 70, 0),

        -- 2. Показники датчиків (4 поля)
        (2, 1, N'sensor',       N'Датчик',                   'list', N'T-101|T-102|T-103|H-201|H-202|H-203|M-301|M-302', NULL, NULL, NULL, NULL, 0),
        (2, 2, N'temperature',  N'Температура, °C',          'num',  NULL, -20, 45, 1, NULL, 0),
        (2, 3, N'humidity',     N'Вологість, %',             'num',  NULL, 10, 100, 1, NULL, 5),
        (2, 4, N'alarm',        N'Тривога',                  'bool', NULL, NULL, NULL, NULL, 3, 0),

        -- 3. Співробітники (8 полів)
        (3, 1, N'full_name',    N'ПІБ',                      'list', N'Іваненко Олег|Петренко Марія|Коваль Андрій|Бойко Наталія|Ткаченко Дмитро|Кравченко Ірина|Савчук Олександр|Мельник Юлія|Шевчук Віктор|Поліщук Оксана|Лисенко Тарас|Мороз Катерина|Павленко Ігор|Руденко Світлана|Гнатюк Роман|Зайченко Анна', NULL, NULL, NULL, NULL, 0),
        (3, 2, N'department',   N'Відділ',                   'list', N'Розробка|Тестування|Продажі|Маркетинг|Бухгалтерія|Підтримка|HR', NULL, NULL, NULL, NULL, 0),
        (3, 3, N'position',     N'Посада',                   'list', N'Intern|Junior|Middle|Senior|Team Lead|Manager', NULL, NULL, NULL, NULL, 0),
        (3, 4, N'salary',       N'Зарплата, грн',            'num',  NULL, 15000, 150000, 0, NULL, 0),
        (3, 5, N'experience',   N'Стаж, років',              'num',  NULL, 0, 30, 0, NULL, 0),
        (3, 6, N'remote',       N'Віддалено',                'bool', NULL, NULL, NULL, NULL, 45, 0),
        (3, 7, N'email',        N'Email',                    'seq',  N'emp#@company.ua', NULL, NULL, NULL, NULL, 0),
        (3, 8, N'rating',       N'Рейтинг',                  'num',  NULL, 1, 5, 1, NULL, 10),

        -- 4. Заявки підтримки (7 полів)
        (4, 1, N'ticket',       N'Номер',                    'seq',  N'TCK-#', NULL, NULL, NULL, NULL, 0),
        (4, 2, N'category',     N'Категорія',                'list', N'Оплата|Доставка|Технічна проблема|Обмін|Повернення|Консультація', NULL, NULL, NULL, NULL, 0),
        (4, 3, N'priority',     N'Пріоритет',                'list', N'Низький|Середній|Високий|Критичний', NULL, NULL, NULL, NULL, 0),
        (4, 4, N'response_hours', N'Час відповіді, год',     'num',  NULL, 0, 72, 1, NULL, 0),
        (4, 5, N'resolved',     N'Вирішено',                 'bool', NULL, NULL, NULL, NULL, 85, 0),
        (4, 6, N'satisfaction', N'Оцінка клієнта',           'num',  NULL, 1, 5, 0, NULL, 40),
        (4, 7, N'operator',     N'Оператор',                 'list', N'Ірина|Олександр|Марія|Дмитро|Наталія|Андрій', NULL, NULL, NULL, NULL, 0),

        -- 5. Лабораторні зразки (5 полів)
        (5, 1, N'sample',       N'Код зразка',               'seq',  N'LAB-#', NULL, NULL, NULL, NULL, 0),
        (5, 2, N'ph',           N'pH',                       'num',  NULL, 4, 10, 2, NULL, 0),
        (5, 3, N'concentration', N'Концентрація, мг/л',      'num',  NULL, 0, 500, 3, NULL, 0),
        (5, 4, N'temperature',  N'Температура, °C',          'num',  NULL, 15, 30, 1, NULL, 0),
        (5, 5, N'passed',       N'Відповідає нормі',         'bool', NULL, NULL, NULL, NULL, 90, 0),

        -- 6. Доставки (9 полів)
        (6, 1, N'tracking',     N'ТТН',                      'seq',  N'TTN-#', NULL, NULL, NULL, NULL, 0),
        (6, 2, N'city',         N'Місто',                    'list', N'Київ|Львів|Луцьк|Рівне|Одеса|Харків|Дніпро|Вінниця|Житомир|Тернопіль', NULL, NULL, NULL, NULL, 0),
        (6, 3, N'carrier',      N'Перевізник',               'list', N'Нова пошта|Укрпошта|Meest|Делівері', NULL, NULL, NULL, NULL, 0),
        (6, 4, N'weight',       N'Вага, кг',                 'num',  NULL, 0.1, 50, 2, NULL, 0),
        (6, 5, N'distance',     N'Відстань, км',             'num',  NULL, 1, 1200, 0, NULL, 0),
        (6, 6, N'cost',         N'Вартість, грн',            'num',  NULL, 50, 1500, 2, NULL, 0),
        (6, 7, N'delivered',    N'Доставлено',               'bool', NULL, NULL, NULL, NULL, 92, 0),
        (6, 8, N'delay',        N'Затримка, год',            'num',  NULL, 0, 96, 0, NULL, 60),
        (6, 9, N'courier',      N'Кур''єр',                  'list', N'Петренко|Коваль|Бойко|Ткаченко|Кравченко|Савчук', NULL, NULL, NULL, NULL, 15),

        -- 7. Показники лічильників (3 поля)
        (7, 1, N'meter',        N'Лічильник',                'list', N'EM-001|EM-002|EM-003|EM-004|EM-005|EM-006|EM-007|EM-008|EM-009|EM-010|EM-011|EM-012', NULL, NULL, NULL, NULL, 0),
        (7, 2, N'kwh',          N'Споживання, кВт·год',      'num',  NULL, 0, 250, 3, NULL, 0),
        (7, 3, N'peak',         N'Пікове навантаження',      'bool', NULL, NULL, NULL, NULL, 15, 0),

        -- 8. Оцінки студентів (6 полів)
        (8, 1, N'student',      N'Студент',                  'list', N'Андрієнко Максим|Бондар Софія|Василенко Артем|Гребенюк Дарина|Демченко Іван|Єрмак Аліна|Жук Владислав|Захарчук Вікторія|Ігнатенко Богдан|Клименко Анастасія|Литвин Денис|Марченко Яна', NULL, NULL, NULL, NULL, 0),
        (8, 2, N'group',        N'Група',                    'list', N'КН-11|КН-12|КН-21|КН-22|КН-31|КН-32|КН-41|КН-42', NULL, NULL, NULL, NULL, 0),
        (8, 3, N'subject',      N'Дисципліна',               'list', N'Бази даних|Алгоритми|Веб-програмування|Комп''ютерні мережі|Математичний аналіз|ООП|Англійська мова', NULL, NULL, NULL, NULL, 0),
        (8, 4, N'score',        N'Бал',                      'num',  NULL, 0, 100, 0, NULL, 0),
        (8, 5, N'credits',      N'Кредити ECTS',             'num',  NULL, 3, 6, 0, NULL, 0),
        (8, 6, N'passed',       N'Зараховано',               'bool', NULL, NULL, NULL, NULL, 80, 0),

        -- 9. Відвідування сайту (10 полів)
        (9, 1, N'session',      N'Сесія',                    'seq',  N'S-#', NULL, NULL, NULL, NULL, 0),
        (9, 2, N'page',         N'Сторінка',                 'list', N'/|/catalog|/product|/cart|/checkout|/blog|/contacts|/about', NULL, NULL, NULL, NULL, 0),
        (9, 3, N'source',       N'Джерело',                  'list', N'Google|Facebook|Instagram|Прямий захід|Email-розсилка|Реклама', NULL, NULL, NULL, NULL, 0),
        (9, 4, N'device',       N'Пристрій',                 'list', N'Десктоп|Смартфон|Планшет', NULL, NULL, NULL, NULL, 0),
        (9, 5, N'browser',      N'Браузер',                  'list', N'Chrome|Firefox|Safari|Edge|Opera', NULL, NULL, NULL, NULL, 0),
        (9, 6, N'country',      N'Країна',                   'list', N'Україна|Польща|Німеччина|США|Чехія', NULL, NULL, NULL, NULL, 0),
        (9, 7, N'duration',     N'Тривалість, с',            'num',  NULL, 1, 1800, 0, NULL, 0),
        (9, 8, N'pages_viewed', N'Переглянуто сторінок',     'num',  NULL, 1, 25, 0, NULL, 0),
        (9, 9, N'bounced',      N'Відмова',                  'bool', NULL, NULL, NULL, NULL, 35, 0),
        (9, 10, N'converted',   N'Конверсія',                'bool', NULL, NULL, NULL, NULL, 4, 0),

        -- 10. Поїздки транспорту (12 полів)
        (10, 1, N'vehicle',     N'Транспорт',                'list', N'АС1234ВК|АС5678ВК|ВК1111АА|ВК2222АА|КА3333ІН|ВС4444ХО|АА5555ММ|ВО6666ТТ', NULL, NULL, NULL, NULL, 0),
        (10, 2, N'driver',      N'Водій',                    'list', N'Шевченко|Мельник|Бондаренко|Лисенко|Мороз|Павленко|Руденко|Зайченко', NULL, NULL, NULL, NULL, 0),
        (10, 3, N'route',       N'Маршрут',                  'list', N'Луцьк — Київ|Луцьк — Львів|Луцьк — Рівне|Київ — Одеса|Львів — Ужгород|Рівне — Житомир', NULL, NULL, NULL, NULL, 0),
        (10, 4, N'distance',    N'Відстань, км',             'num',  NULL, 20, 650, 1, NULL, 0),
        (10, 5, N'fuel',        N'Пальне, л',                'num',  NULL, 2, 80, 2, NULL, 0),
        (10, 6, N'avg_speed',   N'Середня швидкість, км/год', 'num', NULL, 30, 95, 1, NULL, 0),
        (10, 7, N'max_speed',   N'Максимальна швидкість, км/год', 'num', NULL, 60, 140, 0, NULL, 0),
        (10, 8, N'duration',    N'Тривалість, хв',           'num',  NULL, 20, 600, 0, NULL, 0),
        (10, 9, N'idle',        N'Простій, хв',              'num',  NULL, 0, 120, 0, NULL, 25),
        (10, 10, N'overspeed',  N'Перевищення швидкості',    'bool', NULL, NULL, NULL, NULL, 12, 0),
        (10, 11, N'engine_temp', N'Температура двигуна, °C', 'num',  NULL, 70, 115, 1, NULL, 10),
        (10, 12, N'fuel_cost',  N'Вартість пального, грн',   'num',  NULL, 100, 4500, 2, NULL, 0);

    -- Значення списків по одному в рядку з номером, щоб вибирати випадкове за індексом.
    CREATE TABLE #ListItems (
        [ModelIdx] INT NOT NULL,
        [PropIdx] INT NOT NULL,
        [Idx] INT NOT NULL,
        [Value] NVARCHAR(400) NOT NULL,
        PRIMARY KEY ([ModelIdx], [PropIdx], [Idx]));

    INSERT INTO #ListItems ([ModelIdx], [PropIdx], [Idx], [Value])
    SELECT p.[ModelIdx], p.[PropIdx],
           ROW_NUMBER() OVER (PARTITION BY p.[ModelIdx], p.[PropIdx] ORDER BY (SELECT NULL)),
           LTRIM(RTRIM(s.[value]))
    FROM #Props p
    CROSS APPLY STRING_SPLIT(p.[Spec], N'|') s
    WHERE p.[Gen] = 'list';

    UPDATE p
    SET [ItemCount] = (SELECT COUNT(*) FROM #ListItems li WHERE li.[ModelIdx] = p.[ModelIdx] AND li.[PropIdx] = p.[PropIdx])
    FROM #Props p
    WHERE p.[Gen] = 'list';

    CREATE TABLE #Rows (
        [N] INT PRIMARY KEY,
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL);

    -------------------------------------------------------------------------------
    -- 3. Генерація: модель за моделлю
    -------------------------------------------------------------------------------
    DECLARE @ModelIdx INT = 1;
    DECLARE @ModelCount INT = (SELECT COUNT(*) FROM #Models);
    DECLARE @ConfigId UNIQUEIDENTIFIER, @Key NVARCHAR(32), @Name NVARCHAR(128), @OldConfigId UNIQUEIDENTIFIER;
    DECLARE @ConfigPkey INT;
    DECLARE @Properties [dbo].[EntityPropertyList];
    DECLARE @Started DATETIME2, @ValueCount INT, @Seconds INT, @PropertyCount INT;

    WHILE @ModelIdx <= @ModelCount
    BEGIN
        SELECT @ConfigId = [ConfigId], @Key = [Key], @Name = [Name]
        FROM #Models
        WHERE [ModelIdx] = @ModelIdx;

        SET @Started = SYSUTCDATETIME();

        -- Стара версія моделі (якщо є) видаляється разом із записами
        SET @OldConfigId = (SELECT [Id] FROM [dbo].[EntityConfigs] WHERE [Key] = @Key);
        IF @OldConfigId IS NOT NULL
            EXEC [dbo].[sp_EntityConfigs_Delete] @Id = @OldConfigId;

        -- Модель і поля - через ту саму процедуру, що використовує API.
        -- CreatedAt полів зсунуто на мілісекунди, щоб вони показувались у заданому порядку.
        -- WHERE 1 = 1: табличну змінну треба очищати перед кожною моделлю; умова прибирає попередження IDE про DELETE без WHERE.
        DELETE FROM @Properties WHERE 1 = 1;

        INSERT INTO @Properties ([Id], [EntityConfigId], [Key], [Name], [DataType], [CreatedAt])
        SELECT [PropertyId], @ConfigId, [Key], [Name], [DataType], DATEADD(MILLISECOND, [PropIdx], @Now)
        FROM #Props
        WHERE [ModelIdx] = @ModelIdx;

        EXEC [dbo].[sp_EntityConfigs_Create]
             @Id = @ConfigId,
             @Key = @Key,
             @Name = @Name,
             @CreatedAt = @Now,
             @Properties = @Properties;

        -- Далі всі зв'язки - через внутрішні ключі, як у процедурах.
        SET @ConfigPkey = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @ConfigId);

        UPDATE p
        SET [PropertyPkey] = c.[Pkey]
        FROM #Props p
        INNER JOIN [dbo].[EntityPropertyConfigs] c ON c.[Id] = p.[PropertyId]
        WHERE p.[ModelIdx] = @ModelIdx;

        -- Записи моделі
        TRUNCATE TABLE #Rows;

        INSERT INTO #Rows ([N], [Id], [CreatedAt])
        SELECT TOP (@RecordsPerModel)
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
            NEWID(),
            -- & 2147483647 - невід'ємне число без ризику переповнення ABS
            DATEADD(SECOND, -((CHECKSUM(NEWID()) & 2147483647) % (@DaysBack * 86400)), @Now)
        FROM sys.all_objects a
        CROSS JOIN sys.all_objects b
        CROSS JOIN sys.all_objects c;

        BEGIN TRANSACTION;

        -- У хронологічному порядку, щоб внутрішні ключі записів ([Pkey]) зростали разом із датою створення,
        -- як це відбувається при надходженні даних через API.
        INSERT INTO [dbo].[EntityInstances] ([Id], [ConfigPkey], [CreatedAt])
        SELECT [Id], @ConfigPkey, [CreatedAt]
        FROM #Rows
        ORDER BY [CreatedAt];

        -- Значення всіх полів: запис x поле, кожне - у колонку свого типу; порожні пропускаються.
        INSERT INTO [dbo].[EntityPropertyInstances]
            ([InstancePkey], [PropertyPkey], [ValueText], [ValueNumber], [ValueBool])
        SELECT
            i.[Pkey],
            p.[PropertyPkey],
            CASE p.[Gen]
                WHEN 'seq' THEN REPLACE(p.[Spec], N'#', RIGHT(N'000000' + CAST(r.[N] AS NVARCHAR(10)), 6))
                WHEN 'list' THEN li.[Value]
            END,
            CASE WHEN p.[Gen] = 'num' THEN
                CAST(p.[MinValue]
                     + CAST(rnd.[R] % (CAST((p.[MaxValue] - p.[MinValue]) * POWER(10, p.[Scale]) AS BIGINT) + 1) AS DECIMAL(19, 4))
                       / POWER(CAST(10 AS DECIMAL(19, 4)), p.[Scale])
                     AS DECIMAL(38, 10))
            END,
            CASE WHEN p.[Gen] = 'bool' THEN
                CAST(CASE WHEN rnd.[R] % 100 < p.[TruePercent] THEN 1 ELSE 0 END AS BIT)
            END
        FROM #Rows r
        INNER JOIN [dbo].[EntityInstances] i ON i.[Id] = r.[Id]
        INNER JOIN #Props p ON p.[ModelIdx] = @ModelIdx
        -- Детермінована "випадковість" для пари (запис, поле): перші 4 байти хешу - значення, наступні 4 - пропуск.
        CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT(CAST(r.[Id] AS NVARCHAR(36)), N':', p.[PropIdx])) AS [H]) h
        CROSS APPLY (SELECT
            CAST(SUBSTRING(h.[H], 1, 4) AS INT) & 2147483647 AS [R],
            (CAST(SUBSTRING(h.[H], 5, 4) AS INT) & 2147483647) % 100 AS [NullRoll]) rnd
        LEFT JOIN #ListItems li
            ON li.[ModelIdx] = p.[ModelIdx]
           AND li.[PropIdx] = p.[PropIdx]
           AND li.[Idx] = rnd.[R] % p.[ItemCount] + 1
        WHERE rnd.[NullRoll] >= p.[NullPercent];

        SET @ValueCount = @@ROWCOUNT;

        COMMIT TRANSACTION;

        SET @Seconds = DATEDIFF(SECOND, @Started, SYSUTCDATETIME());
        SET @PropertyCount = (SELECT COUNT(*) FROM #Props WHERE [ModelIdx] = @ModelIdx);

        -- RAISERROR ... WITH NOWAIT виводить прогрес одразу, а не наприкінці скрипту (як PRINT)
        RAISERROR (N'[%d/%d] %s: %d полів, %d записів, %d значень, %d с',
                   0, 1, @ModelIdx, @ModelCount, @Key, @PropertyCount, @RecordsPerModel, @ValueCount, @Seconds) WITH NOWAIT;

        SET @ModelIdx += 1;
    END

    DROP TABLE #Rows, #ListItems;

    -------------------------------------------------------------------------------
    -- 4. Перевірка
    -------------------------------------------------------------------------------
    SELECT
        c.[Key],
        c.[Name],
        (SELECT COUNT(*) FROM [dbo].[EntityPropertyConfigs] p WHERE p.[ConfigPkey] = c.[Pkey]) AS [Properties],
        (SELECT COUNT(*) FROM [dbo].[EntityInstances] i WHERE i.[ConfigPkey] = c.[Pkey]) AS [Records],
        (SELECT COUNT(*)
         FROM [dbo].[EntityPropertyInstances] v
         INNER JOIN [dbo].[EntityInstances] i ON i.[Pkey] = v.[InstancePkey]
         WHERE i.[ConfigPkey] = c.[Pkey]) AS [Values]
    FROM [dbo].[EntityConfigs] c
    INNER JOIN #Models m ON m.[ConfigId] = c.[Id]
    ORDER BY m.[ModelIdx];

    DROP TABLE #Props, #Models;
END
