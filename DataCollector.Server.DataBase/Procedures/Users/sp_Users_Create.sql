-- Помилки: 2627 (UQ_Users_Login) - логін зайнятий.
CREATE PROCEDURE [dbo].[sp_Users_Create]
    @Id UNIQUEIDENTIFIER,
    @Login NVARCHAR(64),
    @DisplayName NVARCHAR(128),
    @Role TINYINT,
    @PasswordHash VARCHAR(256),
    @SecurityStamp UNIQUEIDENTIFIER,
    @IsActive BIT,
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Users] ([Id], [Login], [DisplayName], [Role], [PasswordHash], [SecurityStamp], [IsActive], [CreatedAt])
    VALUES (@Id, @Login, @DisplayName, @Role, @PasswordHash, @SecurityStamp, @IsActive, @CreatedAt);
END
