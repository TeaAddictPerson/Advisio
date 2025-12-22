-- Добавление поля Type в таблицу Places
-- Выполните этот скрипт в SQL Server Management Studio или через sqlcmd

USE [AdvisioDb];
GO

-- Проверяем, существует ли уже колонка Type
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Places]') AND name = 'Type')
BEGIN
    ALTER TABLE [Places]
    ADD [Type] NVARCHAR(MAX) NULL;
    
    PRINT 'Колонка Type успешно добавлена в таблицу Places';
END
ELSE
BEGIN
    PRINT 'Колонка Type уже существует в таблице Places';
END
GO
