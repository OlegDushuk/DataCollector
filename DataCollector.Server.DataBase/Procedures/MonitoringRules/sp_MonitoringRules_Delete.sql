-- Видаляє правило; його сповіщення видаляються каскадно.
-- Помилки: 50404 - правило не знайдене.
CREATE PROCEDURE [dbo].[sp_MonitoringRules_Delete]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[MonitoringRules] WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50404, N'Правило не знайдене', 1;
END
