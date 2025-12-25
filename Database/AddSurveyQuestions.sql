-- Удаляем существующие данные опроса
DELETE FROM UserSurveyResponses;
DELETE FROM SurveyAnswers;
DELETE FROM SurveyQuestions;
DELETE FROM Surveys;

-- Создаем новый опрос
INSERT INTO Surveys (Title, Description, IsActive, CreatedAt)
VALUES ('Опрос о предпочтениях отдыха', 'Помогите нам узнать ваши предпочтения', 1, GETUTCDATE());

DECLARE @SurveyId INT = SCOPE_IDENTITY();

-- Вопрос 1
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Ваши друзья зовут вас провести субботний день. Что вам ближе?', 1, GETUTCDATE());
DECLARE @Q1 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q1, 'Устроить пикник с играми в парке или за городом.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q1, 'Сходить в новый модный ресторан или бар.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q1, 'Собраться у кого-то дома для спокойных разговоров и настолок.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q1, 'Отправиться в поход или на спортивную площадку.', 0, GETUTCDATE());

-- Вопрос 2
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Чем вы чаще всего занимаетесь в выходной день с ребенком?', 2, GETUTCDATE());
DECLARE @Q2 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q2, 'Идем в зоопарк, парк аттракционов или на детскую площадку.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q2, 'Посещаем музей, научный центр или мастер-класс.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q2, 'Гуляем в лесопарке, кормим уток, собираем листья для гербария.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q2, 'Идем в кафе или кино, чтобы просто сменить обстановку.', 0, GETUTCDATE());

-- Вопрос 3
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Какую книгу (или фильм) об отдыхе вы скорее выберете?', 3, GETUTCDATE());
DECLARE @Q3 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q3, '"Захватывающее приключение в горах".', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q3, '"Тайны старинного особняка" или исторический роман.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q3, '"Искусство вкусной жизни" или кулинарный гид.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q3, '"Душевные вечера в кругу близких".', 0, GETUTCDATE());

-- Вопрос 4
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Вам подарили сертификат на развлечение. Что выберете?', 4, GETUTCDATE());
DECLARE @Q4 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q4, 'Картинг, лазертаг или веревочный парк.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q4, 'Ужин в ресторане с шеф-меню.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q4, 'Билеты на концерт любимой группы или в театр.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q4, 'Сертификат в спа или на мастер-класс по йоге.', 0, GETUTCDATE());

-- Вопрос 5
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Планируете отпуск на природе. Какой вариант идеален?', 5, GETUTCDATE());
DECLARE @Q5 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q5, 'Поход с палатками, костром и сплавами по реке.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q5, 'Живописные прогулки, фотоохота и пикники.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q5, 'Турбаза с анимацией, командными играми и турнирами.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q5, 'Уединенный домик у озера с книгой и удочкой.', 0, GETUTCDATE());

-- Вопрос 6
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Какой формат обучения или досуга вам интересен?', 6, GETUTCDATE());
DECLARE @Q6 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q6, 'Интерактивная выставка, где все можно трогать.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q6, 'Лекция известного эксперта или писателя.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q6, 'Воркшоп, где вы создаете что-то своими руками (гончарка, живопись).', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q6, 'Никакой, в свободное время я учусь только тому, что напрямую связано с хобби.', 0, GETUTCDATE());

-- Вопрос 7
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Ваша вторая половинка предлагает провести вечер. Ваша идея?', 7, GETUTCDATE());
DECLARE @Q7 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q7, 'Романтический ужин в уютном ресторане.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q7, 'Прогулка по вечернему городу, возможно, с посещением смотровой площадки.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q7, 'Танцевальный вечер или концерт джазовой музыки.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q7, 'Совместная пробежка или поездка на велосипедах.', 0, GETUTCDATE());

-- Вопрос 8
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'В городе проходит фестиваль. Что вас привлечет больше?', 8, GETUTCDATE());
DECLARE @Q8 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q8, 'Еда и напитки со всего мира (фуд-фест).', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q8, 'Музыкальный open-air с несколькими сценами.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q8, 'Историческая реконструкция или ярмарка народных промыслов.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q8, 'Спортивные показательные выступления и мастер-классы.', 0, GETUTCDATE());

-- Вопрос 9
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Чем вас может заинтересовать местный парк?', 9, GETUTCDATE());
DECLARE @Q9 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q9, 'Прокат велосипедов, роликов или лодок.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q9, 'Красивые аллеи для неспешных прогулок и лавочки для чтения.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q9, 'Проводимые там события: концерты, выставки под открытым небом.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q9, 'Детские и спортивные площадки для семейного времяпрепровождения.', 0, GETUTCDATE());

-- Вопрос 10
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Как вы относитесь к соревновательным активностям?', 10, GETUTCDATE());
DECLARE @Q10 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q10, 'Обожаю! Квизы, спортивные турниры, конкурсы — это драйв.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q10, 'Участвую только в несерьезных, веселых командных играх.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q10, 'Предпочитаю быть зрителем на крупных спортивных соревнованиях.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q10, 'Не люблю соревнований, это лишний стресс.', 0, GETUTCDATE());

