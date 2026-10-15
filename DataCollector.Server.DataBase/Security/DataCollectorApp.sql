-- Роль застосунку (принцип найменших привілеїв). Обліковий запис, під яким працює API, входить лише в цю роль:
--  - змінює дані тільки через збережені процедури (EXECUTE); прямого INSERT / UPDATE / DELETE у таблиці немає;
--  - читає напряму лише чотири таблиці записів - їх використовує параметризований SQL пошуку й агрегації;
--  - не має прав на зміну схеми (DDL): CREATE / ALTER / DROP недоступні, навіть якщо зловмисник
--    отримає контроль над рядком підключення чи знайде помилку в коді.
-- Користувача і логін створює scripts/create-app-login.sql (логін - об'єкт сервера, а не бази, тому не в dacpac).
CREATE ROLE [DataCollectorApp] AUTHORIZATION [dbo];
GO

-- Процедури sp_{Table}_{Action}. Процедура належить dbo і працює з таблицями від імені власника
-- (ланцюжок володіння), тому права на самі таблиці для змін не потрібні.
GRANT EXECUTE ON SCHEMA::[dbo] TO [DataCollectorApp];
GO

-- Табличні параметри, які застосунок передає в процедури.
GRANT EXECUTE ON TYPE::[dbo].[PropertyValueList] TO [DataCollectorApp];
GO
GRANT EXECUTE ON TYPE::[dbo].[EntityPropertyList] TO [DataCollectorApp];
GO
GRANT EXECUTE ON TYPE::[dbo].[InstanceList] TO [DataCollectorApp];
GO
GRANT EXECUTE ON TYPE::[dbo].[InstanceValueList] TO [DataCollectorApp];
GO
GRANT EXECUTE ON TYPE::[dbo].[IdList] TO [DataCollectorApp];
GO
GRANT EXECUTE ON TYPE::[dbo].[DashboardWidgetList] TO [DataCollectorApp];
GO

-- Пошук, фільтри, агрегація й експорт будують SQL під запит (лише параметри + ідентифікатори з білого списку).
-- Такий SQL виконується з правами користувача, тож йому потрібне читання - і тільки читання - цих таблиць.
GRANT SELECT ON [dbo].[EntityConfigs] TO [DataCollectorApp];
GO
GRANT SELECT ON [dbo].[EntityPropertyConfigs] TO [DataCollectorApp];
GO
GRANT SELECT ON [dbo].[EntityInstances] TO [DataCollectorApp];
GO
GRANT SELECT ON [dbo].[EntityPropertyInstances] TO [DataCollectorApp];
