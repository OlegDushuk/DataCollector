-- Дашборд (перший набір) і його віджети в порядку розкладки (другий набір).
CREATE PROCEDURE [dbo].[sp_Dashboards_GetById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Pkey INT = (SELECT [Pkey] FROM [dbo].[Dashboards] WHERE [Id] = @Id);

    SELECT [Id], [Name], [Description], [RefreshSeconds], [CreatedAt], [UpdatedAt], CAST([RowVersion] AS BIGINT) AS [Version]
    FROM [dbo].[Dashboards]
    WHERE [Pkey] = @Pkey;

    SELECT
        w.[Id],
        c.[Id] AS [ConfigId],
        w.[Type],
        w.[Title],
        w.[X],
        w.[Y],
        w.[W],
        w.[H],
        w.[Settings]
    FROM [dbo].[DashboardWidgets] w
    LEFT JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = w.[ConfigPkey]
    WHERE w.[DashboardPkey] = @Pkey
    ORDER BY w.[Y], w.[X];
END
