-- Видаляє дашборд; віджети видаляються каскадно (FK_DashboardWidgets_Dashboards).
-- Помилки: 50404 - дашборд не знайдений.
CREATE PROCEDURE [dbo].[sp_Dashboards_Delete]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[Dashboards] WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50404, N'Дашборд не знайдений', 1;
END
