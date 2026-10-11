-- Значення полів одного запису для збереження: заповнена колонка відповідає типу поля,
-- рядок без жодного значення означає "очистити поле".
CREATE TYPE [dbo].[PropertyValueList]
AS TABLE (
    [EntityPropertyConfigId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [ValueText] NVARCHAR(4000) NULL,
    [ValueNumber] DECIMAL(38, 10) NULL,
    [ValueBool] BIT NULL,
    [ValueDate] DATETIME2 NULL
);
