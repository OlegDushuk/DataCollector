-- Пошук для входу; регістр логіна не враховується (колація бази CI).
CREATE PROCEDURE [dbo].[sp_Users_GetByLogin]
    @Login NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [Login], [DisplayName], [Role], [PasswordHash], [SecurityStamp], [IsActive],
           [FailedLoginCount], [LockoutEnd], [CreatedAt], [UpdatedAt], [LastLoginAt]
    FROM [dbo].[Users]
    WHERE [Login] = @Login;
END
