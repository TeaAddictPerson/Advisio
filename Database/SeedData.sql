-- Скрипт для заполнения начальных данных в БД

-- Категории по типу компании
INSERT INTO Categories (Name, Description, Type, CreatedAt) VALUES
('С друзьями', 'Места для отдыха с друзьями', 0, GETUTCDATE()),
('С ребенком', 'Места для отдыха с детьми', 0, GETUTCDATE()),
('С семьей', 'Места для семейного отдыха', 0, GETUTCDATE());

-- Категории по типу активности
INSERT INTO Categories (Name, Description, Type, CreatedAt) VALUES
('Активный отдых', 'Активные виды отдыха: спорт, экстремальные развлечения', 1, GETUTCDATE()),
('На природе', 'Отдых на природе: парки, леса, водоемы', 1, GETUTCDATE()),
('Спокойный отдых', 'Спокойные виды отдыха: музеи, театры, кафе', 1, GETUTCDATE());

-- Пример создания опроса
INSERT INTO Surveys (Title, Description, IsActive, CreatedAt) VALUES
('Опрос о предпочтениях отдыха', 'Помогите нам подобрать идеальные места для вас', 1, GETUTCDATE());

-- Примеры вопросов опроса (замените SurveyId на реальный ID из таблицы Surveys)
DECLARE @SurveyId INT = (SELECT TOP 1 Id FROM Surveys ORDER BY Id DESC);

-- Вопрос 1: Какой тип отдыха вы предпочитаете?
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt) VALUES
(@SurveyId, 'Какой тип отдыха вы предпочитаете?', 1, GETUTCDATE());

DECLARE @Question1Id INT = (SELECT TOP 1 Id FROM SurveyQuestions WHERE SurveyId = @SurveyId AND [Order] = 1);

-- Ответы на вопрос 1 (веса: 10-40 активный, 50-90 спокойный)
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES
(@Question1Id, 'Активный отдых с физическими нагрузками', 20, GETUTCDATE()),
(@Question1Id, 'Умеренная активность', 30, GETUTCDATE()),
(@Question1Id, 'Спокойный отдых без активности', 70, GETUTCDATE()),
(@Question1Id, 'Пассивный отдых, расслабление', 80, GETUTCDATE());

-- Вопрос 2: С кем вы обычно отдыхаете?
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt) VALUES
(@SurveyId, 'С кем вы обычно отдыхаете?', 2, GETUTCDATE());

DECLARE @Question2Id INT = (SELECT TOP 1 Id FROM SurveyQuestions WHERE SurveyId = @SurveyId AND [Order] = 2);

-- Ответы на вопрос 2
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES
(@Question2Id, 'С друзьями', 40, GETUTCDATE()),
(@Question2Id, 'С семьей', 60, GETUTCDATE()),
(@Question2Id, 'С детьми', 50, GETUTCDATE()),
(@Question2Id, 'Один/одна', 55, GETUTCDATE());

-- Вопрос 3: Где вы предпочитаете проводить время?
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt) VALUES
(@SurveyId, 'Где вы предпочитаете проводить время?', 3, GETUTCDATE());

DECLARE @Question3Id INT = (SELECT TOP 1 Id FROM SurveyQuestions WHERE SurveyId = @SurveyId AND [Order] = 3);

-- Ответы на вопрос 3
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES
(@Question3Id, 'На природе, на свежем воздухе', 25, GETUTCDATE()),
(@Question3Id, 'В помещении', 75, GETUTCDATE()),
(@Question3Id, 'В городе, в общественных местах', 50, GETUTCDATE()),
(@Question3Id, 'Вдали от города', 35, GETUTCDATE());

-- Пример создания тестового пользователя
INSERT INTO Users (Username, Email, CreatedAt) VALUES
('testuser', 'test@example.com', GETUTCDATE());

-- Пример создания тестового места
DECLARE @CategoryActiveId INT = (SELECT Id FROM Categories WHERE Name = 'Активный отдых' AND Type = 1);
DECLARE @CategoryFriendsId INT = (SELECT Id FROM Categories WHERE Name = 'С друзьями' AND Type = 0);

INSERT INTO Places (Name, Description, Address, ImageUrl, Latitude, Longitude, CreatedAt) VALUES
('Спортивный комплекс "Актив"', 'Современный спортивный комплекс с бассейном, тренажерным залом и площадками для игр', 
 'ул. Спортивная, 10', 'https://example.com/images/sport.jpg', 55.7558, 37.6173, GETUTCDATE());

DECLARE @PlaceId INT = SCOPE_IDENTITY();

-- Связываем место с категориями
INSERT INTO PlaceCategories (PlaceId, CategoryId) VALUES
(@PlaceId, @CategoryActiveId),
(@PlaceId, @CategoryFriendsId);

