-- Видаляє модель разом з полями, записами та значеннями.
-- Помилки: 50404 - модель не знайдена.
CREATE PROCEDURE [dbo].[sp_EntityConfigs_Delete]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Id] = @Id)
        THROW 50404, N'Модель не знайдена', 1;

    BEGIN TRANSACTION;

    DELETE v
    FROM [dbo].[EntityPropertyInstances] v
    INNER JOIN [dbo].[EntityInstances] i ON i.[Id] = v.[EntityInstanceId]
    WHERE i.[EntityConfigId] = @Id;

    DELETE FROM [dbo].[EntityInstances] WHERE [EntityConfigId] = @Id;
    DELETE FROM [dbo].[EntityPropertyConfigs] WHERE [EntityConfigId] = @Id;
    DELETE FROM [dbo].[EntityConfigs] WHERE [Id] = @Id;

    COMMIT TRANSACTION;
END
