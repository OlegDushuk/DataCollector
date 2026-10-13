-- Список дашбордів з кількістю віджетів.
CREATE PROCEDURE [dbo].[sp_Dashboards_GetAll]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.[Id],
        d.[Name],
        d.[Description],
        d.[RefreshSeconds],
        d.[CreatedAt],
        d.[UpdatedAt],
        (SELECT COUNT(*) FROM [dbo].[DashboardWidgets] w WHERE w.[DashboardPkey] = d.[Pkey]) AS [WidgetCount]
    FROM [dbo].[Dashboards] d
    ORDER BY COALESCE(d.[UpdatedAt], d.[CreatedAt]) DESC;
END
