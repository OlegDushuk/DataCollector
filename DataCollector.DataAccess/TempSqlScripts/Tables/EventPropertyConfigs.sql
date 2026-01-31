CREATE TABLE EventPropertyConfigs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    EventConfigId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATE NOT NULL
    
    CONSTRAINT FK_EventPropertyConfigs_EventConfigs
    FOREIGN KEY (EventConfigId)
    REFERENCES EventConfigs (Id)
)