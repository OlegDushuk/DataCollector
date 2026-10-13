-- Сповіщення моніторингу: по одному на кожну зміну стану правила (у не норму і назад у норму).
-- Тут же - прочитання в інтерфейсі і доставка на зовнішній канал (вебхук / Telegram): сповіщення записується
-- в одній транзакції зі зміною стану, а надсилається окремим кроком, тож збій каналу не губить подію.
CREATE TABLE [dbo].[Notifications]
(
    [Pkey] BIGINT IDENTITY(1, 1) NOT NULL,
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [RulePkey] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [FromState] TINYINT NOT NULL,
    [ToState] TINYINT NOT NULL,
    [Value] DECIMAL(38, 10) NULL,
    [Threshold] DECIMAL(38, 10) NOT NULL,       -- поріг на момент сповіщення (правило могли змінити)
    [Message] NVARCHAR(1000) NOT NULL,
    [ReadAt] DATETIME2 NULL,

    -- Доставка: 0 - не потрібна (канал не налаштовано), 1 - очікує, 2 - надіслано, 3 - не вдалося.
    [DeliveryStatus] TINYINT NOT NULL CONSTRAINT DF_Notifications_DeliveryStatus DEFAULT (0),
    [DeliveryAttempts] INT NOT NULL CONSTRAINT DF_Notifications_DeliveryAttempts DEFAULT (0),
    [DeliveredAt] DATETIME2 NULL,
    [DeliveryError] NVARCHAR(1000) NULL,

    CONSTRAINT PK_Notifications
        PRIMARY KEY CLUSTERED ([Pkey]),

    CONSTRAINT UQ_Notifications_Id
        UNIQUE NONCLUSTERED ([Id]),

    CONSTRAINT FK_Notifications_MonitoringRules
        FOREIGN KEY ([RulePkey])
            REFERENCES [dbo].[MonitoringRules] ([Pkey])
            ON DELETE CASCADE,

    CONSTRAINT CK_Notifications_States
        CHECK ([FromState] BETWEEN 0 AND 3 AND [ToState] BETWEEN 0 AND 3 AND [DeliveryStatus] BETWEEN 0 AND 3)
)
GO

CREATE NONCLUSTERED INDEX IX_Notifications_RulePkey
    ON [dbo].[Notifications] ([RulePkey], [CreatedAt] DESC)
GO

-- Лічильник непрочитаних у меню.
CREATE NONCLUSTERED INDEX IX_Notifications_Unread
    ON [dbo].[Notifications] ([CreatedAt])
    WHERE [ReadAt] IS NULL
GO

-- Черга доставки.
CREATE NONCLUSTERED INDEX IX_Notifications_Pending
    ON [dbo].[Notifications] ([Pkey])
    WHERE [DeliveryStatus] = 1
