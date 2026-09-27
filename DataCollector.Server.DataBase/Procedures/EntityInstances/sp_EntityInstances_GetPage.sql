-- Сторінка записів моделі з сортуванням за полем або датою створення.
-- Повертає три набори: загальна кількість, записи сторінки, значення полів цих записів.
-- @SortPropertyId = NULL -> сортування за CreatedAt.
CREATE PROCEDURE [dbo].[sp_EntityInstances_GetPage]
    @EntityConfigId UNIQUEIDENTIFIER,
    @PageNumber INT,
    @PageSize INT,
    @SortPropertyId UNIQUEIDENTIFIER = NULL,
    @SortDescending BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 10;
    IF @PageSize > 200 SET @PageSize = 200;

    -- 0 = Text, 1 = Number, 2 = Boolean
    DECLARE @SortDataType INT = (
        SELECT [DataType] FROM [dbo].[EntityPropertyConfigs]
        WHERE [Id] = @SortPropertyId AND [EntityConfigId] = @EntityConfigId);

    SELECT COUNT(*) AS [TotalCount]
    FROM [dbo].[EntityInstances]
    WHERE [EntityConfigId] = @EntityConfigId;

    DECLARE @Page TABLE (
        [RowNum] INT IDENTITY(1, 1) PRIMARY KEY,
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL);

    INSERT INTO @Page ([Id], [CreatedAt], [UpdatedAt])
    SELECT i.[Id], i.[CreatedAt], i.[UpdatedAt]
    FROM [dbo].[EntityInstances] i
    OUTER APPLY (
        SELECT v.[Value]
        FROM [dbo].[EntityPropertyInstances] v
        WHERE v.[EntityInstanceId] = i.[Id] AND v.[EntityPropertyConfigId] = @SortPropertyId
    ) s
    WHERE i.[EntityConfigId] = @EntityConfigId
    ORDER BY
        CASE WHEN @SortDescending = 0 AND @SortDataType = 1 THEN TRY_CAST(s.[Value] AS DECIMAL(38, 10)) END ASC,
        CASE WHEN @SortDescending = 1 AND @SortDataType = 1 THEN TRY_CAST(s.[Value] AS DECIMAL(38, 10)) END DESC,
        CASE WHEN @SortDescending = 0 AND @SortDataType IN (0, 2) THEN s.[Value] END ASC,
        CASE WHEN @SortDescending = 1 AND @SortDataType IN (0, 2) THEN s.[Value] END DESC,
        CASE WHEN @SortDescending = 0 THEN i.[CreatedAt] END ASC,
        CASE WHEN @SortDescending = 1 THEN i.[CreatedAt] END DESC,
        i.[Id]
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    SELECT [Id], @EntityConfigId AS [EntityConfigId], [CreatedAt], [UpdatedAt]
    FROM @Page
    ORDER BY [RowNum];

    SELECT v.[Id], v.[EntityInstanceId], v.[EntityPropertyConfigId], v.[Value], v.[CreatedAt]
    FROM [dbo].[EntityPropertyInstances] v
    INNER JOIN @Page p ON p.[Id] = v.[EntityInstanceId];
END
