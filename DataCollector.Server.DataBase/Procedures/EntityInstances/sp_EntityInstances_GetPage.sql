-- Сторінка записів моделі з сортуванням за полем або датою створення.
-- Повертає три набори: загальна кількість, записи сторінки, значення полів цих записів.
-- @SortPropertyId = NULL -> сортування за CreatedAt.
-- Значення поля завжди лежить в одній типізованій колонці, тому сортування за всіма колонками по черзі
-- дає правильний порядок для будь-якого типу без перетворень. Записи без значення йдуть першими (як NULL).
CREATE PROCEDURE [dbo].[sp_EntityInstances_GetPage]
    @EntityConfigId UNIQUEIDENTIFIER,
    @PageNumber INT,
    @PageSize INT,
    @SortPropertyId UNIQUEIDENTIFIER = NULL,
    @SortDescending BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- Межі розміру сторінки перевіряє DataInstanceService (MaxPageSize), тут лише захист від некоректних значень.
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 10;

    DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @EntityConfigId);

    DECLARE @SortPropertyPkey INT = (
        SELECT [Pkey] FROM [dbo].[EntityPropertyConfigs]
        WHERE [Id] = @SortPropertyId AND [ConfigPkey] = @ConfigPkey);

    SELECT COUNT(*) AS [TotalCount]
    FROM [dbo].[EntityInstances]
    WHERE [ConfigPkey] = @ConfigPkey;

    DECLARE @Page TABLE (
        [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
        [Pkey] BIGINT NOT NULL,
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL);

    INSERT INTO @Page ([Pkey], [Id], [CreatedAt], [UpdatedAt])
    SELECT i.[Pkey], i.[Id], i.[CreatedAt], i.[UpdatedAt]
    FROM [dbo].[EntityInstances] i
    LEFT JOIN [dbo].[EntityPropertyInstances] s
        ON s.[InstancePkey] = i.[Pkey] AND s.[PropertyPkey] = @SortPropertyPkey
    WHERE i.[ConfigPkey] = @ConfigPkey
    ORDER BY
        CASE WHEN @SortDescending = 0 THEN s.[ValueNumber] END ASC,
        CASE WHEN @SortDescending = 1 THEN s.[ValueNumber] END DESC,
        CASE WHEN @SortDescending = 0 THEN s.[ValueDate] END ASC,
        CASE WHEN @SortDescending = 1 THEN s.[ValueDate] END DESC,
        CASE WHEN @SortDescending = 0 THEN s.[ValueBool] END ASC,
        CASE WHEN @SortDescending = 1 THEN s.[ValueBool] END DESC,
        CASE WHEN @SortDescending = 0 THEN s.[ValueText] END ASC,
        CASE WHEN @SortDescending = 1 THEN s.[ValueText] END DESC,
        CASE WHEN @SortDescending = 0 THEN i.[CreatedAt] END ASC,
        CASE WHEN @SortDescending = 1 THEN i.[CreatedAt] END DESC,
        i.[Pkey]
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    SELECT [Id], @EntityConfigId AS [EntityConfigId], [CreatedAt], [UpdatedAt]
    FROM @Page
    ORDER BY [RowNum];

    SELECT
        pg.[Id] AS [EntityInstanceId],
        p.[Id] AS [EntityPropertyConfigId],
        v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
    FROM @Page pg
    INNER JOIN [dbo].[EntityPropertyInstances] v ON v.[InstancePkey] = pg.[Pkey]
    INNER JOIN [dbo].[EntityPropertyConfigs] p ON p.[Pkey] = v.[PropertyPkey];
END
