-- Пошук ключа з заголовка X-Api-Key (за SHA-256, унікальний індекс).
CREATE PROCEDURE [dbo].[sp_ApiKeys_GetByHash]
    @KeyHash BINARY(32)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT k.[Id], k.[Name], k.[Prefix], k.[KeyHash], k.[Role], k.[CreatedAt],
           u.[Id] AS [CreatedByUserId], k.[LastUsedAt], k.[RevokedAt]
    FROM [dbo].[ApiKeys] k
    LEFT JOIN [dbo].[Users] u ON u.[Pkey] = k.[CreatedByPkey]
    WHERE k.[KeyHash] = @KeyHash;
END
