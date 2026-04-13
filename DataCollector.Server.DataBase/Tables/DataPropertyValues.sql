CREATE TABLE [dbo].[DataPropertyValues]
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    DataValueId UNIQUEIDENTIFIER NOT NULL,
    DataPropertyConfigId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATE NOT NULL

        CONSTRAINT FK_DataProperties_DataValues
            FOREIGN KEY (DataValueId)
                REFERENCES DataValues (Id),

    CONSTRAINT FK_DataProperties_DataPropertyConfigs
        FOREIGN KEY (DataPropertyConfigId)
            REFERENCES DataPropertyConfigs (Id)
)