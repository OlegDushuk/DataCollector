-- Те саме, що GetById, але пошук за ключем моделі (зручно для зовнішніх інтеграцій).
CREATE PROCEDURE [dbo].[sp_EntityConfigs_GetByKey]
    @Key NVARCHAR(32)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Id UNIQUEIDENTIFIER = (SELECT [Id] FROM [dbo].[EntityConfigs] WHERE [Key] = @Key);

    EXEC [dbo].[sp_EntityConfigs_GetById] @Id = @Id;
END
