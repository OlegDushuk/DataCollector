-- Правила, яким настав час перевірки. Наступна перевірка призначається одразу, в тому ж UPDATE:
-- якщо запущено кілька екземплярів API, одне правило не перевіряють двічі (READPAST пропускає рядки,
-- які зараз бере інший екземпляр), а правило, перевірка якого впала з помилкою, не перевіряється щосекунди.
CREATE PROCEDURE [dbo].[sp_MonitoringRules_ClaimDue]
    @Now DATETIME2,
    @Take INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Claimed TABLE ([Pkey] INT PRIMARY KEY);

    UPDATE TOP (@Take) r
    SET r.[NextCheckAt] = DATEADD(SECOND, r.[CheckIntervalSeconds], @Now)
    OUTPUT inserted.[Pkey] INTO @Claimed
    FROM [dbo].[MonitoringRules] r WITH (ROWLOCK, READPAST)
    WHERE r.[IsEnabled] = 1
      AND r.[NextCheckAt] <= @Now;

    SELECT
        r.[Id], c.[Id] AS [ConfigId], c.[Name] AS [ConfigName], r.[Name], r.[IsEnabled], r.[Filters], r.[Search], r.[Function], r.[FieldKey],
        r.[TimeField], r.[WindowMinutes], r.[Operator], r.[Threshold], r.[ConsecutiveChecks], r.[CheckIntervalSeconds],
        r.[State], r.[StateSince], r.[LastValue], r.[LastCheckedAt], r.[LastError], r.[PendingState], r.[PendingCount],
        r.[NextCheckAt], r.[Version], r.[CreatedAt], r.[UpdatedAt]
    FROM [dbo].[MonitoringRules] r
    INNER JOIN @Claimed x ON x.[Pkey] = r.[Pkey]
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = r.[ConfigPkey];
END
