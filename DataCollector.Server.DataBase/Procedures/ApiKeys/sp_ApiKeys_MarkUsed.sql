-- Час останнього використання ключа (застосунок оновлює його не частіше ніж раз на кілька хвилин).
CREATE PROCEDURE [dbo].[sp_ApiKeys_MarkUsed]
    @Id UNIQUEIDENTIFIER,
    @UsedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[ApiKeys]
    SET [LastUsedAt] = @UsedAt
    WHERE [Id] = @Id;
END
