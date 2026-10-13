-- Останні сповіщення (новіші першими): усі, лише непрочитані або одного правила; @Before - для «показати ще».
CREATE PROCEDURE [dbo].[sp_Notifications_GetRecent]
    @Take INT,
    @UnreadOnly BIT,
    @RuleId UNIQUEIDENTIFIER,
    @Before DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Take)
        n.[Id], r.[Id] AS [RuleId], r.[Name] AS [RuleName], c.[Name] AS [ConfigName], n.[CreatedAt], n.[FromState], n.[ToState],
        n.[Value], n.[Threshold], n.[Message], n.[ReadAt], n.[DeliveryStatus], n.[DeliveryAttempts], n.[DeliveredAt], n.[DeliveryError]
    FROM [dbo].[Notifications] n
    INNER JOIN [dbo].[MonitoringRules] r ON r.[Pkey] = n.[RulePkey]
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = r.[ConfigPkey]
    WHERE (@UnreadOnly = 0 OR n.[ReadAt] IS NULL)
      AND (@RuleId IS NULL OR r.[Id] = @RuleId)
      AND (@Before IS NULL OR n.[CreatedAt] < @Before)
    ORDER BY n.[CreatedAt] DESC, n.[Pkey] DESC;
END
