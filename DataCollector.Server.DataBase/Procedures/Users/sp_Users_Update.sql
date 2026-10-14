-- Зберігає змінювані поля користувача: профіль, роль, пароль, доступ і стан входу.
-- Помилки: 50404 - користувач не знайдений.
CREATE PROCEDURE [dbo].[sp_Users_Update]
    @Id UNIQUEIDENTIFIER,
    @DisplayName NVARCHAR(128),
    @Role TINYINT,
    @PasswordHash VARCHAR(256),
    @SecurityStamp UNIQUEIDENTIFIER,
    @IsActive BIT,
    @FailedLoginCount INT,
    @LockoutEnd DATETIME2,
    @UpdatedAt DATETIME2,
    @LastLoginAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Users]
    SET [DisplayName] = @DisplayName,
        [Role] = @Role,
        [PasswordHash] = @PasswordHash,
        [SecurityStamp] = @SecurityStamp,
        [IsActive] = @IsActive,
        [FailedLoginCount] = @FailedLoginCount,
        [LockoutEnd] = @LockoutEnd,
        [UpdatedAt] = @UpdatedAt,
        [LastLoginAt] = @LastLoginAt
    WHERE [Id] = @Id;

    IF @@ROWCOUNT = 0
        THROW 50404, N'Користувач не знайдений', 1;
END
