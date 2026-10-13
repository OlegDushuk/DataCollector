-- Позначити прочитаним одне сповіщення (@Id) або всі (@Id = NULL).
CREATE PROCEDURE [dbo].[sp_Notifications_MarkRead]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Notifications]
    SET [ReadAt] = @Now
    WHERE [ReadAt] IS NULL
      AND (@Id IS NULL OR [Id] = @Id);
END
