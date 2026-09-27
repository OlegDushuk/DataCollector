-- Повертає два набори: запис і його значення.
CREATE PROCEDURE [dbo].[sp_EntityInstances_GetById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [EntityConfigId], [CreatedAt], [UpdatedAt]
    FROM [dbo].[EntityInstances]
    WHERE [Id] = @Id;

    SELECT [Id], [EntityInstanceId], [EntityPropertyConfigId], [Value], [CreatedAt]
    FROM [dbo].[EntityPropertyInstances]
    WHERE [EntityInstanceId] = @Id;
END
