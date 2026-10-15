# Створює в SQL Server обліковий запис застосунку з мінімальними правами (роль DataCollectorApp)
# і показує рядок підключення для appsettings. Запускати після publish-db.ps1, від імені адміністратора SQL Server.
# Приклад: .\create-app-login.ps1 -Password "Str0ng!Passw0rd"

param(
  [string]$Server = "localhost",
  [string]$Database = "DataCollector_Dev",
  [string]$Login = "datacollector_app",
  [Parameter(Mandatory = $true)][string]$Password
)

$script = Join-Path $PSScriptRoot "scripts\create-app-login.sql"

sqlcmd -S $Server -E -C -f 65001 -b -i $script -v DatabaseName="$Database" AppLogin="$Login" AppPassword="$Password"
if ($LASTEXITCODE -ne 0) { throw "Не вдалося створити обліковий запис застосунку" }

Write-Host ""
Write-Host "Рядок підключення для DataCollector.Server.API (ConnectionStrings:DefaultConnection):"
Write-Host "Server=$Server;Database=$Database;User Id=$Login;Password=<пароль>;TrustServerCertificate=True"
