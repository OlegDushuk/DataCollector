CREATE PROCEDURE [dbo].[sp_Users_GetById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [Login], [DisplayName], [Role], [PasswordHash], [SecurityStamp], [IsActive],
           [FailedLoginCount], [LockoutEnd], [CreatedAt], [UpdatedAt], [LastLoginAt]
    FROM [dbo].[Users]
    WHERE [Id] = @Id;
END
