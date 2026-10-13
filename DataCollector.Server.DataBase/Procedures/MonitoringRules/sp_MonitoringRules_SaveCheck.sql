-- Результат перевірки: новий стан правила і (якщо стан змінився) сповіщення - в одній транзакції.
-- Записується лише якщо визначення правила не змінили під час перевірки (@Version).
-- Повертає Saved = 1, якщо записано.
CREATE PROCEDURE [dbo].[sp_MonitoringRules_SaveCheck]
    @Id UNIQUEIDENTIFIER,
    @Version INT,
    @State TINYINT,
    @StateSince DATETIME2,
    @LastValue DECIMAL(38, 10),
    @LastCheckedAt DATETIME2,
    @LastError NVARCHAR(1000),
    @PendingState TINYINT,
    @PendingCount INT,
    -- Сповіщення (NULL - стан не змінився).
    @NotificationId UNIQUEIDENTIFIER,
    @FromState TINYINT,
    @ToState TINYINT,
    @Value DECIMAL(38, 10),
    @Threshold DECIMAL(38, 10),
    @Message NVARCHAR(1000),
    @DeliveryStatus TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Pkey INT;

    BEGIN TRANSACTION;

    UPDATE [dbo].[MonitoringRules]
    SET @Pkey = [Pkey],
        [State] = @State,
        [StateSince] = @StateSince,
        [LastValue] = @LastValue,
        [LastCheckedAt] = @LastCheckedAt,
        [LastError] = @LastError,
        [PendingState] = @PendingState,
        [PendingCount] = @PendingCount
    WHERE [Id] = @Id
      AND [Version] = @Version;

    IF @Pkey IS NOT NULL AND @NotificationId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[Notifications]
            ([Id], [RulePkey], [CreatedAt], [FromState], [ToState], [Value], [Threshold], [Message], [DeliveryStatus])
        VALUES
            (@NotificationId, @Pkey, @LastCheckedAt, @FromState, @ToState, @Value, @Threshold, @Message, @DeliveryStatus);
    END

    COMMIT TRANSACTION;

    SELECT CAST(CASE WHEN @Pkey IS NULL THEN 0 ELSE 1 END AS BIT) AS [Saved];
END
