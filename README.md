# DataCollector

Система збору, зберігання, моніторингу та аналізу даних. Користувач описує власні моделі даних
(назва, ключ, поля з типами), створює записи вручну в UI або через REST API.

## Структура

| Проєкт | Призначення |
|---|---|
| `DataCollector.Server.Domain` | Сутності (`EntityConfig`, `EntityPropertyConfig`, `EntityInstance`, `EntityProperty`) і бізнес-правила |
| `DataCollector.Server.Application` | Сервіси, контракти API (`ExternalContracts`), інтерфейси репозиторіїв |
| `DataCollector.Server.DataAccess` | Репозиторії на Dapper, виклик збережених процедур `sp_{Table}_{Action}` |
| `DataCollector.Server.DataBase` | Схема БД (SQL Server, `.sqlproj` → dacpac) |
| `DataCollector.Server.API` | REST API, помилки у форматі ProblemDetails, OpenAPI |
| `DataCollector.WebUI` | Blazor Server інтерфейс |

## Запуск

1. **База даних**: `./publish-db.ps1` (збирає dacpac і публікує в `DataCollector_Dev` на localhost).
2. **API**: рядок підключення в `DataCollector.Server.API/appsettings.Development.json`, запуск профілю `http` → `http://localhost:5054`.
3. **WebUI**: адреса API в `DataCollector.WebUI/appsettings.json` (`ServerApi:BaseUrl`), запуск → `http://localhost:5000`.
4. **Стилі**: `npm install`, потім `npx gulp` (або `npx gulp watch`) у `DataCollector.WebUI` — збирає `Assets/Scss` у `wwwroot/dist/app.min.css`.

## API

`{config}` — Id моделі або її ключ.

| Метод | Шлях | Опис |
|---|---|---|
| GET | `api/data-configs` | список моделей з кількістю полів і записів |
| GET | `api/data-configs/{config}` | модель з полями |
| POST | `api/data-configs` | створити модель з полями |
| PATCH | `api/data-configs/{config}` | змінити назву / ключ |
| DELETE | `api/data-configs/{config}` | видалити модель разом з даними |
| POST | `api/data-configs/{config}/properties` | додати поля |
| PATCH | `api/data-configs/{config}/properties/{id}` | змінити назву / ключ поля |
| DELETE | `api/data-configs/{config}/properties/{id}` | видалити поле та його значення |
| GET | `api/data-configs/{config}/records?pageNumber=1&pageSize=10&sortBy=price&sortDescending=false` | записи з пагінацією та сортуванням |
| GET | `api/data-configs/{config}/records/{id}` | запис |
| POST | `api/data-configs/{config}/records` | створити запис |
| PATCH | `api/data-configs/{config}/records/{id}` | оновити передані поля запису |
| DELETE | `api/data-configs/{config}/records/{id}` | видалити запис |

Приклад створення запису:

```http
POST api/data-configs/orders/records
Content-Type: application/json

{ "values": { "number": "ORD-0001", "price": 120.5, "paid": true } }
```

Помилки: `400` — невалідні дані, `404` — не знайдено, `409` — конфлікт (ключ зайнятий).
Специфікація OpenAPI в режимі Development: `/openapi/v1.json`.

## Зберігання даних

Зараз — EAV: значення полів зберігаються рядками в `EntityPropertyInstances.Value`
у нормалізованому вигляді (числа — інваріантна культура, bool — `true`/`false`).
Сортування за полем виконується в `sp_EntityInstances_GetPage` через `TRY_CAST`.
