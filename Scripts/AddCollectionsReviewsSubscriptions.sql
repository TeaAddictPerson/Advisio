-- Скрипт создания таблиц для коллекций, отзывов, подписок и статистики
-- Выполните в SQL Server Management Studio или: sqlcmd -S DESKTOP-2F9M1ET\SQLEXPRESS -d AdvisioDb -i AddCollectionsReviewsSubscriptions.sql

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlaceCollections')
BEGIN
    CREATE TABLE [PlaceCollections] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PlaceCollections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlaceCollections_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_PlaceCollections_UserId] ON [PlaceCollections] ([UserId]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlaceViews')
BEGIN
    CREATE TABLE [PlaceViews] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [PlaceId] int NOT NULL,
        [ViewedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PlaceViews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlaceViews_Places_PlaceId] FOREIGN KEY ([PlaceId]) REFERENCES [Places] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlaceViews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_PlaceViews_PlaceId] ON [PlaceViews] ([PlaceId]);
    CREATE INDEX [IX_PlaceViews_UserId] ON [PlaceViews] ([UserId]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlaceReviews')
BEGIN
    CREATE TABLE [PlaceReviews] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [PlaceId] int NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PlaceReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlaceReviews_Places_PlaceId] FOREIGN KEY ([PlaceId]) REFERENCES [Places] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlaceReviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_PlaceReviews_UserId_PlaceId] ON [PlaceReviews] ([UserId], [PlaceId]);
    CREATE INDEX [IX_PlaceReviews_PlaceId] ON [PlaceReviews] ([PlaceId]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlaceSubscriptions')
BEGIN
    CREATE TABLE [PlaceSubscriptions] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [PlaceId] int NOT NULL,
        [SubscribedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PlaceSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlaceSubscriptions_Places_PlaceId] FOREIGN KEY ([PlaceId]) REFERENCES [Places] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlaceSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_PlaceSubscriptions_UserId_PlaceId] ON [PlaceSubscriptions] ([UserId], [PlaceId]);
    CREATE INDEX [IX_PlaceSubscriptions_PlaceId] ON [PlaceSubscriptions] ([PlaceId]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CategorySubscriptions')
BEGIN
    CREATE TABLE [CategorySubscriptions] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [SubscribedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CategorySubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CategorySubscriptions_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CategorySubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_CategorySubscriptions_UserId_CategoryId] ON [CategorySubscriptions] ([UserId], [CategoryId]);
    CREATE INDEX [IX_CategorySubscriptions_CategoryId] ON [CategorySubscriptions] ([CategoryId]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserFilterUsages')
BEGIN
    CREATE TABLE [UserFilterUsages] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [FilterType] nvarchar(max) NOT NULL,
        [FilterValue] nvarchar(max) NOT NULL,
        [UsedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserFilterUsages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserFilterUsages_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_UserFilterUsages_UserId] ON [UserFilterUsages] ([UserId]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlaceCollectionItems')
BEGIN
    CREATE TABLE [PlaceCollectionItems] (
        [Id] int NOT NULL IDENTITY(1,1),
        [CollectionId] int NOT NULL,
        [PlaceId] int NOT NULL,
        [SortOrder] int NOT NULL,
        [AddedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PlaceCollectionItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlaceCollectionItems_PlaceCollections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [PlaceCollections] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlaceCollectionItems_Places_PlaceId] FOREIGN KEY ([PlaceId]) REFERENCES [Places] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_PlaceCollectionItems_CollectionId] ON [PlaceCollectionItems] ([CollectionId]);
    CREATE INDEX [IX_PlaceCollectionItems_PlaceId] ON [PlaceCollectionItems] ([PlaceId]);
END
GO

-- Записать миграцию в журнал EF (чтобы dotnet ef database update не пытался применить её повторно)
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250317000000_AddCollectionsReviewsSubscriptions')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20250317000000_AddCollectionsReviewsSubscriptions', N'8.0.0');
END
GO
