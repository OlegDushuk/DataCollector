-- Створює дашборд разом з віджетами в одній транзакції.
-- Помилки: 50404 - модель віджета не знайдена.
CREATE PROCEDURE [dbo].[sp_Dashboards_Create]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(128),
    @Description NVARCHAR(512),
    @RefreshSeconds INT,
    @CreatedAt DATETIME2,
    @Widgets [dbo].[DashboardWidgetList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (
        SELECT 1 FROM @Widgets w
        WHERE w.[ConfigId] IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] c WHERE c.[Id] = w.[ConfigId]))
        THROW 50404, N'Модель віджета не знайдена', 1;

    BEGIN TRANSACTION;

    INSERT INTO [dbo].[Dashboards] ([Id], [Name], [Description], [RefreshSeconds], [CreatedAt])
    VALUES (@Id, @Name, @Description, @RefreshSeconds, @CreatedAt);

    DECLARE @Pkey INT = SCOPE_IDENTITY();

    INSERT INTO [dbo].[DashboardWidgets] ([Id], [DashboardPkey], [ConfigPkey], [Type], [Title], [X], [Y], [W], [H], [Settings])
    SELECT w.[Id], @Pkey, c.[Pkey], w.[Type], w.[Title], w.[X], w.[Y], w.[W], w.[H], w.[Settings]
    FROM @Widgets w
    LEFT JOIN [dbo].[EntityConfigs] c ON c.[Id] = w.[ConfigId];

    COMMIT TRANSACTION;
END
