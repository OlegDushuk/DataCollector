# Заповнює БД тестовими даними: модель shop_orders з 6 полями і 10000 записів.
# Потрібен sqlcmd (йде разом з SQL Server / SSMS або: winget install Microsoft.Sqlcmd).
# Кількість записів змінюється в scripts\seed-demo-data.sql (@RecordCount).

param(
  [string]$Server = "localhost",
  [string]$Database = "DataCollector_Dev"
)

$script = Join-Path $PSScriptRoot "scripts\seed-demo-data.sql"

# -f 65001 - читати скрипт як UTF-8 (українські назви), -b - завершитись з помилкою при збої
sqlcmd -S $Server -d $Database -E -C -f 65001 -b -i $script
if ($LASTEXITCODE -ne 0) { throw "Seed failed" }
