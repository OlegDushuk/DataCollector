-- Відкликає ключ (запис лишається для історії). Помилки: 50404 - ключ не знайдений.
CREATE PROCEDURE [dbo].[sp_ApiKeys_Revoke]
    @Id UNIQUEIDENTIFIER,
    @RevokedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[ApiKeys]
    SET [RevokedAt] = COALESCE([RevokedAt], @RevokedAt)
    WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50404, N'API-ключ не знайдений', 1;
END
