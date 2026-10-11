# DataCollector

Система збору, зберігання, моніторингу та аналізу даних. Користувач описує власні моделі даних
(назва, ключ, поля з типами), створює записи вручну в UI або через REST API.

## Структура

| Проєкт | Призначення |
|---|---|
| `DataCollector.Server.Domain` | Сутності (`EntityConfig`, `EntityPropertyConfig`, `EntityInstance`, `EntityProperty`) і бізнес-правила |
| `DataCollector.Server.Application` | Сервіси, контракти API (`ExternalContracts`), інтерфейси репозиторіїв |
| `DataCollector.Server.DataAccess` | Репозиторії на Dapper: збережені процедури `sp_{Table}_{Action}`, для пошуку з фільтрами — параметризований SQL |
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
| GET | `api/data-configs/{config}/records?pageNumber=1&pageSize=10&sortBy=price&sortDescending=false&search=текст&fields=price,status` | записи з пагінацією, сортуванням, пошуком і вибором полів |
| POST | `api/data-configs/{config}/records/query` | записи з умовами фільтра (див. «Фільтри») |
| POST | `api/data-configs/{config}/records/aggregate` | групування і показники (див. «Агрегація») |
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

## Фільтри

`POST api/data-configs/{config}/records/query` — сторінка записів, що відповідають **усім** умовам:

```http
POST api/data-configs/orders/records/query
Content-Type: application/json

{
  "filters": [
    { "field": "price", "operator": "between", "value": 100, "valueTo": 5000 },
    { "field": "status", "operator": "in", "values": ["Нове", "Оплачене"] },
    { "field": "customer", "operator": "contains", "value": "ТОВ" },
    { "field": "createdAt", "operator": "gte", "value": "2026-10-01T00:00:00Z" }
  ],
  "search": "Луцьк",
  "sortBy": "price", "sortDescending": true,
  "pageNumber": 1, "pageSize": 20,
  "fields": ["customer", "price", "status"]
}
```

| Оператор | Значення | Типи |
|---|---|---|
| `eq`, `neq` | `value` | усі поля моделі; `createdAt`, `updatedAt` |
| `gt`, `gte`, `lt`, `lte` | `value` | Number, Date, DateTime, `createdAt`, `updatedAt` |
| `between` | `value` (від), `valueTo` (до), включно | Number, Date, DateTime, `createdAt`, `updatedAt` |
| `contains`, `notContains`, `startsWith`, `endsWith` | `value` | Text |
| `in`, `notIn` | `values` (до 100) | Text, Enum |
| `isEmpty`, `isNotEmpty` | — | усі поля моделі, `updatedAt` |

- `field` — ключ поля або системне поле `createdAt` / `updatedAt` (ці ключі зарезервовані для полів моделі).
- Заперечні оператори (`neq`, `notContains`, `notIn`) включають записи з порожнім значенням.
- `search` — підрядок у будь-якому текстовому полі або полі-переліку; текст порівнюється без урахування регістру.
- Поле-перелік сортується в порядку варіантів, а не за алфавітом.
- Не більше 20 умов у запиті.
- `fields` — які поля повернути у `values` (проєкція). Не задано — усі поля; `[]` — лише `id` і дати.
  Умови, пошук і сортування працюють і за полями, яких немає у `fields`. У GET — `?fields=customer,price`.
  Інтерфейс передає сюди видимі колонки таблиці (вкладка «Колонки» в бічній панелі).

SQL для запиту збирається динамічно (`DataAccess/Querying/RecordQuerySql.cs`): кожна умова — `EXISTS` по значеннях
поля в колонці його типу, усі значення — лише параметрами, `OPTION (RECOMPILE)` будує план під конкретні умови.

## Агрегація

`POST api/data-configs/{config}/records/aggregate` — групує записи і рахує показники для кожної групи.
Умови фільтра і пошук — ті самі, що в `records/query`.

```http
POST api/data-configs/orders/records/aggregate
Content-Type: application/json

{
  "filters": [ { "field": "status", "operator": "neq", "value": "Скасоване" } ],
  "groupBy": [ { "field": "createdAt", "interval": "month" }, { "field": "status" } ],
  "metrics": [ { "function": "count" }, { "function": "sum", "field": "price" }, { "function": "avg", "field": "price" } ],
  "orderBy": "keys",
  "limit": 1000,
  "timeZone": "Europe/Kyiv"
}
```

Відповідь: опис колонок і рядки `{ "keys": ["2026-10", "Нове"], "values": [120, 15340.5, 127.84] }`, прапорець `truncated`,
якщо груп більше за `limit`.

| Параметр | Значення |
|---|---|
| `groupBy` (до 2) | поле; для дат `interval`: `hour`, `day`, `week` (з понеділка), `month`, `quarter`, `year`; для чисел `step` — ширина діапазону (100 → [0; 100), [100; 200)…: нижня межа входить, верхня — ні), без нього — точні значення; для діапазонів рядок має ще `ranges` — фактичні `{ "min", "max" }` значень у групі (група [6; 8) може містити лише 6). Порожньо — один рядок підсумків |
| `metrics` (1–10) | `count` (без поля — записи, з полем — заповнені значення), `sum`, `avg` — числа; `min`, `max` — числа й дати |
| `orderBy` | `keys` — за групами (дати хронологічно, переліки в порядку варіантів), `valueDesc` / `valueAsc` — за першим показником |
| `timeZone` | IANA або Windows; межі днів і місяців для дат з часом рахуються в ньому (за замовчуванням UTC) |

Ключі груп-дат повертаються підписами інтервалу: `2026-10-11`, `2026-10-11T08:00`, `2026-10`, `2026-Q4`, `2026`
(тиждень — дата понеділка). SQL: `DataAccess/Querying/RecordAggregateSql.cs` — LEFT JOIN значення кожного потрібного поля
по первинному ключу (InstancePkey, PropertyPkey) і `GROUP BY` по виразах над колонками типу.

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
