-- Черга доставки на зовнішній канал: найстаріші першими.
CREATE PROCEDURE [dbo].[sp_Notifications_GetPendingDelivery]
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Take)
        n.[Id], r.[Id] AS [RuleId], r.[Name] AS [RuleName], c.[Name] AS [ConfigName], n.[CreatedAt], n.[FromState], n.[ToState],
        n.[Value], n.[Threshold], n.[Message], n.[ReadAt], n.[DeliveryStatus], n.[DeliveryAttempts], n.[DeliveredAt], n.[DeliveryError]
    FROM [dbo].[Notifications] n
    INNER JOIN [dbo].[MonitoringRules] r ON r.[Pkey] = n.[RulePkey]
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = r.[ConfigPkey]
    WHERE n.[DeliveryStatus] = 1
    ORDER BY n.[Pkey];
END
