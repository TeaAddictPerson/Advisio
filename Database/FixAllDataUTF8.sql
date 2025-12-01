-- Исправление всех данных с правильной кодировкой Unicode

-- Обновляем все места
UPDATE Places SET Name = N'Парк Горького', Description = N'Центральный парк культуры и отдыха с аттракционами, кафе и зонами для пикников', Address = N'пр-т Независимости, 1' WHERE Id = 1;
UPDATE Places SET Name = N'Национальный художественный музей', Description = N'Крупнейший музей Беларуси с коллекцией произведений искусства', Address = N'ул. Ленина, 20' WHERE Id = 2;
UPDATE Places SET Name = N'Спортивный комплекс "Минск-Арена"', Description = N'Современный спортивный комплекс с ледовой ареной, бассейном и тренажерным залом', Address = N'пр-т Победителей, 111' WHERE Id = 3;
UPDATE Places SET Name = N'Центральный ботанический сад', Description = N'Красивый ботанический сад с разнообразными растениями и зонами для прогулок', Address = N'ул. Сурганова, 2в' WHERE Id = 4;
UPDATE Places SET Name = N'Квест-комната "Эскейп-рум"', Description = N'Увлекательные квесты для друзей и семьи с различными сценариями', Address = N'ул. Немига, 5' WHERE Id = 5;
UPDATE Places SET Name = N'Каток "Ледовый дворец"', Description = N'Крытый каток для катания на коньках всей семьей', Address = N'ул. Притыцкого, 27' WHERE Id = 6;

-- Обновляем категории по порядку
DECLARE @Cat1 INT = (SELECT MIN(Id) FROM Categories WHERE Type = 0);
DECLARE @Cat2 INT = (SELECT Id FROM Categories WHERE Type = 0 ORDER BY Id OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY);
DECLARE @Cat3 INT = (SELECT Id FROM Categories WHERE Type = 0 ORDER BY Id OFFSET 2 ROWS FETCH NEXT 1 ROWS ONLY);
DECLARE @Cat4 INT = (SELECT MIN(Id) FROM Categories WHERE Type = 1);
DECLARE @Cat5 INT = (SELECT Id FROM Categories WHERE Type = 1 ORDER BY Id OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY);
DECLARE @Cat6 INT = (SELECT Id FROM Categories WHERE Type = 1 ORDER BY Id OFFSET 2 ROWS FETCH NEXT 1 ROWS ONLY);

IF @Cat1 IS NOT NULL UPDATE Categories SET Name = N'С друзьями', Description = N'Места для отдыха с друзьями' WHERE Id = @Cat1;
IF @Cat2 IS NOT NULL UPDATE Categories SET Name = N'С ребенком', Description = N'Места для отдыха с детьми' WHERE Id = @Cat2;
IF @Cat3 IS NOT NULL UPDATE Categories SET Name = N'С семьей', Description = N'Места для семейного отдыха' WHERE Id = @Cat3;
IF @Cat4 IS NOT NULL UPDATE Categories SET Name = N'Активный отдых', Description = N'Активные виды отдыха: спорт, экстремальные развлечения' WHERE Id = @Cat4;
IF @Cat5 IS NOT NULL UPDATE Categories SET Name = N'На природе', Description = N'Отдых на природе: парки, леса, водоемы' WHERE Id = @Cat5;
IF @Cat6 IS NOT NULL UPDATE Categories SET Name = N'Спокойный отдых', Description = N'Спокойные виды отдыха: музеи, театры, кафе' WHERE Id = @Cat6;

PRINT 'Данные обновлены!';

