CREATE TABLE EventProperties (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL,
    EventPropertyConfigId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATE NOT NULL

    CONSTRAINT FK_EventProperties_Events
    FOREIGN KEY (EventId)
    REFERENCES Events (Id),

    CONSTRAINT FK_EventProperties_EventPropertyConfigs
    FOREIGN KEY (EventPropertyConfigId)
    REFERENCES EventPropertyConfigs (Id)
)