-- Нове визначення правила. Стан зберігається (поріг підправили - сповіщення лише якщо стан справді зміниться),
-- але незавершений перехід скидається, а перевірка відбувається одразу. Інша модель - стан починається заново.
-- Version збільшується: результат перевірки, що йшла за старим визначенням, не буде записано.
-- @ExpectedVersion - версія, яку бачив редактор (оптимістична конкурентність); NULL - без перевірки.
-- Помилки: 50404 - правило або модель не знайдені, 50409 - правило змінено після того, як його відкрили.
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
    @UpdatedAt DATETIME2,
    @ExpectedVersion INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @ConfigId);

    IF @ConfigPkey IS NULL
        THROW 50404, N'Модель не знайдена', 1;

    UPDATE [dbo].[MonitoringRules]
    SET [State] = CASE WHEN [ConfigPkey] = @ConfigPkey THEN [State] ELSE 0 END,
        [StateSince] = CASE WHEN [ConfigPkey] = @ConfigPkey THEN [StateSince] ELSE NULL END,
        [LastValue] = CASE WHEN [ConfigPkey] = @ConfigPkey THEN [LastValue] ELSE NULL END,
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
