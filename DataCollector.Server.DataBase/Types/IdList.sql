-- Набір ідентифікаторів (пошук кількох записів одним запитом).
CREATE TYPE [dbo].[IdList]
AS TABLE (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY
);
