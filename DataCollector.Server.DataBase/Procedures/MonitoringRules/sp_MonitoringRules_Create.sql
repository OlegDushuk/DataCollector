-- Нове правило: перша перевірка - одразу (NextCheckAt = час створення).
-- Помилки: 50404 - модель (або модель показника порівняння) не знайдена.
CREATE PROCEDURE [dbo].[sp_MonitoringRules_Create]
    @Id UNIQUEIDENTIFIER,
    @ConfigId UNIQUEIDENTIFIER,
    @Name NVARCHAR(128),
    @IsEnabled BIT,
    @Filters NVARCHAR(MAX),
    @Search NVARCHAR(200),
    @Function NVARCHAR(8),
    @FieldKey NVARCHAR(32),
    @TimeField NVARCHAR(32),
    @WindowMinutes INT,
    @Operator NVARCHAR(4),
    @Threshold DECIMAL(38, 10),
    @ConsecutiveChecks INT,
    @CheckIntervalSeconds INT,
    @WindowUnit NVARCHAR(8) = NULL,
    @WindowOffset INT = 0,
    @WindowToDate BIT = 0,
    @TimeZone NVARCHAR(64) = NULL,
    -- Показник порівняння; @CompareConfigId = NULL - фіксований поріг.
    @CompareConfigId UNIQUEIDENTIFIER = NULL,
    @CompareFilters NVARCHAR(MAX) = NULL,
    @CompareSearch NVARCHAR(200) = NULL,
    @CompareFunction NVARCHAR(8) = NULL,
    @CompareFieldKey NVARCHAR(32) = NULL,
    @CompareTimeField NVARCHAR(32) = NULL,
    @CompareWindowMinutes INT = NULL,
    @CompareWindowUnit NVARCHAR(8) = NULL,
    @CompareWindowOffset INT = 0,
    @CompareWindowToDate BIT = 0,
    @CompareMultiplier DECIMAL(38, 10) = NULL,
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @ConfigId);

    IF @ConfigPkey IS NULL
        THROW 50404, N'Модель не знайдена', 1;

    DECLARE @CompareConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @CompareConfigId);

    IF @CompareConfigId IS NOT NULL AND @CompareConfigPkey IS NULL
        THROW 50404, N'Модель показника порівняння не знайдена', 1;

    INSERT INTO [dbo].[MonitoringRules]
        ([Id], [ConfigPkey], [Name], [IsEnabled], [Filters], [Search], [Function], [FieldKey], [TimeField], [WindowMinutes],
         [Operator], [Threshold], [ConsecutiveChecks], [CheckIntervalSeconds], [NextCheckAt], [CreatedAt],
         [WindowUnit], [WindowOffset], [WindowToDate], [TimeZone],
         [CompareConfigPkey], [CompareFilters], [CompareSearch], [CompareFunction], [CompareFieldKey], [CompareTimeField],
         [CompareWindowMinutes], [CompareWindowUnit], [CompareWindowOffset], [CompareWindowToDate], [CompareMultiplier])
    VALUES
        (@Id, @ConfigPkey, @Name, @IsEnabled, @Filters, @Search, @Function, @FieldKey, @TimeField, @WindowMinutes,
         @Operator, @Threshold, @ConsecutiveChecks, @CheckIntervalSeconds, @CreatedAt, @CreatedAt,
         @WindowUnit, @WindowOffset, @WindowToDate, @TimeZone,
         @CompareConfigPkey, @CompareFilters, @CompareSearch, @CompareFunction, @CompareFieldKey, @CompareTimeField,
         @CompareWindowMinutes, @CompareWindowUnit, @CompareWindowOffset, @CompareWindowToDate, @CompareMultiplier);
END
