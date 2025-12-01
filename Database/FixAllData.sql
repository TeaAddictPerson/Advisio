-- Исправление всех данных с правильной кодировкой Unicode

-- Обновляем все места
UPDATE Places SET 
    Name = N'Парк Горького', 
    Description = N'Центральный парк культуры и отдыха с аттракционами, кафе и зонами для пикников', 
    Address = N'пр-т Независимости, 1' 
WHERE Id = 1;

UPDATE Places SET 
    Name = N'Национальный художественный музей', 
    Description = N'Крупнейший музей Беларуси с коллекцией произведений искусства', 
    Address = N'ул. Ленина, 20' 
WHERE Id = 2;

UPDATE Places SET 
    Name = N'Спортивный комплекс "Минск-Арена"', 
    Description = N'Современный спортивный комплекс с ледовой ареной, бассейном и тренажерным залом', 
    Address = N'пр-т Победителей, 111' 
WHERE Id = 3;

UPDATE Places SET 
    Name = N'Центральный ботанический сад', 
    Description = N'Красивый ботанический сад с разнообразными растениями и зонами для прогулок', 
    Address = N'ул. Сурганова, 2в' 
WHERE Id = 4;

UPDATE Places SET 
    Name = N'Квест-комната "Эскейп-рум"', 
    Description = N'Увлекательные квесты для друзей и семьи с различными сценариями', 
    Address = N'ул. Немига, 5' 
WHERE Id = 5;

UPDATE Places SET 
    Name = N'Каток "Ледовый дворец"', 
    Description = N'Крытый каток для катания на коньках всей семьей', 
    Address = N'ул. Притыцкого, 27' 
WHERE Id = 6;

-- Обновляем категории
UPDATE Categories SET Name = N'С друзьями', Description = N'Места для отдыха с друзьями' WHERE Type = 0 AND (SELECT COUNT(*) FROM Categories WHERE Type = 0 AND Id <= Id) = 1;
UPDATE Categories SET Name = N'С ребенком', Description = N'Места для отдыха с детьми' WHERE Type = 0 AND (SELECT COUNT(*) FROM Categories WHERE Type = 0 AND Id <= Id) = 2;
UPDATE Categories SET Name = N'С семьей', Description = N'Места для семейного отдыха' WHERE Type = 0 AND (SELECT COUNT(*) FROM Categories WHERE Type = 0 AND Id <= Id) = 3;
UPDATE Categories SET Name = N'Активный отдых', Description = N'Активные виды отдыха: спорт, экстремальные развлечения' WHERE Type = 1 AND (SELECT COUNT(*) FROM Categories WHERE Type = 1 AND Id <= Id) = 1;
UPDATE Categories SET Name = N'На природе', Description = N'Отдых на природе: парки, леса, водоемы' WHERE Type = 1 AND (SELECT COUNT(*) FROM Categories WHERE Type = 1 AND Id <= Id) = 2;
UPDATE Categories SET Name = N'Спокойный отдых', Description = N'Спокойные виды отдыха: музеи, театры, кафе' WHERE Type = 1 AND (SELECT COUNT(*) FROM Categories WHERE Type = 1 AND Id <= Id) = 3;

PRINT 'Данные обновлены!';

