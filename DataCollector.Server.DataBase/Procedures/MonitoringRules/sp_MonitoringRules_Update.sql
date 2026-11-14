-- Нове визначення правила. Стан зберігається (поріг підправили - сповіщення лише якщо стан справді зміниться),
-- але незавершений перехід скидається, а перевірка відбувається одразу. Інша модель - стан починається заново.
-- Version збільшується: результат перевірки, що йшла за старим визначенням, не буде записано.
-- @ExpectedVersion - версія, яку бачив редактор (оптимістична конкурентність); NULL - без перевірки.
-- Помилки: 50404 - правило або модель (модель показника порівняння) не знайдені, 50409 - правило змінено після того, як його відкрили.
CREATE PROCEDURE [dbo].[sp_MonitoringRules_Update]
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
    @UpdatedAt DATETIME2,
    @ExpectedVersion INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @ConfigId);

    IF @ConfigPkey IS NULL
        THROW 50404, N'Модель не знайдена', 1;

    DECLARE @CompareConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @CompareConfigId);

    IF @CompareConfigId IS NOT NULL AND @CompareConfigPkey IS NULL
        THROW 50404, N'Модель показника порівняння не знайдена', 1;

    UPDATE [dbo].[MonitoringRules]
    SET [State] = CASE WHEN [ConfigPkey] = @ConfigPkey THEN [State] ELSE 0 END,
        [StateSince] = CASE WHEN [ConfigPkey] = @ConfigPkey THEN [StateSince] ELSE NULL END,
        [LastValue] = CASE WHEN [ConfigPkey] = @ConfigPkey THEN [LastValue] ELSE NULL END,
        [LastCompareValue] = CASE WHEN [ConfigPkey] = @ConfigPkey AND @CompareConfigPkey IS NOT NULL THEN [LastCompareValue] ELSE NULL END,
        [ConfigPkey] = @ConfigPkey,
        [Name] = @Name,
        [IsEnabled] = @IsEnabled,
        [Filters] = @Filters,
        [Search] = @Search,
        [Function] = @Function,
        [FieldKey] = @FieldKey,
        [TimeField] = @TimeField,
        [WindowMinutes] = @WindowMinutes,
        [Operator] = @Operator,
        [Threshold] = @Threshold,
        [ConsecutiveChecks] = @ConsecutiveChecks,
        [CheckIntervalSeconds] = @CheckIntervalSeconds,
        [WindowUnit] = @WindowUnit,
        [WindowOffset] = @WindowOffset,
        [WindowToDate] = @WindowToDate,
        [TimeZone] = @TimeZone,
        [CompareConfigPkey] = @CompareConfigPkey,
        [CompareFilters] = @CompareFilters,
        [CompareSearch] = @CompareSearch,
        [CompareFunction] = @CompareFunction,
        [CompareFieldKey] = @CompareFieldKey,
        [CompareTimeField] = @CompareTimeField,
        [CompareWindowMinutes] = @CompareWindowMinutes,
        [CompareWindowUnit] = @CompareWindowUnit,
        [CompareWindowOffset] = @CompareWindowOffset,
        [CompareWindowToDate] = @CompareWindowToDate,
        [CompareMultiplier] = @CompareMultiplier,
        [PendingState] = NULL,
        [PendingCount] = 0,
        [NextCheckAt] = @UpdatedAt,
        [Version] = [Version] + 1,
        [UpdatedAt] = @UpdatedAt
    WHERE [Id] = @Id
      AND (@ExpectedVersion IS NULL OR [Version] = @ExpectedVersion);

    IF @@ROWCOUNT = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM [dbo].[MonitoringRules] WHERE [Id] = @Id)
            THROW 50409, N'Правило змінено іншим користувачем після того, як ви його відкрили. Оновіть сторінку й повторіть зміни', 1;

        THROW 50404, N'Правило не знайдене', 1;
    END
END
