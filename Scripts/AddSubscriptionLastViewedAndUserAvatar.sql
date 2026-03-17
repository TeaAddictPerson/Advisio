-- 1. PlaceSubscription: LastViewedAt — когда пользователь последний раз просматривал место после подписки
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlaceSubscriptions') AND name = 'LastViewedAt')
BEGIN
    ALTER TABLE [PlaceSubscriptions] ADD [LastViewedAt] datetime2 NULL;
END
GO

-- 2. Users: AvatarUrl — URL аватарки пользователя
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'AvatarUrl')
BEGIN
    ALTER TABLE [Users] ADD [AvatarUrl] nvarchar(500) NULL;
END
GO