-- Вопрос 11
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Что для вас идеальный семейный ужин вне дома?', 11, GETUTCDATE());
DECLARE @Q11 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q11, 'Шведский стол в большом семейном кафе, где учтены вкусы всех.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q11, 'Тематический ресторан с интересной программой для детей.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q11, 'Пикник на природе, который мы подготовили вместе.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q11, 'Домашняя еда, потому что в кафе дети быстро устают.', 0, GETUTCDATE());

-- Вопрос 12
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Как вы восстанавливаете силы после тяжелой недели?', 12, GETUTCDATE());
DECLARE @Q12 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q12, 'Нужна активная смена деятельности: танцы, плавание, боулинг.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q12, 'Лучшее лекарство — вылазка на природу, лес, реку.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q12, 'Глубокий релакс: баня, массаж, медитация.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q12, 'Встреча с самыми близкими друзьями в нешумном месте.', 0, GETUTCDATE());

-- Вопрос 13
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Выбирая мероприятие на вечер, на что обратите внимание в первую очередь?', 13, GETUTCDATE());
DECLARE @Q13 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q13, 'Будет ли там живая музыка или интересная программа?', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q13, 'Есть ли возможность поесть/выпить что-то вкусное?', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q13, 'Новизна и возможность узнать/увидеть что-то необычное.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q13, 'Насколько там будет шумно и многолюдно.', 0, GETUTCDATE());

-- Вопрос 14
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'В дождливый день с ребенком вы скорее всего:', 14, GETUTCDATE());
DECLARE @Q14 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q14, 'Отправитесь в аквапарк, батутный центр или веревочный парк.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q14, 'Пойдете в планетарий, музей или на детский спектакль.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q14, 'Останетесь дома играть в настольные игры, печь печенье.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q14, 'Наденете дождевики и резиновые сапоги — гулять по лужам!', 0, GETUTCDATE());

-- Вопрос 15
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Как часто вы ходите в музеи, галереи или на выставки?', 15, GETUTCDATE());
DECLARE @Q15 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q15, 'Регулярно, слежу за афишей.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q15, 'Только на самые громкие или тематические, которые мне интересны.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q15, 'В основном, когда приезжают гости или это часть другого события.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q15, 'Практически никогда, не мой формат.', 0, GETUTCDATE());

-- Вопрос 16
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Что для вас вечер в театре?', 16, GETUTCDATE());
DECLARE @Q16 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q16, 'Праздник души, люблю ходить часто и на разное.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q16, 'Специальное, торжественное событие, к которому готовятся.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q16, 'Интересная альтернатива кино, если хорошая постановка.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q16, 'Часто скучно, предпочитаю более динамичные развлечения.', 0, GETUTCDATE());

-- Вопрос 17
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Вам предложили бесплатно провести экскурсию для группы. О чем она будет?', 17, GETUTCDATE());
DECLARE @Q17 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q17, '"Тайные бары и лучший кофе в городе".', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q17, '"Неизвестные дворики и стрит-арт".', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q17, '"Исторические анекдоты и легенды старого центра".', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q17, '"Парки и набережные: маршрут для пробежки".', 0, GETUTCDATE());

-- Вопрос 18
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Как вы предпочитаете отмечать личные праздники (ДР, повышение)?', 18, GETUTCDATE());
DECLARE @Q18 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q18, 'Большая вечеринка в клубе или на природе с музыкой.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q18, 'Праздничный ужин в кругу самых близких.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q18, 'Исполнение какого-то желания: прыжок с парашютом, гоночный трек.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q18, 'Спокойный выезд на природу или в спа-отель вдвоем или в одиночестве.', 0, GETUTCDATE());

-- Вопрос 19
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Что важнее всего в отдыхе на природе?', 19, GETUTCDATE());
DECLARE @Q19 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q19, 'Активность (рыбалка, сбор грибов, спорт).', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q19, 'Еда (шашлык, уха, пикник).', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q19, 'Общение у костра с гитарой.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q19, 'Тишина, единение с природой, отдых от города.', 0, GETUTCDATE());

-- Вопрос 20
INSERT INTO SurveyQuestions (SurveyId, QuestionText, [Order], CreatedAt)
VALUES (@SurveyId, 'Какое событие вы скорее добавите в свой календарь, увидев анонс?', 20, GETUTCDATE());
DECLARE @Q20 INT = SCOPE_IDENTITY();
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q20, 'Гастрономический уикенд или дегустация.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q20, 'Открытие новой выставки современного искусства.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q20, 'Мастер-класс по сальсе или воркаут-турнир.', 0, GETUTCDATE());
INSERT INTO SurveyAnswers (QuestionId, AnswerText, Weight, CreatedAt) VALUES (@Q20, 'Поэтический вечер или камерный спектакль в маленьком зале.', 0, GETUTCDATE());

PRINT 'Опрос успешно создан с 20 вопросами';

