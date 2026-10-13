-- Зберігає дашборд повністю: назву, опис, автооновлення і набір віджетів.
-- Віджети з @Widgets оновлюються за Id, нові додаються, відсутні в списку - видаляються.
-- Помилки: 50404 - дашборд або модель віджета не знайдені, 50409 - віджет належить іншому дашборду.
CREATE PROCEDURE [dbo].[sp_Dashboards_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(128),
    @Description NVARCHAR(512),
    @RefreshSeconds INT,
    @UpdatedAt DATETIME2,
    @Widgets [dbo].[DashboardWidgetList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Pkey INT = (SELECT [Pkey] FROM [dbo].[Dashboards] WHERE [Id] = @Id);

    IF @Pkey IS NULL
        THROW 50404, N'Дашборд не знайдений', 1;

    IF EXISTS (
        SELECT 1 FROM @Widgets w
        WHERE w.[ConfigId] IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM [dbo].[EntityConfigs] c WHERE c.[Id] = w.[ConfigId]))
        THROW 50404, N'Модель віджета не знайдена', 1;

    IF EXISTS (
        SELECT 1 FROM @Widgets w
        INNER JOIN [dbo].[DashboardWidgets] x ON x.[Id] = w.[Id]
        WHERE x.[DashboardPkey] <> @Pkey)
        THROW 50409, N'Віджет належить іншому дашборду', 1;

    BEGIN TRANSACTION;

    UPDATE [dbo].[Dashboards]
    SET [Name] = @Name,
        [Description] = @Description,
        [RefreshSeconds] = @RefreshSeconds,
        [UpdatedAt] = @UpdatedAt
    WHERE [Pkey] = @Pkey;

    DELETE FROM [dbo].[DashboardWidgets]
    WHERE [DashboardPkey] = @Pkey
      AND [Id] NOT IN (SELECT [Id] FROM @Widgets);

    UPDATE x
    SET x.[ConfigPkey] = c.[Pkey],
        x.[Type] = w.[Type],
        x.[Title] = w.[Title],
        x.[X] = w.[X],
        x.[Y] = w.[Y],
        x.[W] = w.[W],
        x.[H] = w.[H],
        x.[Settings] = w.[Settings]
    FROM [dbo].[DashboardWidgets] x
    INNER JOIN @Widgets w ON w.[Id] = x.[Id]
    LEFT JOIN [dbo].[EntityConfigs] c ON c.[Id] = w.[ConfigId];

    INSERT INTO [dbo].[DashboardWidgets] ([Id], [DashboardPkey], [ConfigPkey], [Type], [Title], [X], [Y], [W], [H], [Settings])
    SELECT w.[Id], @Pkey, c.[Pkey], w.[Type], w.[Title], w.[X], w.[Y], w.[W], w.[H], w.[Settings]
    FROM @Widgets w
    LEFT JOIN [dbo].[EntityConfigs] c ON c.[Id] = w.[ConfigId]
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[DashboardWidgets] x WHERE x.[Id] = w.[Id]);

    COMMIT TRANSACTION;
END
