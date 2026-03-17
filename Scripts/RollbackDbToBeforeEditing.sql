-- Откат БД к состоянию до редактирования коллекций/заметок
-- Удаляет колонки, добавленные AddSubscriptionLastViewedAndUserAvatar.sql

-- 1. Удалить LastViewedAt из PlaceSubscriptions (если есть)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlaceSubscriptions') AND name = 'LastViewedAt')
BEGIN
    ALTER TABLE [PlaceSubscriptions] DROP COLUMN [LastViewedAt];
END
GO

-- 2. Удалить AvatarUrl из Users (если есть)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'AvatarUrl')
BEGIN
    ALTER TABLE [Users] DROP COLUMN [AvatarUrl];
END
GO

-- 3. Повторно установить пароли (на случай если БД другая или были сброшены)
-- admin@gmail.com -> admin123
-- 1227733@mtp.by -> barbariki
UPDATE Users SET PasswordHash = 'JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=' WHERE LOWER(Email) = 'admin@gmail.com';
UPDATE Users SET PasswordHash = 'MqLd3BnbuPYgC6i10QC6mghptehTxdP+pyuDEFT49oA=' WHERE LOWER(Email) = '1227733@mtp.by';
GO
