-- Яким моделям належать записи з переданими Id (відсутні записи не повертаються).
-- Імпорт так визначає, які рядки оновлюють наявні записи, а які створюють нові.
CREATE PROCEDURE [dbo].[sp_EntityInstances_GetOwners]
    @Ids [dbo].[IdList] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    SELECT i.[Id], c.[Id] AS [EntityConfigId]
    FROM @Ids x
    INNER JOIN [dbo].[EntityInstances] i ON i.[Id] = x.[Id]
    INNER JOIN [dbo].[EntityConfigs] c ON c.[Pkey] = i.[ConfigPkey];
END
