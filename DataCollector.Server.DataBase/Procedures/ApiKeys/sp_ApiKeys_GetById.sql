CREATE PROCEDURE [dbo].[sp_ApiKeys_GetById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT k.[Id], k.[Name], k.[Prefix], k.[KeyHash], k.[Role], k.[CreatedAt],
           u.[Id] AS [CreatedByUserId], k.[LastUsedAt], k.[RevokedAt]
    FROM [dbo].[ApiKeys] k
    LEFT JOIN [dbo].[Users] u ON u.[Pkey] = k.[CreatedByPkey]
    WHERE k.[Id] = @Id;
END
