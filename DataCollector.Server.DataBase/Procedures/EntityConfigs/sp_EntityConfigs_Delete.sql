-- Видаляє модель разом з полями, записами та значеннями.
-- Віджети дашбордів, що показували цю модель, лишаються без моделі (ConfigPkey = NULL).
-- Помилки: 50404 - модель не знайдена.
CREATE PROCEDURE [dbo].[sp_EntityConfigs_Delete]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Pkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @Id);

    IF @Pkey IS NULL
        THROW 50404, N'Модель не знайдена', 1;

    BEGIN TRANSACTION;

    DELETE v
    FROM [dbo].[EntityPropertyInstances] v
    INNER JOIN [dbo].[EntityInstances] i ON i.[Pkey] = v.[InstancePkey]
    WHERE i.[ConfigPkey] = @Pkey;

    DELETE FROM [dbo].[EntityInstances] WHERE [ConfigPkey] = @Pkey;
    UPDATE [dbo].[DashboardWidgets] SET [ConfigPkey] = NULL WHERE [ConfigPkey] = @Pkey;
    DELETE FROM [dbo].[EntityPropertyConfigs] WHERE [ConfigPkey] = @Pkey;
    DELETE FROM [dbo].[EntityConfigs] WHERE [Pkey] = @Pkey;

    COMMIT TRANSACTION;
END
