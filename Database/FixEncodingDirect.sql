-- Прямое исправление кодировки с использованием N'' для Unicode

-- Удаляем старые данные с неправильной кодировкой
DELETE FROM PlaceCategories WHERE PlaceId IN (SELECT Id FROM Places WHERE Name LIKE '%Р%' OR Name LIKE '%�%');
DELETE FROM Places WHERE Name LIKE '%Р%' OR Name LIKE '%�%' OR Name IS NULL;

-- Вставляем данные с правильной кодировкой (используем N'' для Unicode)
INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
(N'Каток "Ледовый дворец"', N'Крытый каток для катания на коньках всей семьей', N'ул. Притыцкого, 27', NULL, 53.8833, 27.5167, GETUTCDATE());

DECLARE @Place6Id INT = SCOPE_IDENTITY();
DECLARE @CategoryActiveId INT = (SELECT Id FROM Categories WHERE Name LIKE '%Активный%' OR Type = 1 ORDER BY Id OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY);
DECLARE @CategoryFamilyId INT = (SELECT Id FROM Categories WHERE Name LIKE '%семьей%' OR Type = 0 ORDER BY Id OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY);
DECLARE @CategoryChildId INT = (SELECT Id FROM Categories WHERE Name LIKE '%ребенком%' OR Type = 0 ORDER BY Id OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY);

IF @CategoryActiveId IS NOT NULL AND @Place6Id IS NOT NULL
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES (@Place6Id, @CategoryActiveId);
IF @CategoryFamilyId IS NOT NULL AND @Place6Id IS NOT NULL
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES (@Place6Id, @CategoryFamilyId);
IF @CategoryChildId IS NOT NULL AND @Place6Id IS NOT NULL
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES (@Place6Id, @CategoryChildId);

-- Обновляем все существующие места
UPDATE Places SET Name = N'Парк Горького', Description = N'Центральный парк культуры и отдыха с аттракционами, кафе и зонами для пикников', Address = N'пр-т Независимости, 1' WHERE Id = 1;
UPDATE Places SET Name = N'Национальный художественный музей', Description = N'Крупнейший музей Беларуси с коллекцией произведений искусства', Address = N'ул. Ленина, 20' WHERE Id = 2;
UPDATE Places SET Name = N'Спортивный комплекс "Минск-Арена"', Description = N'Современный спортивный комплекс с ледовой ареной, бассейном и тренажерным залом', Address = N'пр-т Победителей, 111' WHERE Id = 3;
UPDATE Places SET Name = N'Центральный ботанический сад', Description = N'Красивый ботанический сад с разнообразными растениями и зонами для прогулок', Address = N'ул. Сурганова, 2в' WHERE Id = 4;
UPDATE Places SET Name = N'Квест-комната "Эскейп-рум"', Description = N'Увлекательные квесты для друзей и семьи с различными сценариями', Address = N'ул. Немига, 5' WHERE Id = 5;
UPDATE Places SET Name = N'Каток "Ледовый дворец"', Description = N'Крытый каток для катания на коньках всей семьей', Address = N'ул. Притыцкого, 27' WHERE Id = 6;

-- Обновляем категории
UPDATE Categories SET Name = N'С друзьями', Description = N'Места для отдыха с друзьями' WHERE Type = 0 AND Id = (SELECT MIN(Id) FROM Categories WHERE Type = 0);
UPDATE Categories SET Name = N'С ребенком', Description = N'Места для отдыха с детьми' WHERE Type = 0 AND Id = (SELECT Id FROM Categories WHERE Type = 0 ORDER BY Id OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY);
UPDATE Categories SET Name = N'С семьей', Description = N'Места для семейного отдыха' WHERE Type = 0 AND Id = (SELECT Id FROM Categories WHERE Type = 0 ORDER BY Id OFFSET 2 ROWS FETCH NEXT 1 ROWS ONLY);
UPDATE Categories SET Name = N'Активный отдых', Description = N'Активные виды отдыха: спорт, экстремальные развлечения' WHERE Type = 1 AND Id = (SELECT MIN(Id) FROM Categories WHERE Type = 1);
UPDATE Categories SET Name = N'На природе', Description = N'Отдых на природе: парки, леса, водоемы' WHERE Type = 1 AND Id = (SELECT Id FROM Categories WHERE Type = 1 ORDER BY Id OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY);
UPDATE Categories SET Name = N'Спокойный отдых', Description = N'Спокойные виды отдыха: музеи, театры, кафе' WHERE Type = 1 AND Id = (SELECT Id FROM Categories WHERE Type = 1 ORDER BY Id OFFSET 2 ROWS FETCH NEXT 1 ROWS ONLY);

PRINT 'Кодировка исправлена!';

