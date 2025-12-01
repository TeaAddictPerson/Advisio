-- Исправление кодировки данных в БД (UTF-8)

-- Удаляем старые данные с неправильной кодировкой
DELETE FROM PlaceCategories;
DELETE FROM Places;
DELETE FROM Categories;

-- Вставляем категории с правильной кодировкой
INSERT INTO Categories (Name, Description, Type, CreatedAt) VALUES
(N'С друзьями', N'Места для отдыха с друзьями', 0, GETUTCDATE()),
(N'С ребенком', N'Места для отдыха с детьми', 0, GETUTCDATE()),
(N'С семьей', N'Места для семейного отдыха', 0, GETUTCDATE()),
(N'Активный отдых', N'Активные виды отдыха: спорт, экстремальные развлечения', 1, GETUTCDATE()),
(N'На природе', N'Отдых на природе: парки, леса, водоемы', 1, GETUTCDATE()),
(N'Спокойный отдых', N'Спокойные виды отдыха: музеи, театры, кафе', 1, GETUTCDATE());

-- Вставляем места с правильной кодировкой
DECLARE @CategoryActiveId INT = (SELECT Id FROM Categories WHERE Name = N'Активный отдых' AND Type = 1);
DECLARE @CategoryCalmId INT = (SELECT Id FROM Categories WHERE Name = N'Спокойный отдых' AND Type = 1);
DECLARE @CategoryNatureId INT = (SELECT Id FROM Categories WHERE Name = N'На природе' AND Type = 1);
DECLARE @CategoryFriendsId INT = (SELECT Id FROM Categories WHERE Name = N'С друзьями' AND Type = 0);
DECLARE @CategoryFamilyId INT = (SELECT Id FROM Categories WHERE Name = N'С семьей' AND Type = 0);
DECLARE @CategoryChildId INT = (SELECT Id FROM Categories WHERE Name = N'С ребенком' AND Type = 0);

-- Место 1: Парк Горького
INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
(N'Парк Горького', N'Центральный парк культуры и отдыха с аттракционами, кафе и зонами для пикников', 
 N'пр-т Независимости, 1', NULL, 53.9045, 27.5615, GETUTCDATE());
DECLARE @Place1Id INT = SCOPE_IDENTITY();
INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
(@Place1Id, @CategoryNatureId),
(@Place1Id, @CategoryFamilyId),
(@Place1Id, @CategoryChildId);

-- Место 2: Национальный художественный музей
INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
(N'Национальный художественный музей', N'Крупнейший музей Беларуси с коллекцией произведений искусства', 
 N'ул. Ленина, 20', NULL, 53.8972, 27.5556, GETUTCDATE());
DECLARE @Place2Id INT = SCOPE_IDENTITY();
INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
(@Place2Id, @CategoryCalmId),
(@Place2Id, @CategoryFamilyId);

-- Место 3: Спортивный комплекс "Минск-Арена"
INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
(N'Спортивный комплекс "Минск-Арена"', N'Современный спортивный комплекс с ледовой ареной, бассейном и тренажерным залом', 
 N'пр-т Победителей, 111', NULL, 53.9333, 27.4833, GETUTCDATE());
DECLARE @Place3Id INT = SCOPE_IDENTITY();
INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
(@Place3Id, @CategoryActiveId),
(@Place3Id, @CategoryFriendsId);

-- Место 4: Ботанический сад
INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
(N'Центральный ботанический сад', N'Красивый ботанический сад с разнообразными растениями и зонами для прогулок', 
 N'ул. Сурганова, 2в', NULL, 53.9167, 27.5833, GETUTCDATE());
DECLARE @Place4Id INT = SCOPE_IDENTITY();
INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
(@Place4Id, @CategoryNatureId),
(@Place4Id, @CategoryFamilyId),
(@Place4Id, @CategoryCalmId);

-- Место 5: Квест-комната
INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
(N'Квест-комната "Эскейп-рум"', N'Увлекательные квесты для друзей и семьи с различными сценариями', 
 N'ул. Немига, 5', NULL, 53.9000, 27.5500, GETUTCDATE());
DECLARE @Place5Id INT = SCOPE_IDENTITY();
INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
(@Place5Id, @CategoryFriendsId),
(@Place5Id, @CategoryFamilyId);

-- Место 6: Каток
INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
(N'Каток "Ледовый дворец"', N'Крытый каток для катания на коньках всей семьей', 
 N'ул. Притыцкого, 27', NULL, 53.8833, 27.5167, GETUTCDATE());
DECLARE @Place6Id INT = SCOPE_IDENTITY();
INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
(@Place6Id, @CategoryActiveId),
(@Place6Id, @CategoryFamilyId),
(@Place6Id, @CategoryChildId);

-- Место 7: Спортивный комплекс "Актив"
INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
(N'Спортивный комплекс "Актив"', N'Современный спортивный комплекс с бассейном, тренажерным залом и площадками для игр', 
 N'ул. Спортивная, 10', NULL, 55.7558, 37.6173, GETUTCDATE());
DECLARE @Place7Id INT = SCOPE_IDENTITY();
INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
(@Place7Id, @CategoryActiveId),
(@Place7Id, @CategoryFriendsId);

PRINT N'Данные успешно обновлены с правильной кодировкой UTF-8!';

