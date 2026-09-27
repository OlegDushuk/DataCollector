-- Оновлює значення полів запису (upsert по кожному полю).
-- Помилки: 50404 - запис не знайдений.
CREATE PROCEDURE [dbo].[sp_EntityInstances_Update]
    @Id UNIQUEIDENTIFIER,
    @UpdatedAt DATETIME2,
    @Values [dbo].[PropertyValueList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[EntityInstances] WHERE [Id] = @Id)
        THROW 50404, N'Запис не знайдений', 1;

    BEGIN TRANSACTION;

    UPDATE [dbo].[EntityInstances] SET [UpdatedAt] = @UpdatedAt WHERE [Id] = @Id;

    MERGE [dbo].[EntityPropertyInstances] AS t
    USING @Values AS s
        ON t.[EntityInstanceId] = @Id AND t.[EntityPropertyConfigId] = s.[EntityPropertyConfigId]
    WHEN MATCHED THEN
        UPDATE SET t.[Value] = s.[Value]
    WHEN NOT MATCHED BY TARGET THEN
        INSERT ([Id], [EntityInstanceId], [EntityPropertyConfigId], [Value], [CreatedAt])
        VALUES (s.[Id], @Id, s.[EntityPropertyConfigId], s.[Value], s.[CreatedAt]);

    COMMIT TRANSACTION;
END
