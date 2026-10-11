-- Значення полів записів: EAV з типізованими колонками.
-- Ключ - пара (запис, поле): 12 байт; нові записи мають зростаючий InstancePkey,
-- тож значення дописуються в кінець кластерного індексу.
-- Для пари (запис, поле) зберігається не більше одного рядка, і в ньому заповнена рівно одна колонка -
-- та, що відповідає типу поля (Text -> ValueText, Number -> ValueNumber, Boolean -> ValueBool, Date -> ValueDate).
-- Порожні значення не зберігаються: відсутній рядок означає NULL.
-- ValueNumber і ValueDate оголошені SPARSE: для NULL вони не займають місця в рядку.
CREATE TABLE [dbo].[EntityPropertyInstances]
(
    [InstancePkey] BIGINT NOT NULL,
    [PropertyPkey] INT NOT NULL,
    [ValueText] NVARCHAR(4000) NULL,
    [ValueNumber] DECIMAL(38, 10) SPARSE NULL,
    [ValueBool] BIT NULL,
    [ValueDate] DATETIME2 SPARSE NULL,

    CONSTRAINT PK_EntityPropertyInstances
        PRIMARY KEY CLUSTERED ([InstancePkey], [PropertyPkey]),

    CONSTRAINT FK_EntityPropertyInstances_EntityInstances
        FOREIGN KEY ([InstancePkey])
            REFERENCES [dbo].[EntityInstances] ([Pkey]),

    CONSTRAINT FK_EntityPropertyInstances_EntityPropertyConfigs
        FOREIGN KEY ([PropertyPkey])
            REFERENCES [dbo].[EntityPropertyConfigs] ([Pkey]),

    CONSTRAINT CK_EntityPropertyInstances_SingleValue CHECK (
          CASE WHEN [ValueText]   IS NULL THEN 0 ELSE 1 END
        + CASE WHEN [ValueNumber] IS NULL THEN 0 ELSE 1 END
        + CASE WHEN [ValueBool]   IS NULL THEN 0 ELSE 1 END
        + CASE WHEN [ValueDate]   IS NULL THEN 0 ELSE 1 END = 1)
)
GO

-- Усі значення поля: видалення поля, вибірка за полем (InstancePkey додається автоматично як ключ кластерного індексу).
CREATE INDEX IX_EntityPropertyInstances_PropertyPkey
    ON [dbo].[EntityPropertyInstances] ([PropertyPkey])
GO

-- Фільтрація, сортування та агрегація за значенням конкретного поля.
CREATE INDEX IX_EntityPropertyInstances_Number
    ON [dbo].[EntityPropertyInstances] ([PropertyPkey], [ValueNumber])
    WHERE [ValueNumber] IS NOT NULL
GO

CREATE INDEX IX_EntityPropertyInstances_Bool
    ON [dbo].[EntityPropertyInstances] ([PropertyPkey], [ValueBool])
    WHERE [ValueBool] IS NOT NULL
GO

CREATE INDEX IX_EntityPropertyInstances_Date
    ON [dbo].[EntityPropertyInstances] ([PropertyPkey], [ValueDate])
    WHERE [ValueDate] IS NOT NULL
