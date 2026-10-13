-- Правила моніторингу: показник за вікно часу на даних однієї моделі, порівняний з порогом.
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

    CONSTRAINT PK_MonitoringRules
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_MonitoringRules_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT FK_MonitoringRules_EntityConfigs
        FOREIGN KEY ([ConfigPkey])
            REFERENCES [dbo].[EntityConfigs] ([Pkey]),

    CONSTRAINT CK_MonitoringRules_Function
        CHECK ([Function] IN (N'count', N'sum', N'avg', N'min', N'max')),

    CONSTRAINT CK_MonitoringRules_Operator
        CHECK ([Operator] IN (N'gt', N'gte', N'lt', N'lte', N'eq', N'neq')),

    CONSTRAINT CK_MonitoringRules_Numbers
        CHECK ([ConsecutiveChecks] BETWEEN 1 AND 10
           AND [CheckIntervalSeconds] BETWEEN 30 AND 86400
           AND ([WindowMinutes] IS NULL OR [WindowMinutes] BETWEEN 1 AND 525600)),

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
