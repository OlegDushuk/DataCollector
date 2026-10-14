-- Усі ключі (разом з відкликаними) і хто їх створив.
CREATE PROCEDURE [dbo].[sp_ApiKeys_GetAll]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT k.[Id], k.[Name], k.[Prefix], k.[KeyHash], k.[Role], k.[CreatedAt],
           u.[Id] AS [CreatedByUserId], u.[DisplayName] AS [CreatedByName],
           k.[LastUsedAt], k.[RevokedAt]
    FROM [dbo].[ApiKeys] k
    LEFT JOIN [dbo].[Users] u ON u.[Pkey] = k.[CreatedByPkey];
END
