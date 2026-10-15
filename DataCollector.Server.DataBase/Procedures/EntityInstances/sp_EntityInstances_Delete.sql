-- @ExpectedVersion - версія, яку бачив клієнт: запис, змінений відтоді, не видаляється. NULL - без перевірки.
-- Помилки: 50404 - запис не знайдений, 50409 - запис змінено після того, як його прочитали.
CREATE PROCEDURE [dbo].[sp_EntityInstances_Delete]
    @Id UNIQUEIDENTIFIER,
    @ExpectedVersion BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Pkey BIGINT = (SELECT [Pkey] FROM [dbo].[EntityInstances] WHERE [Id] = @Id);

    IF @Pkey IS NULL
        THROW 50404, N'Запис не знайдений', 1;

    BEGIN TRANSACTION;

    -- Блокування рядка до кінця транзакції: між перевіркою версії і видаленням запис ніхто не змінить.
    IF @ExpectedVersion IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM [dbo].[EntityInstances] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Pkey] = @Pkey AND [RowVersion] = CAST(@ExpectedVersion AS BINARY(8)))
        THROW 50409, N'Запис змінено іншим користувачем після того, як ви його відкрили. Оновіть дані й повторіть дію', 1;

    DELETE FROM [dbo].[EntityPropertyInstances] WHERE [InstancePkey] = @Pkey;
    DELETE FROM [dbo].[EntityInstances] WHERE [Pkey] = @Pkey;

    COMMIT TRANSACTION;
END
