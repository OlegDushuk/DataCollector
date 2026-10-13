-- Нове правило: перша перевірка - одразу (NextCheckAt = час створення).
-- Помилки: 50404 - модель не знайдена.
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
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @ConfigId);

    IF @ConfigPkey IS NULL
        THROW 50404, N'Модель не знайдена', 1;

    INSERT INTO [dbo].[MonitoringRules]
        ([Id], [ConfigPkey], [Name], [IsEnabled], [Filters], [Search], [Function], [FieldKey], [TimeField], [WindowMinutes],
         [Operator], [Threshold], [ConsecutiveChecks], [CheckIntervalSeconds], [NextCheckAt], [CreatedAt])
    VALUES
        (@Id, @ConfigPkey, @Name, @IsEnabled, @Filters, @Search, @Function, @FieldKey, @TimeField, @WindowMinutes,
         @Operator, @Threshold, @ConsecutiveChecks, @CheckIntervalSeconds, @CreatedAt, @CreatedAt);
END
