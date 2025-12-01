-- Скрипт для добавления тестовых мест отдыха

-- Получаем ID категорий
DECLARE @CategoryActiveId INT = (SELECT Id FROM Categories WHERE Name = 'Активный отдых' AND Type = 1);
DECLARE @CategoryCalmId INT = (SELECT Id FROM Categories WHERE Name = 'Спокойный отдых' AND Type = 1);
DECLARE @CategoryNatureId INT = (SELECT Id FROM Categories WHERE Name = 'На природе' AND Type = 1);
DECLARE @CategoryFriendsId INT = (SELECT Id FROM Categories WHERE Name = 'С друзьями' AND Type = 0);
DECLARE @CategoryFamilyId INT = (SELECT Id FROM Categories WHERE Name = 'С семьей' AND Type = 0);
DECLARE @CategoryChildId INT = (SELECT Id FROM Categories WHERE Name = 'С ребенком' AND Type = 0);

-- Место 1: Парк Горького
IF NOT EXISTS (SELECT 1 FROM Places WHERE Name = 'Парк Горького')
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    ('Парк Горького', 'Центральный парк культуры и отдыха с аттракционами, кафе и зонами для пикников', 
     'пр-т Независимости, 1', NULL, 53.9045, 27.5615, GETUTCDATE());
    
    DECLARE @Place1Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place1Id, @CategoryNatureId),
    (@Place1Id, @CategoryFamilyId),
    (@Place1Id, @CategoryChildId);
END

-- Место 2: Национальный художественный музей
IF NOT EXISTS (SELECT 1 FROM Places WHERE Name = 'Национальный художественный музей')
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    ('Национальный художественный музей', 'Крупнейший музей Беларуси с коллекцией произведений искусства', 
     'ул. Ленина, 20', NULL, 53.8972, 27.5556, GETUTCDATE());
    
    DECLARE @Place2Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place2Id, @CategoryCalmId),
    (@Place2Id, @CategoryFamilyId);
END

-- Место 3: Спортивный комплекс "Минск-Арена"
IF NOT EXISTS (SELECT 1 FROM Places WHERE Name = 'Спортивный комплекс "Минск-Арена"')
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    ('Спортивный комплекс "Минск-Арена"', 'Современный спортивный комплекс с ледовой ареной, бассейном и тренажерным залом', 
     'пр-т Победителей, 111', NULL, 53.9333, 27.4833, GETUTCDATE());
    
    DECLARE @Place3Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place3Id, @CategoryActiveId),
    (@Place3Id, @CategoryFriendsId);
END

-- Место 4: Ботанический сад
IF NOT EXISTS (SELECT 1 FROM Places WHERE Name = 'Центральный ботанический сад')
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    ('Центральный ботанический сад', 'Красивый ботанический сад с разнообразными растениями и зонами для прогулок', 
     'ул. Сурганова, 2в', NULL, 53.9167, 27.5833, GETUTCDATE());
    
    DECLARE @Place4Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place4Id, @CategoryNatureId),
    (@Place4Id, @CategoryFamilyId),
    (@Place4Id, @CategoryCalmId);
END

-- Место 5: Квест-комната "Эскейп-рум"
IF NOT EXISTS (SELECT 1 FROM Places WHERE Name = 'Квест-комната "Эскейп-рум"')
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    ('Квест-комната "Эскейп-рум"', 'Увлекательные квесты для друзей и семьи с различными сценариями', 
     'ул. Немига, 5', NULL, 53.9000, 27.5500, GETUTCDATE());
    
    DECLARE @Place5Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place5Id, @CategoryFriendsId),
    (@Place5Id, @CategoryFamilyId);
END

-- Место 6: Каток "Ледовый дворец"
IF NOT EXISTS (SELECT 1 FROM Places WHERE Name = 'Каток "Ледовый дворец"')
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    ('Каток "Ледовый дворец"', 'Крытый каток для катания на коньках всей семьей', 
     'ул. Притыцкого, 27', NULL, 53.8833, 27.5167, GETUTCDATE());
    
    DECLARE @Place6Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place6Id, @CategoryActiveId),
    (@Place6Id, @CategoryFamilyId),
    (@Place6Id, @CategoryChildId);
END

PRINT 'Тестовые места успешно добавлены!';

