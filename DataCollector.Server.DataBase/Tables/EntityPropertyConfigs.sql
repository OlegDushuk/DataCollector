-- Поля моделей.
CREATE TABLE [dbo].[EntityPropertyConfigs]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ConfigPkey] INT NOT NULL,
    [Key] NVARCHAR(32) NOT NULL,
    [Name] NVARCHAR(128) NOT NULL,
    [DataType] INT NOT NULL,    -- 0 Text, 1 Number, 2 Boolean, 3 Date, 4 DateTime, 5 Enum
    [CreatedAt] DATETIME2 NOT NULL,

    -- Правила поля. Загальні для всіх типів - окремими колонками,
    -- специфічні для типу (min/max, довжина, варіанти переліку) - JSON у [Settings].
    -- Значення в [DefaultValue] і [Settings] - текст в інваріантному форматі (як приймає API).
    [IsRequired] BIT NOT NULL CONSTRAINT DF_EntityPropertyConfigs_IsRequired DEFAULT (0),
    [DefaultValue] NVARCHAR(4000) NULL,
    [Settings] NVARCHAR(MAX) NULL,

    CONSTRAINT PK_EntityPropertyConfigs
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_EntityPropertyConfigs_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT UQ_EntityPropertyConfigs_ConfigPkey_Key
        UNIQUE NONCLUSTERED ([ConfigPkey], [Key]),

    CONSTRAINT FK_EntityPropertyConfigs_EntityConfigs
        FOREIGN KEY ([ConfigPkey])
            REFERENCES [dbo].[EntityConfigs] ([Pkey]),

    CONSTRAINT CK_EntityPropertyConfigs_DataType
        CHECK ([DataType] BETWEEN 0 AND 5),

    CONSTRAINT CK_EntityPropertyConfigs_Settings
        CHECK ([Settings] IS NULL OR ISJSON([Settings]) = 1)
)
