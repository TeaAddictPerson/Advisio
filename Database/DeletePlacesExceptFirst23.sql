-- Удаление всех мест кроме первых 23
USE [AdvisioDb];
GO

-- Получаем ID первых 23 мест
DECLARE @First23Ids TABLE (Id INT);
INSERT INTO @First23Ids
SELECT TOP 23 Id FROM Places ORDER BY Id;

-- Удаляем связи с категориями
DELETE FROM PlaceCategories
WHERE PlaceId NOT IN (SELECT Id FROM @First23Ids);

-- Удаляем связи с избранным
DELETE FROM Favorites
WHERE PlaceId NOT IN (SELECT Id FROM @First23Ids);

-- Удаляем связи с лайками
DELETE FROM Likes
WHERE PlaceId NOT IN (SELECT Id FROM @First23Ids);

-- Удаляем рекомендации
DELETE FROM Recommendations
WHERE PlaceId NOT IN (SELECT Id FROM @First23Ids);

-- Удаляем места
DELETE FROM Places
WHERE Id NOT IN (SELECT Id FROM @First23Ids);

PRINT 'Удалены все места кроме первых 23';
GO

