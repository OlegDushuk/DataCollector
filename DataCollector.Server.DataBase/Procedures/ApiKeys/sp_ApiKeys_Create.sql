CREATE PROCEDURE [dbo].[sp_ApiKeys_Create]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(128),
    @Prefix VARCHAR(16),
    @KeyHash BINARY(32),
    @Role TINYINT,
    @CreatedAt DATETIME2,
    @CreatedByUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[ApiKeys] ([Id], [Name], [Prefix], [KeyHash], [Role], [CreatedAt], [CreatedByPkey])
    VALUES (@Id, @Name, @Prefix, @KeyHash, @Role, @CreatedAt,
            (SELECT [Pkey] FROM [dbo].[Users] WHERE [Id] = @CreatedByUserId));
END
