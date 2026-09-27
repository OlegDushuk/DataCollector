-- Створює запис разом зі значеннями полів.
-- Помилки: 50404 - модель не знайдена.
CREATE PROCEDURE [dbo].[sp_EntityInstances_Create]
    @Id UNIQUEIDENTIFIER,
    @EntityConfigId UNIQUEIDENTIFIER,
    @CreatedAt DATETIME2,
    @Values [dbo].[PropertyValueList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Id] = @EntityConfigId)
        THROW 50404, N'Модель не знайдена', 1;

    BEGIN TRANSACTION;

    INSERT INTO [dbo].[EntityInstances] ([Id], [EntityConfigId], [CreatedAt])
    VALUES (@Id, @EntityConfigId, @CreatedAt);

    INSERT INTO [dbo].[EntityPropertyInstances] ([Id], [EntityInstanceId], [EntityPropertyConfigId], [Value], [CreatedAt])
    SELECT [Id], @Id, [EntityPropertyConfigId], [Value], [CreatedAt]
    FROM @Values;

    COMMIT TRANSACTION;
END
