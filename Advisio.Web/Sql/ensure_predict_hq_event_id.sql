-- Сценарий A: В истории миграций есть запись, а столбца PredictHqEventId в Places нет.
-- Тогда EF считает базу «актуальной» и ничего не делает. Удалите только эту запись и снова:
--   dotnet ef database update --project Advisio.Web --startup-project Advisio.Web

IF COL_LENGTH(N'dbo.Places', N'PredictHqEventId') IS NULL
   AND EXISTS (
       SELECT 1 FROM [__EFMigrationsHistory]
       WHERE [MigrationId] = N'20260405195808_AddPredictHqEventIdToPlace')
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405195808_AddPredictHqEventIdToPlace';
END
GO

-- Сценарий B: Просто добавить столбец и индекс (можно без EF). Повторный запуск безопасен.

IF COL_LENGTH(N'dbo.Places', N'PredictHqEventId') IS NULL
BEGIN
    ALTER TABLE dbo.Places ADD PredictHqEventId NVARCHAR(64) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.Places') AND i.name = N'IX_Places_PredictHqEventId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_Places_PredictHqEventId
    ON dbo.Places(PredictHqEventId)
    WHERE PredictHqEventId IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405195808_AddPredictHqEventIdToPlace')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260405195808_AddPredictHqEventIdToPlace', N'8.0.0');
END
GO
