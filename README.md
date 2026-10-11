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

## Типи полів і правила

| Тип | Значення в JSON | Правила, крім загальних |
|---|---|---|
| `Text` | рядок до 4000 символів | `minLength`, `maxLength` |
| `Number` | число (`120.5`) або рядок (`"120,5"`) | `min`, `max` |
| `Boolean` | `true` / `false` (також `"1"` / `"0"`) | — |
| `Date` | `"2026-10-11"` (також `"11.10.2026"`) | `min`, `max` |
| `DateTime` | ISO 8601: `"2026-10-11T08:30:00Z"`; зберігається в UTC, без зміщення вважається UTC | `min`, `max` |
| `Enum` | один з варіантів (регістр не важливий) | `options` — обов'язково |

Загальні правила: `isRequired` (значення не може бути порожнім) і `defaultValue` (підставляється в новий запис,
якщо поле не передали). Правила передаються об'єктом `rules` при створенні поля і повністю замінюються
при `PATCH .../properties/{id}`:

```http
POST api/data-configs/tickets/properties
Content-Type: application/json

{ "properties": [
  { "name": "Пріоритет", "key": "priority", "dataType": "Enum",
    "rules": { "isRequired": true, "options": ["Низький", "Середній", "Високий"], "defaultValue": "Середній" } },
  { "name": "Оцінка", "key": "score", "dataType": "Number", "rules": { "min": 1, "max": 5 } }
] }
```

Нові правила діють для наступних змін записів; уже збережені значення повторно не перевіряються.
Тип поля після створення змінити не можна.
Специфікація OpenAPI в режимі Development: `/openapi/v1.json`.

## Зберігання даних

Модель EAV з типізованими колонками (`DataCollector.Server.DataBase/Tables`):

| Таблиця | Первинний ключ | Зв'язки |
|---|---|---|
| `EntityConfigs` — моделі | `Pkey INT IDENTITY` | — |
| `EntityPropertyConfigs` — поля | `Pkey INT IDENTITY` | `ConfigPkey` → `EntityConfigs.Pkey` |
| `EntityInstances` — записи | `Pkey BIGINT IDENTITY` | `ConfigPkey` → `EntityConfigs.Pkey` |
| `EntityPropertyInstances` — значення | `(InstancePkey, PropertyPkey)` | → `EntityInstances.Pkey`, `EntityPropertyConfigs.Pkey` |

- `Pkey` — внутрішній цілий ключ: кластерні індекси і всі зовнішні ключі побудовані на ньому.
- `Id` (GUID, унікальний індекс) — зовнішній ідентифікатор для API і домену. Процедури приймають GUID,
  перетворюють його на `Pkey` і повертають назовні знову GUID, тому C#-код про `Pkey` не знає.
- Значення поля лежить у колонці свого типу (`ValueText` — Text і Enum, `ValueNumber DECIMAL(38,10)`, `ValueBool`,
  `ValueDate` — Date і DateTime),
  заповнена рівно одна (CHECK); порожні значення не зберігаються. Фільтровані індекси
  `(PropertyPkey, Value*)` пришвидшують сортування і фільтрацію за полем.

- Правила поля — у `EntityPropertyConfigs`: загальні окремими колонками (`IsRequired`, `DefaultValue`),
  специфічні для типу — JSON у `Settings` (`{"min":"0","max":"100"}`, `{"options":["Low","High"]}`).
  Перевіряє правила доменна модель (`EntityPropertyConfig`), БД лише зберігає їх.

Міграцій немає: при зміні схеми БД видаляється і публікується заново (`./publish-db.ps1`).
Тестові дані: `./seed-db.ps1` (10 моделей з усіма типами полів і правилами по 100 000 записів, `scripts/seed-demo-data.sql`).
