-- Поля моделей.
CREATE TABLE [dbo].[EntityPropertyConfigs]
(
    [Pkey] INT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ConfigPkey] INT NOT NULL,
    [Key] NVARCHAR(32) NOT NULL,
    [Name] NVARCHAR(128) NOT NULL,
    [DataType] INT NOT NULL,    -- 0 = Text, 1 = Number, 2 = Boolean
    [CreatedAt] DATETIME2 NOT NULL,

    CONSTRAINT PK_EntityPropertyConfigs
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_EntityPropertyConfigs_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT UQ_EntityPropertyConfigs_ConfigPkey_Key
        UNIQUE NONCLUSTERED ([ConfigPkey], [Key]),

    CONSTRAINT FK_EntityPropertyConfigs_EntityConfigs
        FOREIGN KEY ([ConfigPkey])
            REFERENCES [dbo].[EntityConfigs] ([Pkey])
)
