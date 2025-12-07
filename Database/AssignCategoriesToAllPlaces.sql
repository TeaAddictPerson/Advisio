-- SQL скрипт для присвоения категорий всем местам без категорий
-- Выполните этот скрипт в SQL Server Management Studio

USE [AdvisioDb];
GO

-- Проверяем, сколько мест без категорий
SELECT COUNT(*) AS 'Мест без категорий'
FROM Places p
WHERE NOT EXISTS (
    SELECT 1 FROM PlaceCategories pc WHERE pc.PlaceId = p.Id
);
GO

-- Получаем ID категорий
DECLARE @ActiveCategoryId INT;
DECLARE @CalmCategoryId INT;
DECLARE @NatureCategoryId INT;
DECLARE @FriendsCategoryId INT;
DECLARE @FamilyCategoryId INT;
DECLARE @ChildCategoryId INT;

SELECT @ActiveCategoryId = Id FROM Categories WHERE Name LIKE '%Активный отдых%';
SELECT @CalmCategoryId = Id FROM Categories WHERE Name LIKE '%Спокойный отдых%';
SELECT @NatureCategoryId = Id FROM Categories WHERE Name LIKE '%На природе%';
SELECT @FriendsCategoryId = Id FROM Categories WHERE Name LIKE '%С друзьями%';
SELECT @FamilyCategoryId = Id FROM Categories WHERE Name LIKE '%С семьей%';
SELECT @ChildCategoryId = Id FROM Categories WHERE Name LIKE '%С ребенком%';

PRINT 'ID категорий:';
PRINT 'Активный отдых: ' + CAST(ISNULL(@ActiveCategoryId, 0) AS VARCHAR);
PRINT 'Спокойный отдых: ' + CAST(ISNULL(@CalmCategoryId, 0) AS VARCHAR);
PRINT 'На природе: ' + CAST(ISNULL(@NatureCategoryId, 0) AS VARCHAR);
PRINT 'С друзьями: ' + CAST(ISNULL(@FriendsCategoryId, 0) AS VARCHAR);
PRINT 'С семьей: ' + CAST(ISNULL(@FamilyCategoryId, 0) AS VARCHAR);
PRINT 'С ребенком: ' + CAST(ISNULL(@ChildCategoryId, 0) AS VARCHAR);
GO

-- Присваиваем категории местам без категорий
DECLARE @PlaceId INT;
DECLARE @PlaceName NVARCHAR(MAX);
DECLARE @PlaceDescription NVARCHAR(MAX);
DECLARE @Text NVARCHAR(MAX);
DECLARE @CategoryId INT;

DECLARE place_cursor CURSOR FOR
SELECT Id, Name, Description
FROM Places
WHERE NOT EXISTS (
    SELECT 1 FROM PlaceCategories pc WHERE pc.PlaceId = Places.Id
);

OPEN place_cursor;
FETCH NEXT FROM place_cursor INTO @PlaceId, @PlaceName, @PlaceDescription;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Text = LOWER(ISNULL(@PlaceName, '') + ' ' + ISNULL(@PlaceDescription, ''));
    SET @CategoryId = NULL;

    -- Определяем категорию на основе текста
    IF (@Text LIKE '%спорт%' OR @Text LIKE '%фитнес%' OR @Text LIKE '%актив%' OR @Text LIKE '%тренировк%' OR @Text LIKE '%заняти%')
        SET @CategoryId = @ActiveCategoryId;
    ELSE IF (@Text LIKE '%музей%' OR @Text LIKE '%театр%' OR @Text LIKE '%выставка%' OR @Text LIKE '%кино%' OR @Text LIKE '%концерт%' OR @Text LIKE '%галерея%')
        SET @CategoryId = @CalmCategoryId;
    ELSE IF (@Text LIKE '%парк%' OR @Text LIKE '%природа%' OR @Text LIKE '%лес%' OR @Text LIKE '%озеро%' OR @Text LIKE '%река%' OR @Text LIKE '%пляж%')
        SET @CategoryId = @NatureCategoryId;
    ELSE IF (@Text LIKE '%семья%' OR @Text LIKE '%семейн%')
        SET @CategoryId = @FamilyCategoryId;
    ELSE IF (@Text LIKE '%дет%' OR @Text LIKE '%ребенок%' OR @Text LIKE '%малыш%')
        SET @CategoryId = @ChildCategoryId;
    ELSE
        SET @CategoryId = @FriendsCategoryId; -- По умолчанию

    -- Добавляем категорию, если она определена и еще не существует
    IF @CategoryId IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM PlaceCategories WHERE PlaceId = @PlaceId AND CategoryId = @CategoryId
    )
    BEGIN
        INSERT INTO PlaceCategories (PlaceId, CategoryId)
        VALUES (@PlaceId, @CategoryId);
        PRINT 'Месту "' + @PlaceName + '" присвоена категория ID: ' + CAST(@CategoryId AS VARCHAR);
    END

    FETCH NEXT FROM place_cursor INTO @PlaceId, @PlaceName, @PlaceDescription;
END

CLOSE place_cursor;
DEALLOCATE place_cursor;
GO

-- Проверяем результат
SELECT COUNT(*) AS 'Мест без категорий после присвоения'
FROM Places p
WHERE NOT EXISTS (
    SELECT 1 FROM PlaceCategories pc WHERE pc.PlaceId = p.Id
);
GO

PRINT 'Готово!';
GO


