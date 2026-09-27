-- Видаляє поле разом з усіма його значеннями в записах.
-- Помилки: 50404 - поле не знайдене.
CREATE PROCEDURE [dbo].[sp_EntityPropertyConfigs_Delete]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[EntityPropertyConfigs] WHERE [Id] = @Id)
        THROW 50404, N'Поле не знайдене', 1;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[EntityPropertyInstances] WHERE [EntityPropertyConfigId] = @Id;
    DELETE FROM [dbo].[EntityPropertyConfigs] WHERE [Id] = @Id;

    COMMIT TRANSACTION;
END
