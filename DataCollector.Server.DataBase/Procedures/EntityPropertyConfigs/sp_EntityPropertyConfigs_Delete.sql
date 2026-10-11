-- Видаляє поле разом з усіма його значеннями в записах.
-- Помилки: 50404 - поле не знайдене.
CREATE PROCEDURE [dbo].[sp_EntityPropertyConfigs_Delete]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Pkey INT = (SELECT [Pkey] FROM [dbo].[EntityPropertyConfigs] WHERE [Id] = @Id);

    IF @Pkey IS NULL
        THROW 50404, N'Поле не знайдене', 1;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[EntityPropertyInstances] WHERE [PropertyPkey] = @Pkey;
    DELETE FROM [dbo].[EntityPropertyConfigs] WHERE [Pkey] = @Pkey;

    COMMIT TRANSACTION;
END
