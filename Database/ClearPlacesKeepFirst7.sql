-- Очистка БД, оставляем только первые 7 записей Places

-- Удаляем связи с категориями для мест, которые будут удалены
DELETE FROM PlaceCategories 
WHERE PlaceId NOT IN (
    SELECT TOP 7 Id FROM Places ORDER BY Id
);

-- Удаляем связи с избранным для мест, которые будут удалены
DELETE FROM Favorites 
WHERE PlaceId NOT IN (
    SELECT TOP 7 Id FROM Places ORDER BY Id
);

-- Удаляем связи с лайками для мест, которые будут удалены
DELETE FROM Likes 
WHERE PlaceId NOT IN (
    SELECT TOP 7 Id FROM Places ORDER BY Id
);

-- Удаляем рекомендации для мест, которые будут удалены
DELETE FROM Recommendations 
WHERE PlaceId NOT IN (
    SELECT TOP 7 Id FROM Places ORDER BY Id
);

-- Удаляем места, оставляя только первые 7
DELETE FROM Places 
WHERE Id NOT IN (
    SELECT TOP 7 Id FROM Places ORDER BY Id
);

