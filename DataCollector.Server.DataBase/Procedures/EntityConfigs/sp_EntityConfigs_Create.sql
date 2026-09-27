CREATE PROCEDURE [dbo].[sp_EntityConfigs_Create]
    @Id UNIQUEIDENTIFIER,
    @Key NVARCHAR(32),
    @Name NVARCHAR(32),
    @CreatedAt DATETIME2
AS
BEGIN
    IF EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Key] = @Key)
    BEGIN
        SELECT '' AS Result;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] WHERE [Name] = @Name)
    BEGIN
        SELECT '' AS Result;
        RETURN;
    END
    
    INSERT INTO [dbo].[EntityConfigs] ([Id], [Key], [Name], [CreatedAt])
    VALUES (@Id, @Key, @Name, @CreatedAt)
    
    SELECT '' AS Result;
END