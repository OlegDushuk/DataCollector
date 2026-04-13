CREATE TABLE [dbo].[DataValues]
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    DataConfigId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATE NOT NULL

        CONSTRAINT FK_DataValues_DataConfigs
            FOREIGN KEY (DataConfigId)
                REFERENCES DataConfigs (Id)
)