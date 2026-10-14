-- Усі користувачі (їх небагато - фільтрація і сортування в застосунку).
CREATE PROCEDURE [dbo].[sp_Users_GetAll]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [Login], [DisplayName], [Role], [PasswordHash], [SecurityStamp], [IsActive],
           [FailedLoginCount], [LockoutEnd], [CreatedAt], [UpdatedAt], [LastLoginAt]
    FROM [dbo].[Users];
END
