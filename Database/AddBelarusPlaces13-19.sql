-- Скрипт для добавления мест Беларуси (с 13 по 19)

-- Получаем ID категорий
DECLARE @CategoryActiveId INT = (SELECT Id FROM Categories WHERE Name = N'Активный отдых' AND Type = 1);
DECLARE @CategoryCalmId INT = (SELECT Id FROM Categories WHERE Name = N'Спокойный отдых' AND Type = 1);
DECLARE @CategoryNatureId INT = (SELECT Id FROM Categories WHERE Name = N'На природе' AND Type = 1);
DECLARE @CategoryFriendsId INT = (SELECT Id FROM Categories WHERE Name = N'С друзьями' AND Type = 0);
DECLARE @CategoryFamilyId INT = (SELECT Id FROM Categories WHERE Name = N'С семьей' AND Type = 0);
DECLARE @CategoryChildId INT = (SELECT Id FROM Categories WHERE Name = N'С ребенком' AND Type = 0);

-- Место 13: Мемориальный комплекс "Буйничское поле"
IF NOT EXISTS (SELECT 1 FROM Places WHERE LOWER(Name) = LOWER(N'Мемориальный комплекс "Буйничское поле"'))
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    (N'Мемориальный комплекс "Буйничское поле"', 
     N'Место ожесточенных боев 1941 года. Включает мемориал, часовню, озеро и музей военной техники. Часы работы территории: круглосуточно. Экспозиция техники под открытым небом доступна всегда.', 
     N'Могилевская обл., д. Буйничи', NULL, 53.8581, 30.2494, GETUTCDATE());
    
    DECLARE @Place13Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place13Id, @CategoryCalmId),
    (@Place13Id, @CategoryFamilyId);
END

-- Место 14: Гомельский дворцово-парковый ансамбль
IF NOT EXISTS (SELECT 1 FROM Places WHERE LOWER(Name) = LOWER(N'Гомельский дворцово-парковый ансамбль'))
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    (N'Гомельский дворцово-парковый ансамбль', 
     N'Включает дворец Румянцевых-Паскевичей, часовню-усыпальницу, зимний сад и огромный парк. Часы работы дворца (музея): вторник-воскресенье: 10:00–18:00 (касса до 17:30). Понедельник — выходной. Парк открыт для посещения с утра до вечера.', 
     N'г. Гомель, пл. Ленина, 4', NULL, 52.4238, 31.0158, GETUTCDATE());
    
    DECLARE @Place14Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place14Id, @CategoryCalmId),
    (@Place14Id, @CategoryNatureId),
    (@Place14Id, @CategoryFamilyId);
END

-- Место 15: Национальный парк "Припятский" (Музей природы в Лясковичах)
IF NOT EXISTS (SELECT 1 FROM Places WHERE LOWER(Name) = LOWER(N'Национальный парк "Припятский" (Музей природы в Лясковичах)'))
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    (N'Национальный парк "Припятский" (Музей природы в Лясковичах)', 
     N'Знаменитые белорусские джунгли. Для посещения доступны современный Музей природы и "Парк древних обитателей". Часы работы музея: ежедневно, 9:00–18:00, иногда без выходных в летний период.', 
     N'Гомельская обл., Петриковский р-н, г.п. Лясковичи', NULL, 52.2160, 28.0109, GETUTCDATE());
    
    DECLARE @Place15Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place15Id, @CategoryNatureId),
    (@Place15Id, @CategoryCalmId),
    (@Place15Id, @CategoryFamilyId);
END

-- Место 16: Дудутки (Музейный комплекс старинных ремесел)
IF NOT EXISTS (SELECT 1 FROM Places WHERE LOWER(Name) = LOWER(N'Дудутки (Музейный комплекс старинных ремесел)'))
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    (N'Дудутки (Музейный комплекс старинных ремесел)', 
     N'Этнографический музей под открытым небом. Работа мельницы, гончара, кузнеца, дегустации. Часы работы: среда-воскресенье: 10:00–18:00 (касса до 17:00). Понедельник, вторник — выходной. В летний период часто работают без выходных.', 
     N'Минская обл., Пуховичский р-н, д. Птичь', NULL, 53.5960, 27.6845, GETUTCDATE());
    
    DECLARE @Place16Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place16Id, @CategoryCalmId),
    (@Place16Id, @CategoryNatureId),
    (@Place16Id, @CategoryFamilyId),
    (@Place16Id, @CategoryChildId);
END

-- Место 17: Историко-культурный комплекс "Линия Сталина"
IF NOT EXISTS (SELECT 1 FROM Places WHERE LOWER(Name) = LOWER(N'Историко-культурный комплекс "Линия Сталина"'))
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    (N'Историко-культурный комплекс "Линия Сталина"', 
     N'Крупнейший фортификационный музей под открытым небом на месте реальных укреплений. Множество техники, интерактивные площадки. Часы работы: ежедневно, 10:00–18:00. Экскурсии на бронетехнике и стрельба из оружия — по расписанию.', 
     N'Минская обл., Минский р-н, вблизи д. Лошаны', NULL, 54.0627, 27.2923, GETUTCDATE());
    
    DECLARE @Place17Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place17Id, @CategoryCalmId),
    (@Place17Id, @CategoryFamilyId),
    (@Place17Id, @CategoryChildId);
END

-- Место 18: Несвижский дворцово-парковый ансамбль
IF NOT EXISTS (SELECT 1 FROM Places WHERE LOWER(Name) = LOWER(N'Несвижский дворцово-парковый ансамбль'))
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    (N'Несвижский дворцово-парковый ансамбль', 
     N'Резиденция князей Радзивиллов, объект Всемирного наследия ЮНЕСКО. Величественный дворец-замок с парком. Часы работы дворца (музея): вторник-воскресенье: 10:00–19:00 (касса до 18:00). Понедельник — выходной. Парк открыт дольше.', 
     N'Минская обл., г. Несвиж, ул. Дворцовая, 2', NULL, 53.2226, 26.6919, GETUTCDATE());
    
    DECLARE @Place18Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place18Id, @CategoryCalmId),
    (@Place18Id, @CategoryNatureId),
    (@Place18Id, @CategoryFamilyId);
END

-- Место 19: Национальный парк "Нарочанский" (Аквапарк "Нарочанский")
IF NOT EXISTS (SELECT 1 FROM Places WHERE LOWER(Name) = LOWER(N'Национальный парк "Нарочанский" (Аквапарк "Нарочанский")'))
BEGIN
    INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
    (N'Национальный парк "Нарочанский" (Аквапарк "Нарочанский")', 
     N'Озерный край Беларуси. Крупный аквапарк с бассейнами, горками и спа-зоной. Часы работы аквапарка: обычно с 10:00 до 22:00, сеансы по 2-3 часа. Требуется обязательная предварительная проверка расписания и бронирование.', 
     N'Минская обл., Мядельский р-н, к.п. Нарочь, ул. Зеленая, 15', NULL, 54.9081, 26.6971, GETUTCDATE());
    
    DECLARE @Place19Id INT = SCOPE_IDENTITY();
    INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
    (@Place19Id, @CategoryActiveId),
    (@Place19Id, @CategoryNatureId),
    (@Place19Id, @CategoryFamilyId),
    (@Place19Id, @CategoryChildId);
END

PRINT 'Места с 13 по 19 успешно добавлены!';



