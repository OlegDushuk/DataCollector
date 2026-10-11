-- Помилки: 50404 - запис не знайдений.
CREATE PROCEDURE [dbo].[sp_EntityInstances_Delete]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Pkey BIGINT = (SELECT [Pkey] FROM [dbo].[EntityInstances] WHERE [Id] = @Id);

    IF @Pkey IS NULL
        THROW 50404, N'Запис не знайдений', 1;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[EntityPropertyInstances] WHERE [InstancePkey] = @Pkey;
    DELETE FROM [dbo].[EntityInstances] WHERE [Pkey] = @Pkey;

    COMMIT TRANSACTION;
END
