-- Результат спроби доставки: статус (1 - ще спробувати, 2 - надіслано, 3 - не вдалося), кількість спроб, помилка.
CREATE PROCEDURE [dbo].[sp_Notifications_SetDelivery]
    @Id UNIQUEIDENTIFIER,
    @DeliveryStatus TINYINT,
    @DeliveryAttempts INT,
    @DeliveredAt DATETIME2,
    @DeliveryError NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Notifications]
    SET [DeliveryStatus] = @DeliveryStatus,
        [DeliveryAttempts] = @DeliveryAttempts,
        [DeliveredAt] = @DeliveredAt,
        [DeliveryError] = @DeliveryError
    WHERE [Id] = @Id;
END
