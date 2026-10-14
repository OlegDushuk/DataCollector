-- Записи для пакетного створення / оновлення (імпорт).
CREATE TYPE [dbo].[InstanceList]
AS TABLE (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL
);
