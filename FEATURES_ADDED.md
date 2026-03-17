# Добавленные функции Advisio

## 1. Коллекции мест («Мои планы на выходные»)

- **Модели:** `PlaceCollection`, `PlaceCollectionItem`
- **API:** `GET/POST/PUT/DELETE /api/Collections`, `POST/DELETE /api/Collections/{id}/places/{placeId}`
- **Профиль:** Создание коллекций, просмотр мест в коллекциях
- **Страница места:** Кнопка «Добавить в коллекцию» с выбором коллекции

## 2. Статистика и аналитика в профиле

- **Модели:** `PlaceView`, `UserFilterUsage`
- **API:** `GET /api/Users/{id}/stats`
- **Профиль:** Блок «Статистика активности» — чаще просматриваемые места, часто используемые фильтры
- Просмотры записываются при открытии страницы места
- Использование фильтров записывается при загрузке списка мест

## 3. Google Maps — точное местоположение

- Поиск по названию и адресу: `https://www.google.com/maps/search/?api=1&query={name},{address}`
- Если есть координаты — используется `?q=lat,lng`
- Кнопка «Открыть на карте» доступна и при отсутствии координат (поиск по названию)

## 4. Отзывы, рейтинги и подписки

- **Модели:** `PlaceReview`, `PlaceSubscription`, `CategorySubscription`
- **API:**
  - `GET/POST /api/Places/{id}/reviews`
  - `POST/DELETE /api/Places/{id}/subscribe`
  - `POST/DELETE /api/Categories/{id}/subscribe`
- **Страница места:** Рейтинг, форма отзыва, кнопка подписки на обновления
- Подписка на категории — через API (можно добавить UI)

## 5. Поиск изображений для мест

- **API:** `GET /api/Images/search?query=...&count=5`
- По умолчанию: picsum.photos (placeholder)
- Для Unsplash: в `appsettings.json` добавить `"ImageSearch": { "Provider": "unsplash", "ApiKey": "..." }`
- Для Pexels: `"Provider": "pexels", "ApiKey": "..." `
- **Редактирование места:** Кнопка поиска изображения рядом с полем URL

## 6. Контекстные факторы (время года, погода, геолокация)

Требует доработки:
- **Сезон:** определяется по дате на бэкенде
- **Погода:** интеграция с OpenWeatherMap или аналогом
- **Геолокация:** `navigator.geolocation` на фронтенде, передача lat/lng в API рекомендаций

Рекомендации можно расширить, учитывая эти параметры при расчёте скора.

---

## Миграция БД

После остановки сервера выполните:

```bash
cd D:\CodingStuff\Advisio
dotnet ef database update --project Advisio.Web --startup-project Advisio.Web --context AdvisioDbContext
```
