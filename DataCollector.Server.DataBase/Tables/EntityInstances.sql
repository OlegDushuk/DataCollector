-- Записи моделей.
-- [Pkey] - кластерний ключ: нові записи завжди додаються в кінець таблиці.
CREATE TABLE [dbo].[EntityInstances]
(
    [Pkey] BIGINT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ConfigPkey] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,

    CONSTRAINT PK_EntityInstances
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_EntityInstances_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT FK_EntityInstances_EntityConfigs
        FOREIGN KEY ([ConfigPkey])
            REFERENCES [dbo].[EntityConfigs] ([Pkey])
)
GO

-- Записи моделі в хронологічному порядку: сторінки, кількість, часові вибірки.
CREATE INDEX IX_EntityInstances_ConfigPkey_CreatedAt
    ON [dbo].[EntityInstances] ([ConfigPkey], [CreatedAt])
