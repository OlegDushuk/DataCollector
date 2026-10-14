-- Значення полів кількох записів (імпорт). Як і в PropertyValueList: заповнена колонка відповідає
-- типу поля, рядок без жодного значення при оновленні означає "очистити поле".
CREATE TYPE [dbo].[InstanceValueList]
AS TABLE (
    [InstanceId] UNIQUEIDENTIFIER NOT NULL,
    [EntityPropertyConfigId] UNIQUEIDENTIFIER NOT NULL,
    [ValueText] NVARCHAR(4000) NULL,
    [ValueNumber] DECIMAL(38, 10) NULL,
    [ValueBool] BIT NULL,
    [ValueDate] DATETIME2 NULL,
    PRIMARY KEY ([InstanceId], [EntityPropertyConfigId])
);
