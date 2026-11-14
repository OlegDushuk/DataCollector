-- Правила моніторингу: показник за період на даних однієї моделі, порівняний з порогом - фіксованим числом
-- або іншим показником, теж порахованим з даних (Compare*: та сама чи інша модель, свій період, коефіцієнт).
-- Визначення правила (що рахувати) і його поточний стан (що показала остання перевірка) - в одному рядку:
-- фоновий сервіс на кожній перевірці порівнює новий результат зі збереженим станом.
CREATE TABLE [dbo].[MonitoringRules]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ConfigPkey] INT NOT NULL,
    [Name] NVARCHAR(128) NOT NULL,
    [IsEnabled] BIT NOT NULL CONSTRAINT DF_MonitoringRules_IsEnabled DEFAULT (1),

    -- Що рахувати: умови (JSON - ті самі умови, що в POST .../records/query), пошук, показник.
    [Filters] NVARCHAR(MAX) NULL,
    [Search] NVARCHAR(200) NULL,
    [Function] NVARCHAR(8) NOT NULL,            -- count, sum, avg, min, max
    [FieldKey] NVARCHAR(32) NULL,               -- поле показника (для count - необов'язкове)

    -- Вікно часу: записи, у яких [TimeField] не раніше ніж [WindowMinutes] хвилин тому; NULL - усі записи.
    [TimeField] NVARCHAR(32) NOT NULL CONSTRAINT DF_MonitoringRules_TimeField DEFAULT (N'createdAt'),
    [WindowMinutes] INT NULL,

    -- Умова спрацювання: значення [Operator] [Threshold].
    [Operator] NVARCHAR(4) NOT NULL,            -- gt, gte, lt, lte, eq, neq
    [Threshold] DECIMAL(38, 10) NOT NULL,
    -- Скільки перевірок поспіль мають показати новий стан, щоб він змінився (захист від «тремтіння» біля порогу).
    [ConsecutiveChecks] INT NOT NULL CONSTRAINT DF_MonitoringRules_ConsecutiveChecks DEFAULT (1),
    [CheckIntervalSeconds] INT NOT NULL,

    -- Поточний стан: 0 - ще не перевірялось, 1 - норма, 2 - спрацювало, 3 - немає даних (або помилка розрахунку).
    [State] TINYINT NOT NULL CONSTRAINT DF_MonitoringRules_State DEFAULT (0),
    [StateSince] DATETIME2 NULL,
    [LastValue] DECIMAL(38, 10) NULL,
    [LastCheckedAt] DATETIME2 NULL,
    [LastError] NVARCHAR(1000) NULL,
    -- Новий стан, який уже показали [PendingCount] перевірок поспіль, але ще не підтверджено.
    [PendingState] TINYINT NULL,
    [PendingCount] INT NOT NULL CONSTRAINT DF_MonitoringRules_PendingCount DEFAULT (0),
    [NextCheckAt] DATETIME2 NOT NULL,

    -- Збільшується при кожній зміні визначення: результат перевірки за старим визначенням не записується.
    [Version] INT NOT NULL CONSTRAINT DF_MonitoringRules_Version DEFAULT (1),
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,

    -- Календарний період замість ковзного вікна: поточна година, день, тиждень, місяць, квартал, рік у часовому поясі
    -- [TimeZone]; [WindowOffset] - зсув на N періодів назад (1 - попередній), [WindowToDate] - зсунутий календарний
    -- період лише до того ж моменту, що й поточний. Колонки додано в кінець, щоб публікація не перебудовувала таблицю.
    [WindowUnit] NVARCHAR(8) NULL,              -- hour, day, week, month, quarter, year; NULL - ковзне вікно
    [WindowOffset] INT NOT NULL CONSTRAINT DF_MonitoringRules_WindowOffset DEFAULT (0),
    [WindowToDate] BIT NOT NULL CONSTRAINT DF_MonitoringRules_WindowToDate DEFAULT (0),
    [TimeZone] NVARCHAR(64) NULL,

    -- Показник порівняння (NULL - фіксований поріг [Threshold]): поріг = показник × [CompareMultiplier].
    [CompareConfigPkey] INT NULL,
    [CompareFilters] NVARCHAR(MAX) NULL,
    [CompareSearch] NVARCHAR(200) NULL,
    [CompareFunction] NVARCHAR(8) NULL,
    [CompareFieldKey] NVARCHAR(32) NULL,
    [CompareTimeField] NVARCHAR(32) NULL,
    [CompareWindowMinutes] INT NULL,
    [CompareWindowUnit] NVARCHAR(8) NULL,
    [CompareWindowOffset] INT NOT NULL CONSTRAINT DF_MonitoringRules_CompareWindowOffset DEFAULT (0),
    [CompareWindowToDate] BIT NOT NULL CONSTRAINT DF_MonitoringRules_CompareWindowToDate DEFAULT (0),
    [CompareMultiplier] DECIMAL(38, 10) NULL,
    -- Значення показника порівняння на останній перевірці.
    [LastCompareValue] DECIMAL(38, 10) NULL,

    CONSTRAINT PK_MonitoringRules
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_MonitoringRules_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT FK_MonitoringRules_EntityConfigs
        FOREIGN KEY ([ConfigPkey])
            REFERENCES [dbo].[EntityConfigs] ([Pkey]),

    CONSTRAINT FK_MonitoringRules_CompareEntityConfigs
        FOREIGN KEY ([CompareConfigPkey])
            REFERENCES [dbo].[EntityConfigs] ([Pkey]),

    CONSTRAINT CK_MonitoringRules_Function
        CHECK ([Function] IN (N'count', N'sum', N'avg', N'min', N'max')),

    CONSTRAINT CK_MonitoringRules_Operator
        CHECK ([Operator] IN (N'gt', N'gte', N'lt', N'lte', N'eq', N'neq')),

    CONSTRAINT CK_MonitoringRules_Numbers
        CHECK ([ConsecutiveChecks] BETWEEN 1 AND 10
           AND [CheckIntervalSeconds] BETWEEN 30 AND 86400
           AND ([WindowMinutes] IS NULL OR [WindowMinutes] BETWEEN 1 AND 525600)),

    -- Вікно: або хвилини, або календарний період; зсув - до 100 періодів.
    CONSTRAINT CK_MonitoringRules_Window
        CHECK (([WindowMinutes] IS NULL OR [WindowUnit] IS NULL)
           AND ([WindowUnit] IS NULL OR [WindowUnit] IN (N'hour', N'day', N'week', N'month', N'quarter', N'year'))
           AND [WindowOffset] BETWEEN 0 AND 100),

    -- Показник порівняння задано повністю (модель, функція, поле часу, коефіцієнт) або не задано зовсім.
    CONSTRAINT CK_MonitoringRules_Compare
        CHECK (([CompareConfigPkey] IS NULL AND [CompareFunction] IS NULL AND [CompareMultiplier] IS NULL)
            OR ([CompareConfigPkey] IS NOT NULL
                AND [CompareFunction] IN (N'count', N'sum', N'avg', N'min', N'max')
                AND [CompareTimeField] IS NOT NULL
                AND [CompareMultiplier] > 0
                AND ([CompareWindowMinutes] IS NULL OR [CompareWindowUnit] IS NULL)
                AND ([CompareWindowMinutes] IS NULL OR [CompareWindowMinutes] BETWEEN 1 AND 525600)
                AND ([CompareWindowUnit] IS NULL OR [CompareWindowUnit] IN (N'hour', N'day', N'week', N'month', N'quarter', N'year'))
                AND [CompareWindowOffset] BETWEEN 0 AND 100
                AND ([CompareFilters] IS NULL OR ISJSON([CompareFilters]) = 1))),

    CONSTRAINT CK_MonitoringRules_State
        CHECK ([State] BETWEEN 0 AND 3),

    CONSTRAINT CK_MonitoringRules_Filters
        CHECK ([Filters] IS NULL OR ISJSON([Filters]) = 1)
)
GO

-- Пошук правил, яким настав час перевірки.
CREATE NONCLUSTERED INDEX IX_MonitoringRules_NextCheckAt
    ON [dbo].[MonitoringRules] ([NextCheckAt])
    WHERE [IsEnabled] = 1
GO

CREATE NONCLUSTERED INDEX IX_MonitoringRules_ConfigPkey
    ON [dbo].[MonitoringRules] ([ConfigPkey])
GO

-- Видалення моделі шукає правила, що порівнюють з її даними.
CREATE NONCLUSTERED INDEX IX_MonitoringRules_CompareConfigPkey
    ON [dbo].[MonitoringRules] ([CompareConfigPkey])
    WHERE [CompareConfigPkey] IS NOT NULL
