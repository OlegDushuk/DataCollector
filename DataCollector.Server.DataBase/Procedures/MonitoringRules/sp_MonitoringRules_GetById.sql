CREATE PROCEDURE [dbo].[sp_MonitoringRules_GetById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.[Id], c.[Id] AS [ConfigId], c.[Name] AS [ConfigName], r.[Name], r.[IsEnabled], r.[Filters], r.[Search], r.[Function], r.[FieldKey],
        r.[TimeField], r.[WindowMinutes], r.[Operator], r.[Threshold], r.[ConsecutiveChecks], r.[CheckIntervalSeconds],
        r.[State], r.[StateSince], r.[LastValue], r.[LastCheckedAt], r.[LastError], r.[PendingState], r.[PendingCount],
        r.[NextCheckAt], r.[Version], r.[CreatedAt], r.[UpdatedAt],
        r.[WindowUnit], r.[WindowOffset], r.[WindowToDate], r.[TimeZone],
        cc.[Id] AS [CompareConfigId], r.[CompareFilters], r.[CompareSearch], r.[CompareFunction], r.[CompareFieldKey],
        r.[CompareTimeField], r.[CompareWindowMinutes], r.[CompareWindowUnit], r.[CompareWindowOffset], r.[CompareWindowToDate],
        r.[CompareMultiplier], r.[LastCompareValue]
    FROM [dbo].[MonitoringRules] r
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = r.[ConfigPkey]
    LEFT JOIN [dbo].[EntityConfigs] cc ON cc.[Pkey] = r.[CompareConfigPkey]
    WHERE r.[Id] = @Id;
END
