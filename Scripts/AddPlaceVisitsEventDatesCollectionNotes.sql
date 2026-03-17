-- Миграция: PlaceVisit, EventStartDate/EventEndDate у Place, Note/NotePhotoUrls/IsNotePublic у PlaceCollectionItem

-- 1. Place: EventStartDate, EventEndDate
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Places') AND name = 'EventStartDate')
BEGIN
    ALTER TABLE [Places] ADD [EventStartDate] datetime2 NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Places') AND name = 'EventEndDate')
BEGIN
    ALTER TABLE [Places] ADD [EventEndDate] datetime2 NULL;
END
GO

-- 2. PlaceCollectionItem: Note, NotePhotoUrls, IsNotePublic
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlaceCollectionItems') AND name = 'Note')
BEGIN
    ALTER TABLE [PlaceCollectionItems] ADD [Note] nvarchar(max) NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlaceCollectionItems') AND name = 'NotePhotoUrls')
BEGIN
    ALTER TABLE [PlaceCollectionItems] ADD [NotePhotoUrls] nvarchar(max) NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlaceCollectionItems') AND name = 'IsNotePublic')
BEGIN
    ALTER TABLE [PlaceCollectionItems] ADD [IsNotePublic] bit NOT NULL DEFAULT 0;
END
GO

-- 3. PlaceVisits
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlaceVisits')
BEGIN
    CREATE TABLE [PlaceVisits] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [PlaceId] int NOT NULL,
        [VisitedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PlaceVisits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlaceVisits_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlaceVisits_Places_PlaceId] FOREIGN KEY ([PlaceId]) REFERENCES [Places] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_PlaceVisits_UserId_PlaceId] ON [PlaceVisits] ([UserId], [PlaceId]);
    CREATE INDEX [IX_PlaceVisits_PlaceId] ON [PlaceVisits] ([PlaceId]);
END
GO
