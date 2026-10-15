-- Обліковий запис застосунку з мінімальними правами (роль DataCollectorApp з dacpac).
-- Запуск: create-app-login.ps1 (передає змінні sqlcmd), від імені адміністратора SQL Server.
-- Повторний запуск безпечний: наявні логін і користувач не перестворюються, пароль оновлюється.
--   $(DatabaseName) - база застосунку, $(AppLogin) - ім'я логіна, $(AppPassword) - пароль.
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE [name] = N'$(AppLogin)')
BEGIN
    CREATE LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)];
    PRINT N'Створено логін $(AppLogin)';
END
ELSE
BEGIN
    ALTER LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)';
    PRINT N'Логін $(AppLogin) вже існує - пароль оновлено';
END
GO

USE [$(DatabaseName)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE [name] = N'$(AppLogin)')
    CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)] WITH DEFAULT_SCHEMA = [dbo];
GO

IF IS_ROLEMEMBER(N'DataCollectorApp', N'$(AppLogin)') = 0
    ALTER ROLE [DataCollectorApp] ADD MEMBER [$(AppLogin)];
GO

-- Перевірка: користувач має лише права ролі - зміна схеми недоступна.
EXECUTE AS USER = N'$(AppLogin)';
SELECT
    HAS_PERMS_BY_NAME(N'dbo.sp_EntityConfigs_GetAll', N'OBJECT', N'EXECUTE') AS [CanExecuteProcedures],
    HAS_PERMS_BY_NAME(N'dbo.EntityInstances', N'OBJECT', N'SELECT') AS [CanReadRecords],
    HAS_PERMS_BY_NAME(N'dbo.EntityInstances', N'OBJECT', N'DELETE') AS [CanDeleteDirectly],
    HAS_PERMS_BY_NAME(N'dbo.Users', N'OBJECT', N'SELECT') AS [CanReadUsersDirectly],
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER') AS [CanAlterSchema];
REVERT;
GO
