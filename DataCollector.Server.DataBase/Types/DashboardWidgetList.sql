-- Повний набір віджетів дашборду для збереження: процедура приводить віджети в БД до цього списку
-- (нові додає, наявні оновлює, відсутні видаляє). [ConfigId] - GUID моделі (NULL - модель не вибрано / видалено).
CREATE TYPE [dbo].[DashboardWidgetList]
AS TABLE (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [ConfigId] UNIQUEIDENTIFIER NULL,
    [Type] NVARCHAR(16) NOT NULL,
    [Title] NVARCHAR(128) NULL,
    [X] INT NOT NULL,
    [Y] INT NOT NULL,
    [W] INT NOT NULL,
    [H] INT NOT NULL,
    [Settings] NVARCHAR(MAX) NOT NULL
);
