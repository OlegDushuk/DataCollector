CREATE TABLE [dbo].[DataPropertyConfigs]
(
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    DataConfigId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATE NOT NULL

        CONSTRAINT FK_DataPropertyConfigs_DataConfigs
            FOREIGN KEY (DataConfigId)
                REFERENCES DataConfigs (Id)
)