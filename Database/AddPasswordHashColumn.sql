-- Простой скрипт для добавления столбца PasswordHash
-- Выполните этот скрипт в SQL Server Management Studio

USE [AdvisioDb];
GO

-- Проверяем структуру таблицы Users перед изменением
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users'
ORDER BY ORDINAL_POSITION;
GO

-- Удаляем столбец, если он существует (для пересоздания)
IF EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Users]') 
    AND name = 'PasswordHash'
)
BEGIN
    ALTER TABLE [dbo].[Users]
    DROP COLUMN [PasswordHash];
    PRINT 'Старый столбец PasswordHash удален';
END
GO

-- Добавляем столбец PasswordHash
ALTER TABLE [dbo].[Users]
ADD [PasswordHash] NVARCHAR(MAX) NOT NULL DEFAULT '';
GO

-- Обновляем существующие записи (на всякий случай)
UPDATE [dbo].[Users] 
SET [PasswordHash] = '' 
WHERE [PasswordHash] IS NULL OR [PasswordHash] = '';
GO

-- Проверяем результат
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users' 
AND COLUMN_NAME = 'PasswordHash';
GO

PRINT 'Столбец PasswordHash успешно добавлен!';
GO
