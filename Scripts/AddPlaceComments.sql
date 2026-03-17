-- Таблица PlaceComments для множественных комментариев к местам
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlaceComments')
BEGIN
    CREATE TABLE [PlaceComments] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [PlaceId] int NOT NULL,
        [Comment] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PlaceComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlaceComments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlaceComments_Places_PlaceId] FOREIGN KEY ([PlaceId]) REFERENCES [Places] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_PlaceComments_PlaceId] ON [PlaceComments] ([PlaceId]);
    CREATE INDEX [IX_PlaceComments_UserId] ON [PlaceComments] ([UserId]);
END
GO
