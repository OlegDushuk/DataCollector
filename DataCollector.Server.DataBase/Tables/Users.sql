-- Користувачі системи. Пароль - лише хеш PBKDF2 (формат PBKDF2$SHA256$ітерації$сіль$хеш).
-- SecurityStamp змінюється разом з паролем, роллю або доступом: токени, видані раніше, перестають прийматися.
-- Role: 0 - перегляд, 1 - редактор, 2 - адміністратор.
CREATE TABLE [dbo].[Users]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    -- Порівняння без урахування регістру (колація бази CI): «Admin» і «admin» - один логін.
    [Login] NVARCHAR(64) NOT NULL,
    [DisplayName] NVARCHAR(128) NOT NULL,
    [Role] TINYINT NOT NULL,
    [PasswordHash] VARCHAR(256) NOT NULL,
    [SecurityStamp] UNIQUEIDENTIFIER NOT NULL,
    [IsActive] BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    -- Невдалі спроби входу поспіль і блокування після них (захист від підбору пароля).
    [FailedLoginCount] INT NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0),
    [LockoutEnd] DATETIME2 NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [LastLoginAt] DATETIME2 NULL,

    CONSTRAINT PK_Users
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_Users_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT UQ_Users_Login
        UNIQUE NONCLUSTERED ([Login]),

    CONSTRAINT CK_Users_Role
        CHECK ([Role] BETWEEN 0 AND 2),

    CONSTRAINT CK_Users_FailedLoginCount
        CHECK ([FailedLoginCount] >= 0)
)
