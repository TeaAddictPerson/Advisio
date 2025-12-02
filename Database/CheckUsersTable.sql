-- Скрипт для проверки структуры таблицы Users
-- Выполните этот скрипт, чтобы увидеть все столбцы таблицы Users

USE [AdvisioDb];
GO

-- Показываем все столбцы таблицы Users
SELECT 
    COLUMN_NAME AS 'Имя столбца',
    DATA_TYPE AS 'Тип данных',
    CHARACTER_MAXIMUM_LENGTH AS 'Макс. длина',
    IS_NULLABLE AS 'Может быть NULL',
    COLUMN_DEFAULT AS 'Значение по умолчанию',
    ORDINAL_POSITION AS 'Позиция'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users'
ORDER BY ORDINAL_POSITION;
GO

-- Проверяем, существует ли столбец PasswordHash
IF EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Users]') 
    AND name = 'PasswordHash'
)
BEGIN
    PRINT '✓ Столбец PasswordHash СУЩЕСТВУЕТ в таблице Users';
END
ELSE
BEGIN
    PRINT '✗ Столбец PasswordHash НЕ СУЩЕСТВУЕТ в таблице Users';
END
GO

