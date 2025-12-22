# Инструкция по добавлению поля Type в таблицу Places

## Проблема
В базе данных отсутствует поле `Type` в таблице `Places`, что вызывает ошибку при загрузке мест.

## Решение

### Вариант 1: Применить миграцию через Entity Framework (рекомендуется)

1. **Остановите приложение** (если оно запущено)
2. Выполните команду в терминале:
   ```bash
   dotnet ef database update --project Advisio.Web/Advisio.Web.csproj
   ```

### Вариант 2: Выполнить SQL скрипт вручную

1. Откройте SQL Server Management Studio или другой SQL клиент
2. Подключитесь к базе данных `AdvisioDb`
3. Выполните скрипт `AddTypeColumnToPlaces.sql`:
   ```sql
   USE [AdvisioDb];
   GO
   
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
   ```

## После применения

После добавления поля перезапустите приложение. Ошибка "Ошибка загрузки мест" должна исчезнуть.

