-- API-ключі зовнішніх систем і пристроїв (заголовок X-Api-Key).
-- Зберігається лише SHA-256 від ключа і його початок (Prefix) для впізнавання у списку;
-- сам ключ показується один раз - при створенні.
-- Role: 0 - лише читання, 1 - запис даних (адміністрування ключем недоступне).
CREATE TABLE [dbo].[ApiKeys]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(128) NOT NULL,
    [Prefix] VARCHAR(16) NOT NULL,
    [KeyHash] BINARY(32) NOT NULL,
    [Role] TINYINT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    -- Хто створив; користувача не видаляють (лише вимикають), тож посилання лишається дійсним.
    [CreatedByPkey] INT NULL,
    [LastUsedAt] DATETIME2 NULL,
    [RevokedAt] DATETIME2 NULL,

    CONSTRAINT PK_ApiKeys
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_ApiKeys_Id
        UNIQUE NONCLUSTERED ([Id]),

    -- Пошук ключа при кожному запиті - за хешем.
    CONSTRAINT UQ_ApiKeys_KeyHash
        UNIQUE NONCLUSTERED ([KeyHash]),

    CONSTRAINT FK_ApiKeys_Users
        FOREIGN KEY ([CreatedByPkey])
            REFERENCES [dbo].[Users] ([Pkey]),

    CONSTRAINT CK_ApiKeys_Role
        CHECK ([Role] BETWEEN 0 AND 1)
)
