-- Змінює ключ і назву поля. Тип поля не змінюється.
-- Помилки: 50404 - поле не знайдене, 50409 - ключ зайнятий іншим полем цієї моделі.
CREATE PROCEDURE [dbo].[sp_EntityPropertyConfigs_Update]
    @Id UNIQUEIDENTIFIER,
    @Key NVARCHAR(32),
    @Name NVARCHAR(32)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EntityConfigId UNIQUEIDENTIFIER =
        (SELECT [EntityConfigId] FROM [dbo].[EntityPropertyConfigs] WHERE [Id] = @Id);

    IF @EntityConfigId IS NULL
        THROW 50404, N'Поле не знайдене', 1;

    IF EXISTS (
        SELECT 1 FROM [dbo].[EntityPropertyConfigs]
        WHERE [EntityConfigId] = @EntityConfigId AND [Key] = @Key AND [Id] <> @Id)
        THROW 50409, N'Поле з таким ключем вже існує', 1;

    UPDATE [dbo].[EntityPropertyConfigs]
    SET [Key] = @Key,
        [Name] = @Name
    WHERE [Id] = @Id;
END
