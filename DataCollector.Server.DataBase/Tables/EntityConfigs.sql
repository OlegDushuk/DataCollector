-- Моделі даних.
-- Правило для всіх таблиць: [Pkey] - внутрішній цілий первинний ключ, усі зв'язки між таблицями йдуть через нього;
-- [Id] (GUID) - зовнішній ідентифікатор для API та домену, використовується лише для пошуку.
CREATE TABLE [dbo].[EntityConfigs]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Key] NVARCHAR(32) NOT NULL,
    [Name] NVARCHAR(128) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,

    CONSTRAINT PK_EntityConfigs
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_EntityConfigs_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT UQ_EntityConfigs_Key
        UNIQUE NONCLUSTERED ([Key])
)
