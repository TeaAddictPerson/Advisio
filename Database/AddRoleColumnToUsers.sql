-- Скрипт для добавления столбца Role в таблицу Users
-- Выполните этот скрипт в SQL Server Management Studio

USE [AdvisioDb];
GO

-- Проверяем, существует ли столбец Role
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Users]') 
    AND name = 'Role'
)
BEGIN
    -- Добавляем столбец Role типа int со значением по умолчанию 0 (User)
    ALTER TABLE [dbo].[Users]
    ADD [Role] INT NOT NULL DEFAULT 0;
    
    PRINT 'Столбец Role успешно добавлен в таблицу Users';
END
ELSE
BEGIN
    PRINT 'Столбец Role уже существует в таблице Users';
END
GO

-- Проверяем результат
SELECT 
    COLUMN_NAME AS 'Имя столбца',
    DATA_TYPE AS 'Тип данных',
    IS_NULLABLE AS 'Может быть NULL',
    COLUMN_DEFAULT AS 'Значение по умолчанию'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users' 
AND COLUMN_NAME = 'Role';
GO

PRINT 'Готово!';
GO

