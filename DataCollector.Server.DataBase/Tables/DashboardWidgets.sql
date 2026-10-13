-- Віджети дашборду. Позиція в сітці на 12 колонок: [X], [Y] - клітинка лівого верхнього кута, [W] - ширина в колонках,
-- [H] - висота в рядах. Перша версія інтерфейсу розкладає віджети по порядку (Y, X), але координати збережені повністю,
-- щоб згодом перейти на вільне перетягування без зміни схеми.
-- [Settings] - налаштування віджета у JSON (фільтри, колонки, групування, вигляд діаграми); формат належить інтерфейсу
-- і має власне поле версії, тож база перевіряє лише, що це коректний JSON.
CREATE TABLE [dbo].[DashboardWidgets]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [DashboardPkey] INT NOT NULL,
    -- Модель, з якої віджет бере дані. NULL - модель видалено (віджет лишається і просить вибрати іншу).
    [ConfigPkey] INT NULL,
    [Type] NVARCHAR(16) NOT NULL,   -- table, summary, chart, kpi, monitoring
    [Title] NVARCHAR(128) NULL,
    [X] INT NOT NULL,
    [Y] INT NOT NULL,
    [W] INT NOT NULL,
    [H] INT NOT NULL,
    [Settings] NVARCHAR(MAX) NOT NULL,

    CONSTRAINT PK_DashboardWidgets
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_DashboardWidgets_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT FK_DashboardWidgets_Dashboards
        FOREIGN KEY ([DashboardPkey])
            REFERENCES [dbo].[Dashboards] ([Pkey])
            ON DELETE CASCADE,

    CONSTRAINT FK_DashboardWidgets_EntityConfigs
        FOREIGN KEY ([ConfigPkey])
            REFERENCES [dbo].[EntityConfigs] ([Pkey]),

    CONSTRAINT CK_DashboardWidgets_Type
        CHECK ([Type] IN (N'table', N'summary', N'chart', N'kpi', N'monitoring')),

    CONSTRAINT CK_DashboardWidgets_Grid
        CHECK ([X] >= 0 AND [Y] >= 0 AND [W] BETWEEN 1 AND 12 AND [X] + [W] <= 12 AND [H] BETWEEN 1 AND 12),

    CONSTRAINT CK_DashboardWidgets_Settings
        CHECK (ISJSON([Settings]) = 1)
)
GO

CREATE NONCLUSTERED INDEX IX_DashboardWidgets_DashboardPkey
    ON [dbo].[DashboardWidgets] ([DashboardPkey])
GO

-- Для обнулення посилань при видаленні моделі.
CREATE NONCLUSTERED INDEX IX_DashboardWidgets_ConfigPkey
    ON [dbo].[DashboardWidgets] ([ConfigPkey])
    WHERE [ConfigPkey] IS NOT NULL
