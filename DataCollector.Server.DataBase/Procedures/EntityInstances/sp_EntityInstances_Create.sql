-- Створює запис разом зі значеннями полів. Порожні значення не зберігаються.
-- Значення полів, що не належать моделі, ігноруються.
-- Помилки: 50404 - модель не знайдена.
CREATE PROCEDURE [dbo].[sp_EntityInstances_Create]
    @Id UNIQUEIDENTIFIER,
    @EntityConfigId UNIQUEIDENTIFIER,
    @CreatedAt DATETIME2,
    @Values [dbo].[PropertyValueList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ConfigPkey INT = (SELECT [Pkey] FROM [dbo].[EntityConfigs] WHERE [Id] = @EntityConfigId);

    IF @ConfigPkey IS NULL
        THROW 50404, N'Модель не знайдена', 1;

    BEGIN TRANSACTION;

    INSERT INTO [dbo].[EntityInstances] ([Id], [ConfigPkey], [CreatedAt])
    VALUES (@Id, @ConfigPkey, @CreatedAt);

    DECLARE @Pkey BIGINT = SCOPE_IDENTITY();

    INSERT INTO [dbo].[EntityPropertyInstances]
        ([InstancePkey], [PropertyPkey], [ValueText], [ValueNumber], [ValueBool], [ValueDate])
    SELECT @Pkey, p.[Pkey], v.[ValueText], v.[ValueNumber], v.[ValueBool], v.[ValueDate]
    FROM @Values v
    INNER JOIN [dbo].[EntityPropertyConfigs] p
        ON p.[Id] = v.[EntityPropertyConfigId] AND p.[ConfigPkey] = @ConfigPkey
    WHERE v.[ValueText] IS NOT NULL
       OR v.[ValueNumber] IS NOT NULL
       OR v.[ValueBool] IS NOT NULL
       OR v.[ValueDate] IS NOT NULL;

    COMMIT TRANSACTION;
END
