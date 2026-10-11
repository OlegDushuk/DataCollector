# Заповнює БД тестовими даними: 10 моделей різної структури (від 3 до 12 полів) по 100 000 записів.
# Потрібен sqlcmd (йде разом з SQL Server / SSMS або: winget install Microsoft.Sqlcmd).
# Кількість записів змінюється в scripts\seed-demo-data.sql (@RecordsPerModel), моделі й поля - у #Models / #Props.

param(
  [string]$Server = "localhost",
  [string]$Database = "DataCollector_Dev"
)

$script = Join-Path $PSScriptRoot "scripts\seed-demo-data.sql"

# -f 65001 - читати скрипт як UTF-8 (українські назви), -b - завершитись з помилкою при збої
sqlcmd -S $Server -d $Database -E -C -f 65001 -b -i $script
if ($LASTEXITCODE -ne 0) { throw "Seed failed" }
