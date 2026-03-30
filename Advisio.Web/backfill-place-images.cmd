@echo off
cd /d "%~dp0"
echo Заполнение ImageUrl для мест без картинки (тот же автоподбор, что в UI^)...
dotnet run -- --backfill-place-images
pause
