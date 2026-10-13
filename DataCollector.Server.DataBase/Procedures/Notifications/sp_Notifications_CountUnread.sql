CREATE PROCEDURE [dbo].[sp_Notifications_CountUnread]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS [Count] FROM [dbo].[Notifications] WHERE [ReadAt] IS NULL;
END
