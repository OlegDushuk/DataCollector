-- Помилки: 50404 - запис не знайдений.
CREATE PROCEDURE [dbo].[sp_EntityInstances_Delete]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[EntityInstances] WHERE [Id] = @Id)
        THROW 50404, N'Запис не знайдений', 1;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[EntityPropertyInstances] WHERE [EntityInstanceId] = @Id;
    DELETE FROM [dbo].[EntityInstances] WHERE [Id] = @Id;

    COMMIT TRANSACTION;
END
