-- Исправление кодировки данных в БД

-- Обновление мест
UPDATE Places SET Name = N'Каток "Ледовый дворец"', Description = N'Крытый каток для катания на коньках всей семьей' WHERE Id = 6;
UPDATE Places SET Name = N'Парк Горького', Description = N'Центральный парк культуры и отдыха с аттракционами, кафе и зонами для пикников' WHERE Name LIKE '%Горького%' OR Name LIKE '%Park%';
UPDATE Places SET Name = N'Национальный художественный музей', Description = N'Крупнейший музей Беларуси с коллекцией произведений искусства' WHERE Name LIKE '%музей%' OR Name LIKE '%museum%';
UPDATE Places SET Name = N'Спортивный комплекс "Минск-Арена"', Description = N'Современный спортивный комплекс с ледовой ареной, бассейном и тренажерным залом' WHERE Name LIKE '%Минск%' OR Name LIKE '%Arena%';
UPDATE Places SET Name = N'Центральный ботанический сад', Description = N'Красивый ботанический сад с разнообразными растениями и зонами для прогулок' WHERE Name LIKE '%ботанический%' OR Name LIKE '%botanical%';
UPDATE Places SET Name = N'Квест-комната "Эскейп-рум"', Description = N'Увлекательные квесты для друзей и семьи с различными сценариями' WHERE Name LIKE '%квест%' OR Name LIKE '%quest%';
UPDATE Places SET Name = N'Спортивный комплекс "Актив"', Description = N'Современный спортивный комплекс с бассейном, тренажерным залом и площадками для игр' WHERE Name LIKE '%Актив%' OR Name LIKE '%Active%';

-- Обновление категорий
UPDATE Categories SET Name = N'С друзьями', Description = N'Места для отдыха с друзьями' WHERE Type = 0 AND (Name LIKE '%друзьями%' OR Name LIKE '%friends%');
UPDATE Categories SET Name = N'С ребенком', Description = N'Места для отдыха с детьми' WHERE Type = 0 AND (Name LIKE '%ребенком%' OR Name LIKE '%child%');
UPDATE Categories SET Name = N'С семьей', Description = N'Места для семейного отдыха' WHERE Type = 0 AND (Name LIKE '%семьей%' OR Name LIKE '%family%');
UPDATE Categories SET Name = N'Активный отдых', Description = N'Активные виды отдыха: спорт, экстремальные развлечения' WHERE Type = 1 AND (Name LIKE '%Активный%' OR Name LIKE '%Active%');
UPDATE Categories SET Name = N'На природе', Description = N'Отдых на природе: парки, леса, водоемы' WHERE Type = 1 AND (Name LIKE '%природе%' OR Name LIKE '%nature%');
UPDATE Categories SET Name = N'Спокойный отдых', Description = N'Спокойные виды отдыха: музеи, театры, кафе' WHERE Type = 1 AND (Name LIKE '%Спокойный%' OR Name LIKE '%Calm%');

PRINT 'Кодировка исправлена!';
