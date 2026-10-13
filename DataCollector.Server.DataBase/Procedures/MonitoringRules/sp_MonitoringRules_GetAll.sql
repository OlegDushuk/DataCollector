-- Усі правила з назвою моделі: спершу ті, що спрацювали, потім без даних, далі за назвою.
CREATE PROCEDURE [dbo].[sp_MonitoringRules_GetAll]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.[Id], c.[Id] AS [ConfigId], c.[Name] AS [ConfigName], r.[Name], r.[IsEnabled], r.[Filters], r.[Search], r.[Function], r.[FieldKey],
        r.[TimeField], r.[WindowMinutes], r.[Operator], r.[Threshold], r.[ConsecutiveChecks], r.[CheckIntervalSeconds],
        r.[State], r.[StateSince], r.[LastValue], r.[LastCheckedAt], r.[LastError], r.[PendingState], r.[PendingCount],
        r.[NextCheckAt], r.[Version], r.[CreatedAt], r.[UpdatedAt]
    FROM [dbo].[MonitoringRules] r
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = r.[ConfigPkey]
    ORDER BY CASE r.[State] WHEN 2 THEN 0 WHEN 3 THEN 1 ELSE 2 END, r.[Name];
END
