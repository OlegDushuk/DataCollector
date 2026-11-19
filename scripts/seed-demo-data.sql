/*
    Демонстраційні дані: інтернет-магазин електроніки.

    УВАГА: скрипт спочатку ВИДАЛЯЄ з бази всі моделі (разом із записами), дашборди, правила моніторингу
    і сповіщення, а потім створює демонстрацію заново. Користувачі та створені вручну API-ключі лишаються.

    Що створюється:
      - моделі «Замовлення», «Товари», «Повернення», «Відгуки» (усі шість типів полів і правила полів);
      - історія приблизно за 14 місяців до моменту запуску: зростання продажів, тижнева й річна сезонність,
        «чорна пʼятниця», денний профіль замовлень за київським часом (≈ 45 000 замовлень);
      - повернення й відгуки, повʼязані з доставленими замовленнями, і каталог товарів з рейтингом за відгуками;
      - контрольована аномалія для демонстрації моніторингу: за останні ≈ 20 годин у мобільному застосунку
        скасовується більшість замовлень з онлайн-оплатою («збій оплати»);
      - дашборди «Продажі інтернет-магазину» і «Склад, повернення та відгуки»;
      - правила моніторингу (дві тривоги одразу після запуску, решта - у нормі);
      - демонстраційні користувачі (якщо їх ще немає) і API-ключ «Сайт магазину».

    Дані будуються відносно моменту запуску, тому скрипт варто запускати в день демонстрації:
    правила за останню добу і поточний місяць працюють з «живими» даними.
    Випадкові значення обчислюються з хешу номера запису, тож повторний запуск у той самий момент дає ті самі дані.
*/
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- Потрібно для вставки в таблицю з фільтрованими індексами (sqlcmd за замовчуванням вимикає QUOTED_IDENTIFIER).
    SET QUOTED_IDENTIFIER ON;
    SET ANSI_NULLS ON;
    -- Текстові колонки тимчасових таблиць - у кодуванні (collation) бази: tempdb може мати інше,
    -- і тоді порівняння з таблицями бази дає помилку 468.

    IF OBJECT_ID(N'[dbo].[EntityConfigs]') IS NULL OR OBJECT_ID(N'[dbo].[sp_EntityConfigs_Create]') IS NULL
        THROW 50000, N'Це не база DataCollector: спочатку опублікуйте схему (publish-db.ps1).', 1;

    DECLARE @Now DATETIME2 = SYSUTCDATETIME();
    DECLARE @Tz NVARCHAR(64) = N'FLE Standard Time';     -- Київ (UTC+2, улітку UTC+3)
    DECLARE @NowLocal DATETIME2 = CAST((@Now AT TIME ZONE 'UTC') AT TIME ZONE @Tz AS DATETIME2);
    DECLARE @TodayLocal DATE = CAST(@NowLocal AS DATE);
    -- Історія - з першого числа місяця 14 місяців тому (є і той самий місяць торік).
    DECLARE @FirstDay DATE = DATEFROMPARTS(YEAR(DATEADD(MONTH, -14, @TodayLocal)), MONTH(DATEADD(MONTH, -14, @TodayLocal)), 1);
    DECLARE @Days INT = DATEDIFF(DAY, @FirstDay, @TodayLocal) + 1;
    DECLARE @BasePerDay FLOAT = 115;                       -- замовлень за звичайний день у кінці періоду ≈ 115-140

    DROP TABLE IF EXISTS #Num, #PickSrc, #Pick, #Cat, #Tpl, #ProdRaw, #Prod, #Days, #Orders, #Returns, #Reviews,
                         #Models, #Fields, #Inst, #V;

    -------------------------------------------------------------------------------
    -- 1. Очищення
    -------------------------------------------------------------------------------
    RAISERROR (N'1. Очищення бази...', 0, 1) WITH NOWAIT;

    DELETE FROM [dbo].[Dashboards];         -- віджети - каскадно
    DELETE FROM [dbo].[MonitoringRules];    -- сповіщення - каскадно

    DECLARE @DelId UNIQUEIDENTIFIER, @DelKey NVARCHAR(32);
    WHILE 1 = 1
    BEGIN
        SET @DelId = NULL;
        SELECT TOP (1) @DelId = [Id], @DelKey = [Key] FROM [dbo].[EntityConfigs] ORDER BY [Pkey];
        IF @DelId IS NULL
            BREAK;

        EXEC [dbo].[sp_EntityConfigs_Delete] @Id = @DelId;
        RAISERROR (N'   видалено модель %s', 0, 1, @DelKey) WITH NOWAIT;
    END

    -- Демонстраційні ключі (старий «Датчики теплиці» і ключ магазину з попереднього запуску).
    DELETE FROM [dbo].[ApiKeys]
    WHERE [Id] IN ('6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8fb1', '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8fb2');

    -------------------------------------------------------------------------------
    -- 2. Довідники
    -------------------------------------------------------------------------------
    RAISERROR (N'2. Генерація даних...', 0, 1) WITH NOWAIT;

    SELECT TOP (2000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS INT) AS [N]
    INTO #Num
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b;

    -- Списки значень з вагами: вибір - залишок від ділення випадкового числа на суму ваг.
    CREATE TABLE #PickSrc (
        [List] VARCHAR(16) COLLATE DATABASE_DEFAULT NOT NULL,
        [Idx] INT NOT NULL,
        [Value] NVARCHAR(200) COLLATE DATABASE_DEFAULT NOT NULL,
        [W] INT NOT NULL,
        PRIMARY KEY ([List], [Idx]));

    INSERT INTO #PickSrc ([List], [Idx], [Value], [W])
    VALUES
        ('hour', 1, N'0', 12),
        ('hour', 2, N'1', 6),
        ('hour', 3, N'2', 3),
        ('hour', 4, N'3', 2),
        ('hour', 5, N'4', 2),
        ('hour', 6, N'5', 4),
        ('hour', 7, N'6', 10),
        ('hour', 8, N'7', 22),
        ('hour', 9, N'8', 38),
        ('hour', 10, N'9', 52),
        ('hour', 11, N'10', 62),
        ('hour', 12, N'11', 64),
        ('hour', 13, N'12', 62),
        ('hour', 14, N'13', 60),
        ('hour', 15, N'14', 56),
        ('hour', 16, N'15', 54),
        ('hour', 17, N'16', 55),
        ('hour', 18, N'17', 58),
        ('hour', 19, N'18', 64),
        ('hour', 20, N'19', 72),
        ('hour', 21, N'20', 78),
        ('hour', 22, N'21', 74),
        ('hour', 23, N'22', 52),
        ('hour', 24, N'23', 28),
        ('cat', 1, N'Смартфони', 20),
        ('cat', 2, N'Ноутбуки', 9),
        ('cat', 3, N'Аудіо', 14),
        ('cat', 4, N'Аксесуари', 27),
        ('cat', 5, N'Побутова техніка', 12),
        ('cat', 6, N'Ігри та консолі', 8),
        ('cat', 7, N'Розумний дім', 10),
        ('items', 1, N'1', 78),
        ('items', 2, N'2', 16),
        ('items', 3, N'3', 5),
        ('items', 4, N'4', 1),
        ('items_acc', 1, N'1', 55),
        ('items_acc', 2, N'2', 30),
        ('items_acc', 3, N'3', 12),
        ('items_acc', 4, N'4', 3),
        ('disc', 1, N'0', 70),
        ('disc', 2, N'5', 12),
        ('disc', 3, N'10', 11),
        ('disc', 4, N'15', 7),
        ('disc_sale', 1, N'0', 15),
        ('disc_sale', 2, N'10', 20),
        ('disc_sale', 3, N'15', 25),
        ('disc_sale', 4, N'20', 25),
        ('disc_sale', 5, N'30', 15),
        ('channel', 1, N'Сайт', 52),
        ('channel', 2, N'Мобільний застосунок', 31),
        ('channel', 3, N'Instagram', 9),
        ('channel', 4, N'Телефон', 8),
        ('payment', 1, N'Картка онлайн', 47),
        ('payment', 2, N'Apple Pay / Google Pay', 20),
        ('payment', 3, N'Накладений платіж', 27),
        ('payment', 4, N'Оплата частинами', 6),
        ('delivery', 1, N'Нова пошта', 70),
        ('delivery', 2, N'Самовивіз', 12),
        ('delivery', 3, N'Курʼєр', 9),
        ('delivery', 4, N'Укрпошта', 9),
        ('city', 1, N'Київ', 30),
        ('city', 2, N'Львів', 11),
        ('city', 3, N'Харків', 9),
        ('city', 4, N'Одеса', 9),
        ('city', 5, N'Дніпро', 9),
        ('city', 6, N'Луцьк', 6),
        ('city', 7, N'Вінниця', 5),
        ('city', 8, N'Запоріжжя', 4),
        ('city', 9, N'Рівне', 4),
        ('city', 10, N'Івано-Франківськ', 4),
        ('city', 11, N'Тернопіль', 3),
        ('city', 12, N'Житомир', 3),
        ('city', 13, N'Полтава', 3),
        ('first', 1, N'Олена', 1),
        ('first', 2, N'Андрій', 1),
        ('first', 3, N'Марія', 1),
        ('first', 4, N'Олександр', 1),
        ('first', 5, N'Ірина', 1),
        ('first', 6, N'Дмитро', 1),
        ('first', 7, N'Наталія', 1),
        ('first', 8, N'Сергій', 1),
        ('first', 9, N'Юлія', 1),
        ('first', 10, N'Максим', 1),
        ('first', 11, N'Оксана', 1),
        ('first', 12, N'Віктор', 1),
        ('first', 13, N'Катерина', 1),
        ('first', 14, N'Тарас', 1),
        ('first', 15, N'Анна', 1),
        ('first', 16, N'Богдан', 1),
        ('first', 17, N'Світлана', 1),
        ('first', 18, N'Роман', 1),
        ('first', 19, N'Софія', 1),
        ('first', 20, N'Ігор', 1),
        ('last', 1, N'Коваленко', 1),
        ('last', 2, N'Шевчук', 1),
        ('last', 3, N'Бондаренко', 1),
        ('last', 4, N'Мельник', 1),
        ('last', 5, N'Ткачук', 1),
        ('last', 6, N'Кравчук', 1),
        ('last', 7, N'Олійник', 1),
        ('last', 8, N'Лисенко', 1),
        ('last', 9, N'Савчук', 1),
        ('last', 10, N'Гнатюк', 1),
        ('last', 11, N'Поліщук', 1),
        ('last', 12, N'Мороз', 1),
        ('last', 13, N'Марченко', 1),
        ('last', 14, N'Литвин', 1),
        ('last', 15, N'Павлюк', 1),
        ('last', 16, N'Руденко', 1),
        ('last', 17, N'Гончар', 1),
        ('last', 18, N'Ковальчук', 1),
        ('last', 19, N'Остапчук', 1),
        ('last', 20, N'Демчук', 1),
        ('last', 21, N'Климчук', 1),
        ('last', 22, N'Петренко', 1),
        ('last', 23, N'Савченко', 1),
        ('last', 24, N'Яковенко', 1),
        ('reason', 1, N'Не підійшов', 30),
        ('reason', 2, N'Брак або несправність', 28),
        ('reason', 3, N'Не відповідає опису', 14),
        ('reason', 4, N'Пошкоджено під час доставки', 10),
        ('reason', 5, N'Передумав', 18),
        ('rating', 1, N'5', 62),
        ('rating', 2, N'4', 23),
        ('rating', 3, N'3', 7),
        ('rating', 4, N'2', 3),
        ('rating', 5, N'1', 5),
        ('rating_weak', 1, N'5', 28),
        ('rating_weak', 2, N'4', 24),
        ('rating_weak', 3, N'3', 18),
        ('rating_weak', 4, N'2', 12),
        ('rating_weak', 5, N'1', 18),
        ('c5', 1, N'Усе чудово, рекомендую', 1),
        ('c5', 2, N'Швидка доставка, товар відповідає опису', 1),
        ('c5', 3, N'Дуже задоволений покупкою', 1),
        ('c5', 4, N'Якісний товар за свої гроші', 1),
        ('c5', 5, N'Добре упаковано, усе працює', 1),
        ('c5', 6, N'Купую тут не вперше, усе як завжди добре', 1),
        ('c4', 1, N'Загалом добре, але доставка трохи затрималася', 1),
        ('c4', 2, N'Хороший товар, є дрібні зауваження', 1),
        ('c4', 3, N'Відповідає ціні', 1),
        ('c4', 4, N'Усе працює, але інструкція могла б бути кращою', 1),
        ('c3', 1, N'Нормально, але очікував більшого', 1),
        ('c3', 2, N'Є недоліки, але користуватися можна', 1),
        ('c3', 3, N'Середня якість за таку ціну', 1),
        ('c12', 1, N'Товар прийшов з дефектом', 1),
        ('c12', 2, N'Не відповідає опису на сайті', 1),
        ('c12', 3, N'Розчарований якістю', 1),
        ('c12', 4, N'Довго чекав на доставку', 1);

    SELECT [List], [Idx], [Value], [W],
           SUM([W]) OVER (PARTITION BY [List] ORDER BY [Idx] ROWS UNBOUNDED PRECEDING) - [W] AS [CumFrom],
           SUM([W]) OVER (PARTITION BY [List] ORDER BY [Idx] ROWS UNBOUNDED PRECEDING) AS [CumTo],
           SUM([W]) OVER (PARTITION BY [List]) AS [Total]
    INTO #Pick
    FROM #PickSrc;

    -- Частка повернень за категорією, проміле від доставлених замовлень.
    CREATE TABLE #Cat ([Name] NVARCHAR(64) COLLATE DATABASE_DEFAULT PRIMARY KEY, [RetPermille] INT NOT NULL);
    INSERT INTO #Cat ([Name], [RetPermille])
    VALUES
        (N'Смартфони', 32),
        (N'Ноутбуки', 28),
        (N'Аудіо', 36),
        (N'Аксесуари', 20),
        (N'Побутова техніка', 28),
        (N'Ігри та консолі', 20),
        (N'Розумний дім', 24);

    -------------------------------------------------------------------------------
    -- 3. Каталог товарів: шаблон × модель × колір
    -------------------------------------------------------------------------------
    CREATE TABLE #Tpl (
        [T] INT PRIMARY KEY,
        [Category] NVARCHAR(64) COLLATE DATABASE_DEFAULT NOT NULL,
        [TypeName] NVARCHAR(64) COLLATE DATABASE_DEFAULT NOT NULL,
        [Brand] NVARCHAR(64) COLLATE DATABASE_DEFAULT NOT NULL,
        [Models] NVARCHAR(400) COLLATE DATABASE_DEFAULT NOT NULL,
        [Colours] NVARCHAR(200) COLLATE DATABASE_DEFAULT NULL,
        [PMin] FLOAT NOT NULL,
        [PMax] FLOAT NOT NULL);

    INSERT INTO #Tpl ([T], [Category], [TypeName], [Brand], [Models], [Colours], [PMin], [PMax])
    VALUES
        (1, N'Смартфони', N'Смартфон', N'Samsung', N'Galaxy A16|Galaxy A26|Galaxy A36|Galaxy A56|Galaxy S25|Galaxy S25 Ultra', N'чорний|синій|сірий', 6500, 52000),
        (2, N'Смартфони', N'Смартфон', N'Apple', N'iPhone 15|iPhone 16|iPhone 16 Pro|iPhone 16 Pro Max', N'чорний|білий', 31000, 63000),
        (3, N'Смартфони', N'Смартфон', N'Xiaomi', N'Redmi 14C|Redmi Note 14|Redmi Note 14 Pro|Xiaomi 14T', N'чорний|синій|зелений', 4500, 22000),
        (4, N'Смартфони', N'Смартфон', N'Motorola', N'Moto G35|Moto G55|Edge 50', N'чорний|сірий', 5500, 16000),
        (5, N'Смартфони', N'Смартфон', N'Google', N'Pixel 9a|Pixel 9|Pixel 9 Pro', N'чорний|білий', 22000, 42000),
        (6, N'Ноутбуки', N'Ноутбук', N'Lenovo', N'IdeaPad Slim 3|IdeaPad 5|ThinkPad E14|Legion 5', NULL, 18000, 60000),
        (7, N'Ноутбуки', N'Ноутбук', N'ASUS', N'Vivobook 15|Zenbook 14|TUF Gaming A15|ROG Strix G16', NULL, 19000, 75000),
        (8, N'Ноутбуки', N'Ноутбук', N'HP', N'250 G10|Pavilion 15|Victus 15', NULL, 15000, 40000),
        (9, N'Ноутбуки', N'Ноутбук', N'Apple', N'MacBook Air 13 M3|MacBook Air 15 M3|MacBook Pro 14 M4', N'сірий|сріблястий', 48000, 90000),
        (10, N'Ноутбуки', N'Ноутбук', N'Acer', N'Aspire 3|Swift Go 14|Nitro V15', NULL, 15000, 42000),
        (11, N'Аудіо', N'Навушники', N'Apple', N'AirPods 4|AirPods Pro 2', NULL, 6000, 10000),
        (12, N'Аудіо', N'Навушники', N'Sony', N'WH-CH520|WF-1000XM5|WH-1000XM5', N'чорний|бежевий', 2000, 14000),
        (13, N'Аудіо', N'Навушники', N'JBL', N'Tune 520BT|Tune 770NC|Live Pro 2', N'чорний|синій|білий', 1600, 5500),
        (14, N'Аудіо', N'Колонка', N'JBL', N'Go 4|Flip 6|Charge 5|PartyBox 110', N'чорний|синій', 1700, 17000),
        (15, N'Аудіо', N'Колонка', N'Marshall', N'Emberton II|Acton III', NULL, 6500, 12000),
        (16, N'Аксесуари', N'Зарядний пристрій', N'Baseus', N'20 Вт|30 Вт|65 Вт GaN', N'білий|чорний', 350, 1500),
        (17, N'Аксесуари', N'Павербанк', N'Xiaomi', N'10000 мА·год|20000 мА·год', NULL, 700, 1300),
        (18, N'Аксесуари', N'Кабель', N'Ugreen', N'USB-C – USB-C 1 м|USB-C – Lightning 1 м|USB-C – USB-C 2 м', N'чорний|білий', 250, 600),
        (19, N'Аксесуари', N'Захисне скло', N'2E', N'для iPhone 16|для iPhone 16 Pro|для Galaxy S25', NULL, 300, 500),
        (20, N'Аксесуари', N'Чохол', N'Spigen', N'для iPhone 16|для iPhone 16 Pro|для Galaxy S25', N'чорний|прозорий', 600, 1300),
        (21, N'Аксесуари', N'Карта памʼяті', N'SanDisk', N'microSD 64 ГБ|microSD 128 ГБ|microSD 256 ГБ', NULL, 350, 1300),
        (22, N'Аксесуари', N'Миша', N'Logitech', N'M190|M240 Silent|MX Master 3S', N'чорний|сірий', 400, 4600),
        (23, N'Аксесуари', N'Клавіатура', N'Logitech', N'K120|K380|MX Keys S', NULL, 1000, 5000),
        (24, N'Побутова техніка', N'Робот-пилосос', N'Xiaomi', N'Robot Vacuum E10|Robot Vacuum S20|Robot Vacuum X20', N'білий|чорний', 6000, 16000),
        (25, N'Побутова техніка', N'Пилосос', N'Dyson', N'V8|V12 Detect Slim|V15 Detect', NULL, 15000, 30000),
        (26, N'Побутова техніка', N'Кавомашина', N'DeʼLonghi', N'Magnifica S|Dinamica|Rivelia', NULL, 13000, 40000),
        (27, N'Побутова техніка', N'Мультипіч', N'Philips', N'Airfryer 3000|Airfryer 5000 XXL', NULL, 3200, 7500),
        (28, N'Побутова техніка', N'Електрочайник', N'Tefal', N'Express|Sense', N'білий|чорний', 1200, 2500),
        (29, N'Побутова техніка', N'Мікрохвильова піч', N'Samsung', N'MS23K3614AW|MG23K3515AK', NULL, 3500, 5200),
        (30, N'Ігри та консолі', N'Консоль', N'Sony', N'PlayStation 5 Slim|PlayStation 5 Pro', NULL, 22000, 33000),
        (31, N'Ігри та консолі', N'Консоль', N'Nintendo', N'Switch OLED|Switch 2', NULL, 13000, 20000),
        (32, N'Ігри та консолі', N'Геймпад', N'Sony', N'DualSense', N'білий|чорний', 2900, 2900),
        (33, N'Ігри та консолі', N'Геймпад', N'Microsoft', N'Xbox Wireless Controller', N'чорний|білий', 2500, 2500),
        (34, N'Ігри та консолі', N'Гра', N'Sony', N'Astro Bot (PS5)|Gran Turismo 7 (PS5)|EA Sports FC 26 (PS5)', NULL, 1800, 2600),
        (35, N'Розумний дім', N'Розумна лампа', N'Xiaomi', N'Mi Smart LED Bulb|Smart Light Strip', NULL, 350, 1500),
        (36, N'Розумний дім', N'Камера', N'TP-Link', N'Tapo C200|Tapo C520WS', NULL, 1200, 3200),
        (37, N'Розумний дім', N'Розумна розетка', N'TP-Link', N'Tapo P100|Tapo P110', NULL, 400, 550),
        (38, N'Розумний дім', N'Датчик', N'Aqara', N'відкриття дверей|протікання води|руху', NULL, 600, 1000),
        (39, N'Розумний дім', N'Wi-Fi роутер', N'TP-Link', N'Archer AX23|Deco X50 (2 шт.)', NULL, 2300, 8000);

    SELECT t.[T], t.[Category], t.[TypeName], t.[Brand],
           CAST(m.[key] AS INT) AS [Mi], m.[value] AS [Model],
           CAST(c.[key] AS INT) AS [Ci], NULLIF(c.[value], N'') AS [Colour],
           t.[PMin], t.[PMax]
    INTO #ProdRaw
    FROM #Tpl t
    CROSS APPLY OPENJSON(N'["' + REPLACE(t.[Models], N'|', N'","') + N'"]') m
    CROSS APPLY OPENJSON(N'["' + REPLACE(ISNULL(t.[Colours], N''), N'|', N'","') + N'"]') c;

    CREATE TABLE #Prod (
        [P] INT PRIMARY KEY,
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [Sku] NVARCHAR(16) COLLATE DATABASE_DEFAULT NULL,
        [Name] NVARCHAR(200) COLLATE DATABASE_DEFAULT NOT NULL,
        [Category] NVARCHAR(64) COLLATE DATABASE_DEFAULT NOT NULL,
        [Brand] NVARCHAR(64) COLLATE DATABASE_DEFAULT NOT NULL,
        [Price] DECIMAL(19, 2) NOT NULL,
        [Stock] INT NULL,
        [Active] BIT NULL,
        [ArrivedOn] DATE NULL,
        [CreatedAt] DATETIME2 NULL,
        [UpdatedAt] DATETIME2 NULL,
        [Weak] BIT NULL,                  -- товар зі слабшими відгуками
        [Weight] INT NULL,                -- популярність
        [CumFrom] INT NULL,
        [CumTo] INT NULL,
        [CatTotal] INT NULL,
        [Rating] DECIMAL(19, 1) NULL);

    -- Ціна - від мінімальної до максимальної в геометричній прогресії за моделлю (колір на ціну не впливає), закінчується на 9.
    INSERT INTO #Prod ([P], [Name], [Category], [Brand], [Price])
    SELECT ROW_NUMBER() OVER (ORDER BY r.[T], r.[Mi], r.[Ci]),
           CONCAT(r.[TypeName], N' ', r.[Brand], N' ', r.[Model], IIF(r.[Colour] IS NULL, N'', N', ' + r.[Colour])),
           r.[Category],
           r.[Brand],
           CAST(CEILING(r.[PMin] * POWER(r.[PMax] / r.[PMin], IIF(mc.[Cnt] > 1, r.[Mi] * 1.0 / (mc.[Cnt] - 1), 0.0))
                        / 10) * 10 - 1 AS DECIMAL(19, 2))
    FROM #ProdRaw r
    CROSS APPLY (SELECT COUNT(DISTINCT x.[Mi]) AS [Cnt] FROM #ProdRaw x WHERE x.[T] = r.[T]) mc;

    UPDATE p
    SET [Sku] = CONCAT(N'SKU-', 10000 + p.[P]),
        [Active] = IIF(x.[R1] % 100 < 3, 0, 1),
        [Stock] = CASE WHEN x.[R1] % 100 < 3 THEN 0
                       WHEN x.[R1] % 100 < 7 THEN x.[R2] % 5
                       ELSE 6 + x.[R2] % 140 END,
        [ArrivedOn] = DATEADD(DAY, -(x.[R3] % 90), @TodayLocal),
        [CreatedAt] = DATEADD(MINUTE, -(x.[R4] % (60 * 24 * 60)), DATEADD(DAY, -@Days, @Now)),
        [Weak] = IIF(x.[R5] % 12 = 0, 1, 0)
    FROM #Prod p
    CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT(N'p:', p.[P])) AS [H]) h
    CROSS APPLY (SELECT CAST(SUBSTRING(h.[H], 1, 4) AS INT) & 2147483647 AS [R1], CAST(SUBSTRING(h.[H], 5, 4) AS INT) & 2147483647 AS [R2], CAST(SUBSTRING(h.[H], 9, 4) AS INT) & 2147483647 AS [R3], CAST(SUBSTRING(h.[H], 13, 4) AS INT) & 2147483647 AS [R4], CAST(SUBSTRING(h.[H], 17, 4) AS INT) & 2147483647 AS [R5]) x;

    -- Оновлення картки товару - останнє надходження (не пізніше за поточний момент).
    UPDATE #Prod
    SET [UpdatedAt] = IIF(DATEADD(HOUR, 8, CAST([ArrivedOn] AS DATETIME2)) > @Now, @Now, DATEADD(HOUR, 8, CAST([ArrivedOn] AS DATETIME2)));

    -- Популярність усередині категорії: перший за «рейтингом» продається найчастіше (розподіл Ципфа).
    ;WITH w AS (
        SELECT [P], 1 + 2000 / (1 + ROW_NUMBER() OVER (PARTITION BY [Category]
                                    ORDER BY HASHBYTES('SHA2_256', CONCAT(N'w:', [P])))) AS [W]
        FROM #Prod)
    UPDATE p SET [Weight] = w.[W]
    FROM #Prod p
    INNER JOIN w ON w.[P] = p.[P];

    ;WITH c AS (
        SELECT [P],
               SUM([Weight]) OVER (PARTITION BY [Category] ORDER BY [P] ROWS UNBOUNDED PRECEDING) AS [CumTo],
               SUM([Weight]) OVER (PARTITION BY [Category]) AS [Total]
        FROM #Prod)
    UPDATE p SET [CumTo] = c.[CumTo], [CumFrom] = c.[CumTo] - p.[Weight], [CatTotal] = c.[Total]
    FROM #Prod p
    INNER JOIN c ON c.[P] = p.[P];

    -------------------------------------------------------------------------------
    -- 4. Замовлення
    -------------------------------------------------------------------------------
    -- Кількість замовлень за день: зростання бізнесу, день тижня, місяць, акції, випадкові коливання.
    CREATE TABLE #Days (
        [DayIdx] INT PRIMARY KEY,
        [D] DATE NOT NULL,
        [Wd] INT NULL,                    -- 0 - понеділок ... 6 - неділя
        [Sale] BIT NULL,                  -- «чорна пʼятниця», вихідні після неї, кіберпонеділок
        [Cnt] INT NULL);

    INSERT INTO #Days ([DayIdx], [D])
    SELECT [N] - 1, DATEADD(DAY, [N] - 1, @FirstDay)
    FROM #Num
    WHERE [N] <= @Days;

    UPDATE #Days SET [Wd] = DATEDIFF(DAY, '19000101', [D]) % 7;

    UPDATE d
    SET [Sale] = IIF(f.[Bf] = 1 OR f.[BfWeekend] = 1 OR f.[CyberMonday] = 1, 1, 0),
        [Cnt] = CAST(ROUND(
                    @BasePerDay
                    * (0.72 + 0.43 * d.[DayIdx] / (@Days - 1.0))
                    * CHOOSE(d.[Wd] + 1, 1.08, 1.05, 1.0, 1.0, 0.95, 0.88, 0.97)
                    * CHOOSE(MONTH(d.[D]), 0.82, 0.85, 0.95, 0.93, 0.92, 0.88, 0.86, 0.98, 1.0, 1.0, 1.08, 1.30)
                    * CASE WHEN f.[Bf] = 1 THEN 3.0
                           WHEN f.[BfWeekend] = 1 THEN 1.8
                           WHEN f.[CyberMonday] = 1 THEN 1.9
                           WHEN MONTH(d.[D]) = 12 AND DAY(d.[D]) BETWEEN 15 AND 24 THEN 1.2
                           WHEN MONTH(d.[D]) = 1 AND DAY(d.[D]) <= 7 THEN 0.7
                           ELSE 1.0 END
                    * (0.85 + (CAST(SUBSTRING(HASHBYTES('SHA2_256', CONCAT(N'd:', d.[D])), 1, 4) AS INT) & 2147483647) % 31 / 100.0),
                0) AS INT)
    FROM #Days d
    -- «Чорна пʼятниця» - пʼятниця між 23 і 29 листопада.
    CROSS APPLY (SELECT
        IIF(MONTH(d.[D]) = 11 AND DAY(d.[D]) BETWEEN 23 AND 29 AND d.[Wd] = 4, 1, 0) AS [Bf],
        IIF(d.[Wd] IN (5, 6) AND MONTH(DATEADD(DAY, 4 - d.[Wd], d.[D])) = 11
            AND DAY(DATEADD(DAY, 4 - d.[Wd], d.[D])) BETWEEN 23 AND 29, 1, 0) AS [BfWeekend],
        IIF(d.[Wd] = 0 AND MONTH(DATEADD(DAY, -3, d.[D])) = 11
            AND DAY(DATEADD(DAY, -3, d.[D])) BETWEEN 23 AND 29, 1, 0) AS [CyberMonday]) f;

    CREATE TABLE #Orders (
        [N] INT NULL,
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [DayIdx] INT NOT NULL,
        [K] INT NOT NULL,
        [Sale] BIT NOT NULL,
        [LocalAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NULL,
        [UpdatedAt] DATETIME2 NULL,
        [R1] INT NULL, [R2] INT NULL, [R3] INT NULL, [R4] INT NULL, [R5] INT NULL, [R6] INT NULL, [R7] INT NULL,
        [R8] INT NULL, [R9] INT NULL, [R10] INT NULL, [R11] INT NULL, [R12] INT NULL, [R13] INT NULL, [R14] INT NULL,
        [Number] NVARCHAR(16) COLLATE DATABASE_DEFAULT NULL,
        [Category] NVARCHAR(64) COLLATE DATABASE_DEFAULT NULL,
        [P] INT NULL,
        [Product] NVARCHAR(200) COLLATE DATABASE_DEFAULT NULL,
        [UnitPrice] DECIMAL(19, 2) NULL,
        [Items] INT NULL,
        [Discount] INT NULL,
        [Amount] DECIMAL(19, 2) NULL,
        [Channel] NVARCHAR(64) COLLATE DATABASE_DEFAULT NULL,
        [Payment] NVARCHAR(64) COLLATE DATABASE_DEFAULT NULL,
        [Delivery] NVARCHAR(64) COLLATE DATABASE_DEFAULT NULL,
        [City] NVARCHAR(64) COLLATE DATABASE_DEFAULT NULL,
        [Customer] NVARCHAR(128) COLLATE DATABASE_DEFAULT NULL,
        [NewCustomer] BIT NULL,
        [Status] NVARCHAR(32) COLLATE DATABASE_DEFAULT NULL,
        [Paid] BIT NULL,
        [DeliveredLocal] DATETIME2 NULL,
        [DeliveredAt] DATETIME2 NULL);

    -- Час замовлення - за денним профілем (київський час): мало вночі, пік увечері.
    INSERT INTO #Orders ([DayIdx], [K], [Sale], [LocalAt])
    SELECT d.[DayIdx], n.[N], d.[Sale],
           DATEADD(SECOND, CAST(hr.[Value] AS INT) * 3600 + x.[R2] % 3600, CAST(d.[D] AS DATETIME2))
    FROM #Days d
    INNER JOIN #Num n ON n.[N] <= d.[Cnt]
    CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT(N's:', d.[DayIdx], N':', n.[N])) AS [H]) h
    CROSS APPLY (SELECT CAST(SUBSTRING(h.[H], 1, 4) AS INT) & 2147483647 AS [R1], CAST(SUBSTRING(h.[H], 5, 4) AS INT) & 2147483647 AS [R2]) x
    INNER JOIN #Pick hr
        ON hr.[List] = 'hour' AND x.[R1] % hr.[Total] >= hr.[CumFrom] AND x.[R1] % hr.[Total] < hr.[CumTo];

    UPDATE #Orders SET [CreatedAt] = CAST(([LocalAt] AT TIME ZONE @Tz) AT TIME ZONE 'UTC' AS DATETIME2);
    DELETE FROM #Orders WHERE [CreatedAt] > @Now;     -- сьогоднішні - лише до поточного моменту

    -- Номери в хронологічному порядку і випадкові числа для кожного замовлення.
    ;WITH r AS (SELECT [N], ROW_NUMBER() OVER (ORDER BY [CreatedAt], [DayIdx], [K]) AS [Rn] FROM #Orders)
    UPDATE r SET [N] = [Rn];

    UPDATE o
    SET [R1] = x.[R1], [R2] = x.[R2], [R3] = x.[R3], [R4] = x.[R4], [R5] = x.[R5], [R6] = x.[R6], [R7] = x.[R7],
        [R8] = x.[R8], [R9] = y.[R9], [R10] = y.[R10], [R11] = y.[R11], [R12] = y.[R12], [R13] = y.[R13], [R14] = y.[R14],
        [Number] = CONCAT(N'ORD-', RIGHT(CONCAT(N'000000', o.[N]), 6))
    FROM #Orders o
    CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT(N'o1:', o.[N])) AS [H1],
                        HASHBYTES('SHA2_256', CONCAT(N'o2:', o.[N])) AS [H2]) h
    CROSS APPLY (SELECT CAST(SUBSTRING(h.[H1], 1, 4) AS INT) & 2147483647 AS [R1], CAST(SUBSTRING(h.[H1], 5, 4) AS INT) & 2147483647 AS [R2], CAST(SUBSTRING(h.[H1], 9, 4) AS INT) & 2147483647 AS [R3], CAST(SUBSTRING(h.[H1], 13, 4) AS INT) & 2147483647 AS [R4], CAST(SUBSTRING(h.[H1], 17, 4) AS INT) & 2147483647 AS [R5], CAST(SUBSTRING(h.[H1], 21, 4) AS INT) & 2147483647 AS [R6], CAST(SUBSTRING(h.[H1], 25, 4) AS INT) & 2147483647 AS [R7], CAST(SUBSTRING(h.[H1], 29, 4) AS INT) & 2147483647 AS [R8]) x
    CROSS APPLY (SELECT CAST(SUBSTRING(h.[H2], 1, 4) AS INT) & 2147483647 AS [R9], CAST(SUBSTRING(h.[H2], 5, 4) AS INT) & 2147483647 AS [R10], CAST(SUBSTRING(h.[H2], 9, 4) AS INT) & 2147483647 AS [R11], CAST(SUBSTRING(h.[H2], 13, 4) AS INT) & 2147483647 AS [R12], CAST(SUBSTRING(h.[H2], 17, 4) AS INT) & 2147483647 AS [R13], CAST(SUBSTRING(h.[H2], 21, 4) AS INT) & 2147483647 AS [R14]) y;

    -- Категорія, а в ній товар за популярністю.
    UPDATE o SET [Category] = c.[Value]
    FROM #Orders o
    INNER JOIN #Pick c ON c.[List] = 'cat' AND o.[R1] % c.[Total] >= c.[CumFrom] AND o.[R1] % c.[Total] < c.[CumTo];

    UPDATE o SET [P] = p.[P], [Product] = p.[Name], [UnitPrice] = p.[Price]
    FROM #Orders o
    INNER JOIN #Prod p
        ON p.[Category] = o.[Category] AND o.[R2] % p.[CatTotal] >= p.[CumFrom] AND o.[R2] % p.[CatTotal] < p.[CumTo];

    -- Кількість (аксесуари частіше беруть по кілька), знижка (на розпродажах - більша).
    UPDATE o SET [Items] = CAST(i.[Value] AS INT)
    FROM #Orders o
    INNER JOIN #Pick i
        ON i.[List] = IIF(o.[Category] = N'Аксесуари', 'items_acc', 'items')
       AND o.[R3] % i.[Total] >= i.[CumFrom] AND o.[R3] % i.[Total] < i.[CumTo];

    UPDATE o SET [Discount] = CAST(i.[Value] AS INT)
    FROM #Orders o
    INNER JOIN #Pick i
        ON i.[List] = IIF(o.[Sale] = 1, 'disc_sale', 'disc')
       AND o.[R4] % i.[Total] >= i.[CumFrom] AND o.[R4] % i.[Total] < i.[CumTo];

    UPDATE #Orders SET [Amount] = CAST(ROUND([UnitPrice] * [Items] * (100 - [Discount]) / 100.0, 2) AS DECIMAL(19, 2));

    UPDATE o SET [Channel] = c.[Value], [Payment] = p.[Value], [Delivery] = dl.[Value], [City] = ct.[Value],
                 [Customer] = CONCAT(fn.[Value], N' ', ln.[Value]),
                 [NewCustomer] = IIF(o.[R11] % 100 < 34, 1, 0)
    FROM #Orders o
    INNER JOIN #Pick c  ON c.[List]  = 'channel'  AND o.[R5]  % c.[Total]  >= c.[CumFrom]  AND o.[R5]  % c.[Total]  < c.[CumTo]
    INNER JOIN #Pick p  ON p.[List]  = 'payment'  AND o.[R6]  % p.[Total]  >= p.[CumFrom]  AND o.[R6]  % p.[Total]  < p.[CumTo]
    INNER JOIN #Pick dl ON dl.[List] = 'delivery' AND o.[R7]  % dl.[Total] >= dl.[CumFrom] AND o.[R7]  % dl.[Total] < dl.[CumTo]
    INNER JOIN #Pick ct ON ct.[List] = 'city'     AND o.[R8]  % ct.[Total] >= ct.[CumFrom] AND o.[R8]  % ct.[Total] < ct.[CumTo]
    INNER JOIN #Pick fn ON fn.[List] = 'first'    AND o.[R9]  % fn.[Total] >= fn.[CumFrom] AND o.[R9]  % fn.[Total] < fn.[CumTo]
    INNER JOIN #Pick ln ON ln.[List] = 'last'     AND o.[R10] % ln.[Total] >= ln.[CumFrom] AND o.[R10] % ln.[Total] < ln.[CumTo];

    -- Оплата частинами - лише для покупок від 3 000 грн.
    UPDATE #Orders SET [Payment] = N'Картка онлайн'
    WHERE [Payment] = N'Оплата частинами' AND [UnitPrice] * [Items] < 3000;

    -- Скасування: звичайно ≈ 4,5 % (накладений платіж - 8 %, відмови при отриманні).
    -- Аномалія: останні 20 годин у застосунку не проходить онлайн-оплата - скасовано 90 % таких замовлень.
    UPDATE #Orders SET [Status] = N'Скасоване'
    WHERE [R12] % 1000 < CASE
            WHEN [CreatedAt] >= DATEADD(HOUR, -20, @Now) AND [Channel] = N'Мобільний застосунок'
                 AND [Payment] IN (N'Картка онлайн', N'Apple Pay / Google Pay') THEN 900
            WHEN [Payment] = N'Накладений платіж' THEN 80
            ELSE 45 END;

    -- Доставка через 1-3 дні (Укрпоштою - на день довше) з 9:00 до 20:00; що ще не настало - «в дорозі».
    UPDATE #Orders
    SET [DeliveredLocal] = DATEADD(MINUTE, 9 * 60 + [R13] % 660,
                               CAST(DATEADD(DAY, 1 + [R14] % 3 + IIF([Delivery] = N'Укрпошта', 1, 0), CAST([LocalAt] AS DATE)) AS DATETIME2))
    WHERE [Status] IS NULL;

    UPDATE #Orders
    SET [Status] = CASE
            WHEN [DeliveredLocal] <= @NowLocal THEN N'Доставлене'
            WHEN DATEDIFF(MINUTE, [CreatedAt], @Now) < 60 THEN N'Нове'
            WHEN DATEDIFF(MINUTE, [CreatedAt], @Now) < 240 THEN IIF([R13] % 100 < 40, N'Нове', N'Підтверджене')
            WHEN DATEDIFF(MINUTE, [CreatedAt], @Now) < 1200 THEN IIF([R13] % 100 < 35, N'Відправлене', N'Підтверджене')
            ELSE N'Відправлене' END
    WHERE [Status] IS NULL;

    UPDATE #Orders SET [DeliveredLocal] = NULL WHERE [Status] <> N'Доставлене';
    UPDATE #Orders
    SET [DeliveredAt] = CAST(([DeliveredLocal] AT TIME ZONE @Tz) AT TIME ZONE 'UTC' AS DATETIME2)
    WHERE [DeliveredLocal] IS NOT NULL;

    -- Оплачено: онлайн - одразу (скасовані - кошти повернуто), накладений платіж - при отриманні.
    UPDATE #Orders
    SET [Paid] = CASE WHEN [Status] = N'Скасоване' THEN 0
                      WHEN [Payment] = N'Накладений платіж' THEN IIF([Status] = N'Доставлене', 1, 0)
                      ELSE 1 END,
        [UpdatedAt] = [DeliveredAt];

    -------------------------------------------------------------------------------
    -- 5. Повернення (з доставлених замовлень, через 1-13 днів після доставки)
    -------------------------------------------------------------------------------
    CREATE TABLE #Returns (
        [N] INT NULL,
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [Number] NVARCHAR(16) COLLATE DATABASE_DEFAULT NULL,
        [OrderNumber] NVARCHAR(16) COLLATE DATABASE_DEFAULT NOT NULL,
        [Product] NVARCHAR(200) COLLATE DATABASE_DEFAULT NOT NULL,
        [Category] NVARCHAR(64) COLLATE DATABASE_DEFAULT NOT NULL,
        [Amount] DECIMAL(19, 2) NOT NULL,
        [Reason] NVARCHAR(64) COLLATE DATABASE_DEFAULT NULL,
        [Status] NVARCHAR(32) COLLATE DATABASE_DEFAULT NULL,
        [LocalAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NULL,
        [R1] INT NOT NULL, [R2] INT NOT NULL, [R5] INT NOT NULL);

    INSERT INTO #Returns ([OrderNumber], [Product], [Category], [Amount], [LocalAt], [R1], [R2], [R5])
    SELECT o.[Number], o.[Product], o.[Category],
           -- з кількох одиниць іноді повертають одну
           CAST(IIF(o.[Items] > 1 AND x.[R1] % 100 < 45, o.[Amount] / o.[Items], o.[Amount]) AS DECIMAL(19, 2)),
           DATEADD(MINUTE, 9 * 60 + x.[R2] % 720, CAST(DATEADD(DAY, 1 + x.[R3] % 13, CAST(o.[DeliveredLocal] AS DATE)) AS DATETIME2)),
           x.[R1], x.[R2], x.[R5]
    FROM #Orders o
    INNER JOIN #Cat c ON c.[Name] = o.[Category]
    CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT(N'r:', o.[N])) AS [H]) h
    CROSS APPLY (SELECT CAST(SUBSTRING(h.[H], 1, 4) AS INT) & 2147483647 AS [R1], CAST(SUBSTRING(h.[H], 5, 4) AS INT) & 2147483647 AS [R2], CAST(SUBSTRING(h.[H], 9, 4) AS INT) & 2147483647 AS [R3], CAST(SUBSTRING(h.[H], 13, 4) AS INT) & 2147483647 AS [R4], CAST(SUBSTRING(h.[H], 17, 4) AS INT) & 2147483647 AS [R5]) x
    WHERE o.[Status] = N'Доставлене' AND x.[R4] % 1000 < c.[RetPermille];

    UPDATE #Returns SET [CreatedAt] = CAST(([LocalAt] AT TIME ZONE @Tz) AT TIME ZONE 'UTC' AS DATETIME2);
    DELETE FROM #Returns WHERE [CreatedAt] > @Now;

    ;WITH r AS (SELECT [N], ROW_NUMBER() OVER (ORDER BY [CreatedAt], [OrderNumber]) AS [Rn] FROM #Returns)
    UPDATE r SET [N] = [Rn];

    UPDATE r
    SET [Number] = CONCAT(N'RET-', RIGHT(CONCAT(N'00000', r.[N]), 5)),
        [Reason] = p.[Value],
        [Status] = CASE
            WHEN DATEDIFF(HOUR, r.[CreatedAt], @Now) < 24 THEN N'Нове'
            WHEN DATEDIFF(HOUR, r.[CreatedAt], @Now) < 72 THEN N'На розгляді'
            WHEN r.[R2] % 100 < 12 THEN N'Відхилено'
            WHEN DATEDIFF(HOUR, r.[CreatedAt], @Now) < 144 THEN N'Погоджено'
            ELSE N'Кошти повернуто' END
    FROM #Returns r
    INNER JOIN #Pick p ON p.[List] = 'reason' AND r.[R5] % p.[Total] >= p.[CumFrom] AND r.[R5] % p.[Total] < p.[CumTo];

    -------------------------------------------------------------------------------
    -- 6. Відгуки (≈ 14 % доставлених замовлень, через 1-9 днів після доставки)
    -------------------------------------------------------------------------------
    CREATE TABLE #Reviews (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [P] INT NOT NULL,
        [Product] NVARCHAR(200) COLLATE DATABASE_DEFAULT NOT NULL,
        [Category] NVARCHAR(64) COLLATE DATABASE_DEFAULT NOT NULL,
        [Weak] BIT NOT NULL,
        [Rating] INT NULL,
        [Recommend] BIT NULL,
        [Comment] NVARCHAR(400) COLLATE DATABASE_DEFAULT NULL,
        [LocalAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NULL,
        [R1] INT NOT NULL, [R2] INT NOT NULL, [R5] INT NOT NULL);

    INSERT INTO #Reviews ([P], [Product], [Category], [Weak], [LocalAt], [R1], [R2], [R5])
    SELECT o.[P], o.[Product], o.[Category], p.[Weak],
           DATEADD(MINUTE, 8 * 60 + x.[R2] % 900, CAST(DATEADD(DAY, 1 + x.[R3] % 9, CAST(o.[DeliveredLocal] AS DATE)) AS DATETIME2)),
           x.[R1], x.[R2], x.[R5]
    FROM #Orders o
    INNER JOIN #Prod p ON p.[P] = o.[P]
    CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT(N'v:', o.[N])) AS [H]) h
    CROSS APPLY (SELECT CAST(SUBSTRING(h.[H], 1, 4) AS INT) & 2147483647 AS [R1], CAST(SUBSTRING(h.[H], 5, 4) AS INT) & 2147483647 AS [R2], CAST(SUBSTRING(h.[H], 9, 4) AS INT) & 2147483647 AS [R3], CAST(SUBSTRING(h.[H], 13, 4) AS INT) & 2147483647 AS [R4], CAST(SUBSTRING(h.[H], 17, 4) AS INT) & 2147483647 AS [R5]) x
    WHERE o.[Status] = N'Доставлене' AND x.[R4] % 1000 < 140;

    UPDATE #Reviews SET [CreatedAt] = CAST(([LocalAt] AT TIME ZONE @Tz) AT TIME ZONE 'UTC' AS DATETIME2);
    DELETE FROM #Reviews WHERE [CreatedAt] > @Now;

    UPDATE r SET [Rating] = CAST(p.[Value] AS INT)
    FROM #Reviews r
    INNER JOIN #Pick p
        ON p.[List] = IIF(r.[Weak] = 1, 'rating_weak', 'rating')
       AND r.[R5] % p.[Total] >= p.[CumFrom] AND r.[R5] % p.[Total] < p.[CumTo];

    UPDATE #Reviews
    SET [Recommend] = CASE WHEN [Rating] >= 4 THEN IIF([R1] % 100 < 96, 1, 0)
                           WHEN [Rating] = 3 THEN IIF([R1] % 100 < 45, 1, 0)
                           ELSE IIF([R1] % 100 < 4, 1, 0) END;

    -- Коментар залишає приблизно дві третини покупців.
    UPDATE r SET [Comment] = p.[Value]
    FROM #Reviews r
    INNER JOIN #Pick p
        ON p.[List] = CASE WHEN r.[Rating] = 5 THEN 'c5' WHEN r.[Rating] = 4 THEN 'c4' WHEN r.[Rating] = 3 THEN 'c3' ELSE 'c12' END
       AND (r.[R2] / 100) % p.[Total] >= p.[CumFrom] AND (r.[R2] / 100) % p.[Total] < p.[CumTo]
    WHERE r.[R2] % 100 < 65;

    -- Рейтинг товару - середня оцінка у відгуках.
    UPDATE p SET [Rating] = r.[Avg]
    FROM #Prod p
    INNER JOIN (SELECT [P], CAST(ROUND(AVG(CAST([Rating] AS DECIMAL(9, 2))), 1) AS DECIMAL(19, 1)) AS [Avg]
                FROM #Reviews GROUP BY [P]) r ON r.[P] = p.[P];

    -------------------------------------------------------------------------------
    -- 7. Моделі і поля (тією ж процедурою, що й API)
    -------------------------------------------------------------------------------
    RAISERROR (N'3. Створення моделей і запис даних...', 0, 1) WITH NOWAIT;

    CREATE TABLE #Models (
        [ModelIdx] INT PRIMARY KEY,
        [Key] NVARCHAR(32) COLLATE DATABASE_DEFAULT NOT NULL,
        [Name] NVARCHAR(128) COLLATE DATABASE_DEFAULT NOT NULL,
        [ConfigId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID());

    INSERT INTO #Models ([ModelIdx], [Key], [Name])
    VALUES
        (1, N'orders', N'Замовлення'),
        (2, N'products', N'Товари'),
        (3, N'returns', N'Повернення'),
        (4, N'reviews', N'Відгуки');

    -- 0 Text, 1 Number, 2 Boolean, 3 Date, 4 DateTime, 5 Enum; Settings - правила в тому ж JSON, що пише API.
    CREATE TABLE #Fields (
        [ModelIdx] INT NOT NULL,
        [Ord] INT NOT NULL,
        [Key] NVARCHAR(32) COLLATE DATABASE_DEFAULT NOT NULL,
        [Name] NVARCHAR(128) COLLATE DATABASE_DEFAULT NOT NULL,
        [DataType] INT NOT NULL,
        [IsRequired] BIT NOT NULL,
        [DefaultValue] NVARCHAR(4000) COLLATE DATABASE_DEFAULT NULL,
        [Settings] NVARCHAR(MAX) COLLATE DATABASE_DEFAULT NULL,
        PRIMARY KEY ([ModelIdx], [Ord]));

    INSERT INTO #Fields ([ModelIdx], [Ord], [Key], [Name], [DataType], [IsRequired], [DefaultValue], [Settings])
    VALUES
        (1, 1, N'number', N'Номер замовлення', 0, 1, NULL, N'{"minLength":5,"maxLength":20}'),
        (1, 2, N'customer', N'Клієнт', 0, 1, NULL, N'{"maxLength":128}'),
        (1, 3, N'newCustomer', N'Новий клієнт', 2, 0, NULL, NULL),
        (1, 4, N'city', N'Місто', 0, 0, NULL, N'{"maxLength":64}'),
        (1, 5, N'channel', N'Канал продажу', 5, 1, N'Сайт', N'{"options":["Сайт","Мобільний застосунок","Instagram","Телефон"]}'),
        (1, 6, N'category', N'Категорія', 5, 1, NULL, N'{"options":["Смартфони","Ноутбуки","Аудіо","Аксесуари","Побутова техніка","Ігри та консолі","Розумний дім"]}'),
        (1, 7, N'product', N'Товар', 0, 1, NULL, N'{"maxLength":200}'),
        (1, 8, N'items', N'Кількість товарів', 1, 1, N'1', N'{"min":"1","max":"50"}'),
        (1, 9, N'discount', N'Знижка, %', 1, 0, N'0', N'{"min":"0","max":"90"}'),
        (1, 10, N'amount', N'Сума, грн', 1, 1, NULL, N'{"min":"0","max":"1000000"}'),
        (1, 11, N'payment', N'Спосіб оплати', 5, 1, NULL, N'{"options":["Картка онлайн","Apple Pay / Google Pay","Накладений платіж","Оплата частинами"]}'),
        (1, 12, N'paid', N'Оплачено', 2, 0, NULL, NULL),
        (1, 13, N'status', N'Статус', 5, 1, N'Нове', N'{"options":["Нове","Підтверджене","Відправлене","Доставлене","Скасоване"]}'),
        (1, 14, N'delivery', N'Доставка', 5, 1, NULL, N'{"options":["Нова пошта","Самовивіз","Курʼєр","Укрпошта"]}'),
        (1, 15, N'deliveredAt', N'Доставлено', 4, 0, NULL, NULL),
        (2, 1, N'sku', N'Артикул', 0, 1, NULL, N'{"minLength":5,"maxLength":16}'),
        (2, 2, N'name', N'Назва', 0, 1, NULL, N'{"maxLength":200}'),
        (2, 3, N'category', N'Категорія', 5, 1, NULL, N'{"options":["Смартфони","Ноутбуки","Аудіо","Аксесуари","Побутова техніка","Ігри та консолі","Розумний дім"]}'),
        (2, 4, N'brand', N'Бренд', 0, 0, NULL, N'{"maxLength":64}'),
        (2, 5, N'price', N'Ціна, грн', 1, 1, NULL, N'{"min":"0","max":"1000000"}'),
        (2, 6, N'stock', N'Залишок, шт.', 1, 1, N'0', N'{"min":"0","max":"100000"}'),
        (2, 7, N'active', N'У продажу', 2, 0, NULL, NULL),
        (2, 8, N'rating', N'Рейтинг', 1, 0, NULL, N'{"min":"1","max":"5"}'),
        (2, 9, N'arrivedOn', N'Останнє надходження', 3, 0, NULL, NULL),
        (3, 1, N'number', N'Номер повернення', 0, 1, NULL, N'{"minLength":5,"maxLength":20}'),
        (3, 2, N'orderNumber', N'Номер замовлення', 0, 1, NULL, N'{"minLength":5,"maxLength":20}'),
        (3, 3, N'product', N'Товар', 0, 1, NULL, N'{"maxLength":200}'),
        (3, 4, N'category', N'Категорія', 5, 1, NULL, N'{"options":["Смартфони","Ноутбуки","Аудіо","Аксесуари","Побутова техніка","Ігри та консолі","Розумний дім"]}'),
        (3, 5, N'amount', N'Сума повернення, грн', 1, 1, NULL, N'{"min":"0","max":"1000000"}'),
        (3, 6, N'reason', N'Причина', 5, 1, NULL, N'{"options":["Не підійшов","Брак або несправність","Не відповідає опису","Пошкоджено під час доставки","Передумав"]}'),
        (3, 7, N'status', N'Статус', 5, 1, N'Нове', N'{"options":["Нове","На розгляді","Погоджено","Відхилено","Кошти повернуто"]}'),
        (4, 1, N'product', N'Товар', 0, 1, NULL, N'{"maxLength":200}'),
        (4, 2, N'category', N'Категорія', 5, 1, NULL, N'{"options":["Смартфони","Ноутбуки","Аудіо","Аксесуари","Побутова техніка","Ігри та консолі","Розумний дім"]}'),
        (4, 3, N'rating', N'Оцінка', 1, 1, NULL, N'{"min":"1","max":"5"}'),
        (4, 4, N'recommends', N'Рекомендує', 2, 0, NULL, NULL),
        (4, 5, N'comment', N'Коментар', 0, 0, NULL, N'{"maxLength":1000}');

    -- Моделі налаштовано раніше, ніж заведено каталог товарів і надійшло перше замовлення.
    DECLARE @ModelsCreatedAt DATETIME2 = DATEADD(DAY, -(@Days + 70), @Now);
    DECLARE @Properties [dbo].[EntityPropertyList];
    DECLARE @MI INT = 1, @MC INT = (SELECT COUNT(*) FROM #Models);
    DECLARE @CfgId UNIQUEIDENTIFIER, @CfgKey NVARCHAR(32), @CfgName NVARCHAR(128), @CfgAt DATETIME2;

    WHILE @MI <= @MC
    BEGIN
        SELECT @CfgId = [ConfigId], @CfgKey = [Key], @CfgName = [Name] FROM #Models WHERE [ModelIdx] = @MI;
        SET @CfgAt = DATEADD(MINUTE, @MI, @ModelsCreatedAt);

        DELETE FROM @Properties;
        INSERT INTO @Properties ([Id], [EntityConfigId], [Key], [Name], [DataType], [CreatedAt], [IsRequired], [DefaultValue], [Settings])
        SELECT NEWID(), @CfgId, [Key], [Name], [DataType], DATEADD(MILLISECOND, [Ord], @CfgAt), [IsRequired], [DefaultValue], [Settings]
        FROM #Fields
        WHERE [ModelIdx] = @MI;

        EXEC [dbo].[sp_EntityConfigs_Create]
             @Id = @CfgId, @Key = @CfgKey, @Name = @CfgName, @CreatedAt = @CfgAt, @Properties = @Properties;

        SET @MI += 1;
    END

    DECLARE @Cfg_orders UNIQUEIDENTIFIER = (SELECT [ConfigId] FROM #Models WHERE [Key] = N'orders');
    DECLARE @Cfg_products UNIQUEIDENTIFIER = (SELECT [ConfigId] FROM #Models WHERE [Key] = N'products');
    DECLARE @Cfg_returns UNIQUEIDENTIFIER = (SELECT [ConfigId] FROM #Models WHERE [Key] = N'returns');
    DECLARE @Cfg_reviews UNIQUEIDENTIFIER = (SELECT [ConfigId] FROM #Models WHERE [Key] = N'reviews');

    -------------------------------------------------------------------------------
    -- 8. Записи і значення (як пише API: кожне значення - у колонку свого типу, порожні не зберігаються)
    -------------------------------------------------------------------------------
    CREATE TABLE #Inst (
        [ModelKey] NVARCHAR(32) COLLATE DATABASE_DEFAULT NOT NULL,
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL);

    INSERT INTO #Inst SELECT N'orders', [Id], [CreatedAt], [UpdatedAt] FROM #Orders;
    INSERT INTO #Inst SELECT N'products', [Id], [CreatedAt], [UpdatedAt] FROM #Prod;
    INSERT INTO #Inst SELECT N'returns', [Id], [CreatedAt], NULL FROM #Returns;
    INSERT INTO #Inst SELECT N'reviews', [Id], [CreatedAt], NULL FROM #Reviews;

    CREATE TABLE #V (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [PropKey] NVARCHAR(32) COLLATE DATABASE_DEFAULT NOT NULL,
        [T] NVARCHAR(4000) COLLATE DATABASE_DEFAULT NULL,
        [Num] DECIMAL(38, 10) NULL,
        [B] BIT NULL,
        [D] DATETIME2 NULL);

    INSERT INTO #V ([Id], [PropKey], [T], [Num], [B], [D])
    SELECT o.[Id], v.[PropKey], v.[T], v.[Num], v.[B], v.[D]
    FROM #Orders o
    CROSS APPLY (VALUES
            (N'number', CAST(o.[Number] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'customer', CAST(o.[Customer] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'newCustomer', CAST(NULL AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(o.[NewCustomer] AS BIT), CAST(NULL AS DATETIME2)),
            (N'city', CAST(o.[City] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'channel', CAST(o.[Channel] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'category', CAST(o.[Category] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'product', CAST(o.[Product] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'items', CAST(NULL AS NVARCHAR(4000)), CAST(o.[Items] AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'discount', CAST(NULL AS NVARCHAR(4000)), CAST(o.[Discount] AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'amount', CAST(NULL AS NVARCHAR(4000)), CAST(o.[Amount] AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'payment', CAST(o.[Payment] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'paid', CAST(NULL AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(o.[Paid] AS BIT), CAST(NULL AS DATETIME2)),
            (N'status', CAST(o.[Status] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'delivery', CAST(o.[Delivery] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'deliveredAt', CAST(NULL AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(o.[DeliveredAt] AS DATETIME2))
        ) v ([PropKey], [T], [Num], [B], [D]);

    INSERT INTO #V ([Id], [PropKey], [T], [Num], [B], [D])
    SELECT o.[Id], v.[PropKey], v.[T], v.[Num], v.[B], v.[D]
    FROM #Prod o
    CROSS APPLY (VALUES
            (N'sku', CAST(o.[Sku] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'name', CAST(o.[Name] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'category', CAST(o.[Category] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'brand', CAST(o.[Brand] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'price', CAST(NULL AS NVARCHAR(4000)), CAST(o.[Price] AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'stock', CAST(NULL AS NVARCHAR(4000)), CAST(o.[Stock] AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'active', CAST(NULL AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(o.[Active] AS BIT), CAST(NULL AS DATETIME2)),
            (N'rating', CAST(NULL AS NVARCHAR(4000)), CAST(o.[Rating] AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'arrivedOn', CAST(NULL AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(o.[ArrivedOn] AS DATETIME2))
        ) v ([PropKey], [T], [Num], [B], [D]);

    INSERT INTO #V ([Id], [PropKey], [T], [Num], [B], [D])
    SELECT o.[Id], v.[PropKey], v.[T], v.[Num], v.[B], v.[D]
    FROM #Returns o
    CROSS APPLY (VALUES
            (N'number', CAST(o.[Number] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'orderNumber', CAST(o.[OrderNumber] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'product', CAST(o.[Product] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'category', CAST(o.[Category] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'amount', CAST(NULL AS NVARCHAR(4000)), CAST(o.[Amount] AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'reason', CAST(o.[Reason] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'status', CAST(o.[Status] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2))
        ) v ([PropKey], [T], [Num], [B], [D]);

    INSERT INTO #V ([Id], [PropKey], [T], [Num], [B], [D])
    SELECT o.[Id], v.[PropKey], v.[T], v.[Num], v.[B], v.[D]
    FROM #Reviews o
    CROSS APPLY (VALUES
            (N'product', CAST(o.[Product] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'category', CAST(o.[Category] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'rating', CAST(NULL AS NVARCHAR(4000)), CAST(o.[Rating] AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2)),
            (N'recommends', CAST(NULL AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(o.[Recommend] AS BIT), CAST(NULL AS DATETIME2)),
            (N'comment', CAST(o.[Comment] AS NVARCHAR(4000)), CAST(NULL AS DECIMAL(38, 10)), CAST(NULL AS BIT), CAST(NULL AS DATETIME2))
        ) v ([PropKey], [T], [Num], [B], [D]);

    BEGIN TRANSACTION;

    -- У хронологічному порядку, як при надходженні через API (внутрішні ключі зростають з датою створення).
    INSERT INTO [dbo].[EntityInstances] ([Id], [ConfigPkey], [CreatedAt], [UpdatedAt])
    SELECT i.[Id], c.[Pkey], i.[CreatedAt], i.[UpdatedAt]
    FROM #Inst i
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Key] = i.[ModelKey]
    ORDER BY i.[CreatedAt];

    INSERT INTO [dbo].[EntityPropertyInstances] ([InstancePkey], [PropertyPkey], [ValueText], [ValueNumber], [ValueBool], [ValueDate])
    SELECT i.[Pkey], pc.[Pkey], v.[T], v.[Num], v.[B], v.[D]
    FROM #V v
    INNER JOIN [dbo].[EntityInstances] i ON i.[Id] = v.[Id]
    INNER JOIN [dbo].[EntityPropertyConfigs] pc ON pc.[ConfigPkey] = i.[ConfigPkey] AND pc.[Key] = v.[PropKey]
    WHERE v.[T] IS NOT NULL OR v.[Num] IS NOT NULL OR v.[B] IS NOT NULL OR v.[D] IS NOT NULL
    ORDER BY i.[Pkey], pc.[Pkey];

    COMMIT TRANSACTION;

    -------------------------------------------------------------------------------
    -- 9. Дашборди
    -------------------------------------------------------------------------------
    RAISERROR (N'4. Дашборди, правила моніторингу, користувачі...', 0, 1) WITH NOWAIT;

    -- Налаштування віджетів - JSON у форматі інтерфейсу (WidgetSettings у DataCollector.WebUI).
    DECLARE @Widgets [dbo].[DashboardWidgetList];

    INSERT INTO @Widgets ([Id], [ConfigId], [Type], [Title], [X], [Y], [W], [H], [Settings])
    VALUES
        (NEWID(), @Cfg_orders, N'kpi', N'Замовлень', 0, 0, 3, 2,
         N'{"version":1,"kpi":{"function":"count","field":""}}'),
        (NEWID(), @Cfg_orders, N'kpi', N'Виручка (оплачені), грн', 3, 0, 3, 2,
         N'{"version":1,"kpi":{"function":"sum","field":"amount"},"filters":[{"field":"paid","operator":"eq","value":"true","values":[]}]}'),
        (NEWID(), @Cfg_orders, N'kpi', N'Середній чек, грн', 6, 0, 3, 2,
         N'{"version":1,"kpi":{"function":"avg","field":"amount"},"filters":[{"field":"paid","operator":"eq","value":"true","values":[]}]}'),
        (NEWID(), @Cfg_orders, N'kpi', N'Замовлень від нових клієнтів', 9, 0, 3, 2,
         N'{"version":1,"kpi":{"function":"count","field":""},"filters":[{"field":"newCustomer","operator":"eq","value":"true","values":[]}]}'),
        (NEWID(), @Cfg_orders, N'chart', N'Виручка по місяцях', 0, 2, 8, 5,
         N'{"version":1,"chart":{"type":"bar","stack":false,"sortByValue":false,"xField":"createdAt","interval":"month","lines":[{"function":"sum","field":"amount"}],"smooth":false,"area":false,"fillGaps":true},"filters":[{"field":"paid","operator":"eq","value":"true","values":[]}]}'),
        (NEWID(), NULL, N'monitoring', N'Стан правил моніторингу', 8, 2, 4, 5,
         N'{"version":1}'),
        (NEWID(), @Cfg_orders, N'chart', N'Замовлення по тижнях за каналами продажу', 0, 7, 8, 5,
         N'{"version":1,"chart":{"type":"line","stack":false,"sortByValue":false,"xField":"createdAt","interval":"week","lines":[{"function":"count","field":""}],"smooth":true,"area":false,"fillGaps":true,"splitBy":"channel"}}'),
        (NEWID(), @Cfg_orders, N'chart', N'Спосіб оплати', 8, 7, 4, 5,
         N'{"version":1,"chart":{"type":"pie","stack":false,"sortByValue":false,"xField":"payment","interval":"day","lines":[{"function":"count","field":""}],"smooth":false,"area":false,"fillGaps":true}}'),
        (NEWID(), @Cfg_orders, N'chart', N'Виручка за категоріями, грн', 0, 12, 6, 5,
         N'{"version":1,"chart":{"type":"barHorizontal","stack":false,"sortByValue":true,"xField":"category","interval":"day","lines":[{"function":"sum","field":"amount"}],"smooth":false,"area":false,"fillGaps":true},"filters":[{"field":"paid","operator":"eq","value":"true","values":[]}]}'),
        (NEWID(), @Cfg_orders, N'chart', N'Сезонність категорій (замовлення)', 6, 12, 6, 5,
         N'{"version":1,"chart":{"type":"heatmap","stack":false,"sortByValue":false,"xField":"createdAt","interval":"month","lines":[{"function":"count","field":""}],"smooth":false,"area":false,"fillGaps":true,"splitBy":"category"}}'),
        (NEWID(), @Cfg_orders, N'summary', N'Продажі за товарами', 0, 17, 6, 7,
         N'{"version":1,"summary":{"dimensions":[{"field":"product","interval":"day"}],"metrics":[{"function":"count","field":""},{"function":"sum","field":"amount"}],"order":"valueDesc"},"filters":[{"field":"paid","operator":"eq","value":"true","values":[]}]}'),
        (NEWID(), @Cfg_orders, N'chart', N'Замовлення за містами', 6, 17, 6, 7,
         N'{"version":1,"chart":{"type":"barHorizontal","stack":false,"sortByValue":true,"xField":"city","interval":"day","lines":[{"function":"count","field":""}],"smooth":false,"area":false,"fillGaps":true}}'),
        (NEWID(), @Cfg_orders, N'table', N'Останні замовлення', 0, 24, 12, 7,
         N'{"version":1,"columns":[{"key":"number","visible":true},{"key":"createdAt","visible":true},{"key":"customer","visible":true},{"key":"city","visible":true},{"key":"product","visible":true},{"key":"amount","visible":true},{"key":"payment","visible":true},{"key":"paid","visible":true},{"key":"status","visible":true},{"key":"newCustomer","visible":false},{"key":"channel","visible":false},{"key":"category","visible":false},{"key":"items","visible":false},{"key":"discount","visible":false},{"key":"delivery","visible":false},{"key":"deliveredAt","visible":false},{"key":"updatedAt","visible":false}],"sortKey":"createdAt","sortDescending":true,"pageSize":10}');

    EXEC [dbo].[sp_Dashboards_Create]
         @Id = '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8f90',
         @Name = N'Продажі інтернет-магазину',
         @Description = N'Виручка, замовлення, канали продажу, категорії й товари',
         @RefreshSeconds = 60,
         @CreatedAt = @Now,
         @Widgets = @Widgets;

    DELETE FROM @Widgets;
    DECLARE @Dashboard2At DATETIME2 = DATEADD(SECOND, 1, @Now);

    INSERT INTO @Widgets ([Id], [ConfigId], [Type], [Title], [X], [Y], [W], [H], [Settings])
    VALUES
        (NEWID(), @Cfg_products, N'kpi', N'Товарів у продажу', 0, 0, 3, 2,
         N'{"version":1,"kpi":{"function":"count","field":""},"filters":[{"field":"active","operator":"eq","value":"true","values":[]}]}'),
        (NEWID(), @Cfg_products, N'kpi', N'Закінчуються (менше 5 шт.)', 3, 0, 3, 2,
         N'{"version":1,"kpi":{"function":"count","field":""},"filters":[{"field":"active","operator":"eq","value":"true","values":[]},{"field":"stock","operator":"lt","value":"5","values":[]}]}'),
        (NEWID(), @Cfg_returns, N'kpi', N'Повернень', 6, 0, 3, 2,
         N'{"version":1,"kpi":{"function":"count","field":""}}'),
        (NEWID(), @Cfg_reviews, N'kpi', N'Середня оцінка', 9, 0, 3, 2,
         N'{"version":1,"kpi":{"function":"avg","field":"rating"}}'),
        (NEWID(), @Cfg_products, N'table', N'Товари, що закінчуються', 0, 2, 8, 5,
         N'{"version":1,"columns":[{"key":"sku","visible":true},{"key":"name","visible":true},{"key":"category","visible":true},{"key":"stock","visible":true},{"key":"price","visible":true},{"key":"arrivedOn","visible":true},{"key":"brand","visible":false},{"key":"active","visible":false},{"key":"rating","visible":false},{"key":"createdAt","visible":false},{"key":"updatedAt","visible":false}],"sortKey":"stock","sortDescending":false,"pageSize":10,"filters":[{"field":"active","operator":"eq","value":"true","values":[]},{"field":"stock","operator":"lt","value":"5","values":[]}]}'),
        (NEWID(), NULL, N'monitoring', N'Стан правил моніторингу', 8, 2, 4, 5,
         N'{"version":1}'),
        (NEWID(), @Cfg_returns, N'chart', N'Повернення по місяцях за причинами', 0, 7, 8, 5,
         N'{"version":1,"chart":{"type":"bar","stack":true,"sortByValue":false,"xField":"createdAt","interval":"month","lines":[{"function":"count","field":""}],"smooth":false,"area":false,"fillGaps":true,"splitBy":"reason"}}'),
        (NEWID(), @Cfg_returns, N'chart', N'Причини повернень', 8, 7, 4, 5,
         N'{"version":1,"chart":{"type":"pie","stack":false,"sortByValue":false,"xField":"reason","interval":"day","lines":[{"function":"count","field":""}],"smooth":false,"area":false,"fillGaps":true}}'),
        (NEWID(), @Cfg_reviews, N'chart', N'Середня оцінка по місяцях', 0, 12, 6, 5,
         N'{"version":1,"chart":{"type":"line","stack":false,"sortByValue":false,"xField":"createdAt","interval":"month","lines":[{"function":"avg","field":"rating"}],"smooth":true,"area":false,"fillGaps":true}}'),
        (NEWID(), @Cfg_returns, N'summary', N'Повернення за категоріями', 6, 12, 6, 5,
         N'{"version":1,"summary":{"dimensions":[{"field":"category","interval":"day"}],"metrics":[{"function":"count","field":""},{"function":"sum","field":"amount"}],"order":"valueDesc"}}'),
        (NEWID(), @Cfg_reviews, N'table', N'Останні відгуки', 0, 17, 12, 7,
         N'{"version":1,"columns":[{"key":"createdAt","visible":true},{"key":"product","visible":true},{"key":"rating","visible":true},{"key":"recommends","visible":true},{"key":"comment","visible":true},{"key":"category","visible":false},{"key":"updatedAt","visible":false}],"sortKey":"createdAt","sortDescending":true,"pageSize":10}');

    EXEC [dbo].[sp_Dashboards_Create]
         @Id = '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8f9a',
         @Name = N'Склад, повернення та відгуки',
         @Description = N'Залишки товарів, повернення за причинами, оцінки покупців',
         @RefreshSeconds = 300,
         @CreatedAt = @Dashboard2At,
         @Widgets = @Widgets;

    -------------------------------------------------------------------------------
    -- 10. Правила моніторингу
    -------------------------------------------------------------------------------
    -- Періоди й інтервали перевірки - лише ті, що пропонує редактор правила (інакше поле в ньому порожнє).
    -- Одразу після запуску: «Частка скасувань» і «Товари закінчуються» - тривога, решта - у нормі.
    DECLARE @RuleNow DATETIME2 = SYSUTCDATETIME();

    -- Порівняння з минулим періодом: виручка поточного місяця < 90 % виручки минулого місяця до того ж моменту.
    EXEC [dbo].[sp_MonitoringRules_Create]
         @Id = '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8f91', @ConfigId = @Cfg_orders,
         @Name = N'Виручка місяця нижча, ніж минулого місяця',
         @IsEnabled = 1, @Filters = N'[{"field":"paid","operator":"eq","value":true}]', @Search = NULL,
         @Function = N'sum', @FieldKey = N'amount', @TimeField = N'createdAt', @WindowMinutes = NULL,
         @WindowUnit = N'month', @TimeZone = N'Europe/Kiev',
         @Operator = N'lt', @Threshold = 0, @ConsecutiveChecks = 1, @CheckIntervalSeconds = 900, @CreatedAt = @RuleNow,
         @CompareConfigId = @Cfg_orders, @CompareFilters = N'[{"field":"paid","operator":"eq","value":true}]',
         @CompareFunction = N'sum', @CompareFieldKey = N'amount', @CompareTimeField = N'createdAt',
         @CompareWindowUnit = N'month', @CompareWindowOffset = 1, @CompareWindowToDate = 1, @CompareMultiplier = 0.9;

    -- Порівняння з іншим показником тієї ж моделі: скасовані за добу > 10 % усіх замовлень за добу.
    EXEC [dbo].[sp_MonitoringRules_Create]
         @Id = '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8f92', @ConfigId = @Cfg_orders,
         @Name = N'Частка скасованих замовлень за добу понад 10 %',
         @IsEnabled = 1, @Filters = N'[{"field":"status","operator":"in","values":["Скасоване"]}]', @Search = NULL,
         @Function = N'count', @FieldKey = NULL, @TimeField = N'createdAt', @WindowMinutes = 1440,
         @Operator = N'gt', @Threshold = 0, @ConsecutiveChecks = 1, @CheckIntervalSeconds = 300, @CreatedAt = @RuleNow,
         @CompareConfigId = @Cfg_orders, @CompareFilters = NULL,
         @CompareFunction = N'count', @CompareFieldKey = NULL, @CompareTimeField = N'createdAt',
         @CompareWindowMinutes = 1440, @CompareMultiplier = 0.1;

    -- Порівняння з показником іншої моделі: сума повернень за місяць > 5 % виручки за місяць.
    EXEC [dbo].[sp_MonitoringRules_Create]
         @Id = '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8f93', @ConfigId = @Cfg_returns,
         @Name = N'Повернення перевищують 5 % виручки місяця',
         @IsEnabled = 1, @Filters = NULL, @Search = NULL,
         @Function = N'sum', @FieldKey = N'amount', @TimeField = N'createdAt', @WindowMinutes = NULL,
         @WindowUnit = N'month', @TimeZone = N'Europe/Kiev',
         @Operator = N'gt', @Threshold = 0, @ConsecutiveChecks = 1, @CheckIntervalSeconds = 900, @CreatedAt = @RuleNow,
         @CompareConfigId = @Cfg_orders, @CompareFilters = N'[{"field":"paid","operator":"eq","value":true}]',
         @CompareFunction = N'sum', @CompareFieldKey = N'amount', @CompareTimeField = N'createdAt',
         @CompareWindowUnit = N'month', @CompareMultiplier = 0.05;

    -- Фіксований поріг за всіма записами: є товари в продажу із залишком менше 5 шт.
    EXEC [dbo].[sp_MonitoringRules_Create]
         @Id = '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8f94', @ConfigId = @Cfg_products,
         @Name = N'Товари закінчуються (залишок менше 5 шт.)',
         @IsEnabled = 1,
         @Filters = N'[{"field":"active","operator":"eq","value":true},{"field":"stock","operator":"lt","value":5}]', @Search = NULL,
         @Function = N'count', @FieldKey = NULL, @TimeField = N'createdAt', @WindowMinutes = NULL,
         @Operator = N'gt', @Threshold = 0, @ConsecutiveChecks = 1, @CheckIntervalSeconds = 300, @CreatedAt = @RuleNow;

    -- Ковзне вікно: середня оцінка відгуків за 7 днів нижча за 4.
    EXEC [dbo].[sp_MonitoringRules_Create]
         @Id = '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8f95', @ConfigId = @Cfg_reviews,
         @Name = N'Середня оцінка за тиждень нижча за 4',
         @IsEnabled = 1, @Filters = NULL, @Search = NULL,
         @Function = N'avg', @FieldKey = N'rating', @TimeField = N'createdAt', @WindowMinutes = 10080,
         @Operator = N'lt', @Threshold = 4, @ConsecutiveChecks = 1, @CheckIntervalSeconds = 900, @CreatedAt = @RuleNow;

    -- Вимкнене: щоб показати наживо - увімкнути (через 2 години після запуску скрипту стане тривогою),
    -- надіслати замовлення через API - правило повернеться в норму (після 2 перевірок поспіль).
    EXEC [dbo].[sp_MonitoringRules_Create]
         @Id = '6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8f96', @ConfigId = @Cfg_orders,
         @Name = N'Замовлення не надходять 2 години',
         @IsEnabled = 0, @Filters = NULL, @Search = NULL,
         @Function = N'count', @FieldKey = NULL, @TimeField = N'createdAt', @WindowMinutes = 120,
         @Operator = N'lt', @Threshold = 1, @ConsecutiveChecks = 2, @CheckIntervalSeconds = 60, @CreatedAt = @RuleNow;

    -------------------------------------------------------------------------------
    -- 11. Демонстраційні користувачі і API-ключ сайту магазину
    -------------------------------------------------------------------------------
    -- Створюються, лише якщо такого логіна ще немає (наявні паролі не перезаписуються).
    -- Паролі: admin / Admin12345, editor / Editor12345, viewer / Viewer12345.
    -- Хеші пораховано тим самим алгоритмом, що й у застосунку (PBKDF2-HMAC-SHA256, 600 000 ітерацій).
    INSERT INTO [dbo].[Users] ([Id], [Login], [DisplayName], [Role], [PasswordHash], [SecurityStamp], [IsActive], [CreatedAt])
    SELECT u.[Id], u.[Login], u.[DisplayName], u.[Role], u.[PasswordHash], NEWID(), 1, @Now
    FROM (VALUES
        ('6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8fa1', N'admin', N'Адміністратор', 2, 'PBKDF2$SHA256$600000$5CYZHFNu8gM5rVK4ZJgIdg==$q03/UyKiB8gwQmOWmATjC5952sJw93MPbK3L3zqrcN4='),
        ('6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8fa2', N'editor', N'Менеджер магазину', 1, 'PBKDF2$SHA256$600000$heJMUrsrP771wtN2+f4pGg==$b/9iMHb8xicQ/EYCZv82fuj9ujVUwSDGI6EAOHJKrls='),
        ('6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8fa3', N'viewer', N'Аналітик (перегляд)', 0, 'PBKDF2$SHA256$600000$8DQ0PdT6DBIWoZXCJtZwkQ==$B/F+DO8n3DEZfV5VcK9N/sdCRCXkIovW9kiQH3x7Y+g=')
    ) AS u ([Id], [Login], [DisplayName], [Role], [PasswordHash])
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Users] x WHERE x.[Login] = u.[Login] OR x.[Id] = u.[Id]);

    -- Ключ, з яким сайт магазину надсилає нові замовлення (роль «Запис даних»):
    -- X-Api-Key: dc_demo_shop_site_key_2026_000000000000000
    INSERT INTO [dbo].[ApiKeys] ([Id], [Name], [Prefix], [KeyHash], [Role], [CreatedAt], [CreatedByPkey])
    VALUES ('6d0a5a1e-2f4b-4c39-9a51-0d5b6c7e8fb2', N'Сайт магазину (демо)', 'dc_demo_sh',
            0x1CC8A841598B4DA5EF770BBC18380F5C79BBFEDB92225A9B4619FD8AD65BE025, 1, @Now,
            (SELECT [Pkey] FROM [dbo].[Users] WHERE [Login] = N'admin'));

    -------------------------------------------------------------------------------
    -- 12. Підсумок
    -------------------------------------------------------------------------------
    SELECT m.[Name] AS [Модель],
           (SELECT COUNT(*) FROM [dbo].[EntityPropertyConfigs] p WHERE p.[ConfigPkey] = c.[Pkey]) AS [Полів],
           (SELECT COUNT(*) FROM [dbo].[EntityInstances] i WHERE i.[ConfigPkey] = c.[Pkey]) AS [Записів]
    FROM #Models m
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Id] = m.[ConfigId]
    ORDER BY m.[ModelIdx];

    SELECT CAST(MIN([CreatedAt]) AS DATE) AS [Перше замовлення], CAST(MAX([CreatedAt]) AS DATE) AS [Останнє замовлення],
           SUM(IIF([Status] = N'Скасоване', 1, 0)) AS [Скасовано],
           SUM(IIF([CreatedAt] >= DATEADD(DAY, -1, @Now), 1, 0)) AS [За останню добу],
           SUM(IIF([CreatedAt] >= DATEADD(DAY, -1, @Now) AND [Status] = N'Скасоване', 1, 0)) AS [Скасовано за добу],
           CAST(SUM(IIF([Paid] = 1, [Amount], 0)) AS DECIMAL(19, 2)) AS [Виручка, грн]
    FROM #Orders;

    DROP TABLE #Num, #PickSrc, #Pick, #Cat, #Tpl, #ProdRaw, #Prod, #Days, #Orders, #Returns, #Reviews,
               #Models, #Fields, #Inst, #V;

    RAISERROR (N'Готово. Перевірте сторінки «Дашборди» і «Моніторинг» (перша перевірка правил - протягом хвилини).', 0, 1) WITH NOWAIT;
END
