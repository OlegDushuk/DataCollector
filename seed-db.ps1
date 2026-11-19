# Демонстраційні дані інтернет-магазину: моделі «Замовлення», «Товари», «Повернення», «Відгуки» (≈ 14 місяців історії),
# два дашборди, правила моніторингу, демо-користувачі й API-ключ сайту магазину.
# УВАГА: спочатку видаляє з бази всі моделі з записами, дашборди, правила й сповіщення (користувачі лишаються).
# Дані будуються відносно моменту запуску - запускайте в день демонстрації.
# Потрібен sqlcmd (йде разом з SQL Server / SSMS або: winget install Microsoft.Sqlcmd). Сам скрипт - scripts\seed-demo-data.sql.

param(
  [string]$Server = "localhost",
  [string]$Database = "DataCollector_Dev"
)

$script = Join-Path $PSScriptRoot "scripts\seed-demo-data.sql"

# Повідомлення скрипту - українською: вивід sqlcmd у UTF-8.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# -f 65001 - читати скрипт як UTF-8 (українські назви), -b - завершитись з помилкою при збої
sqlcmd -S $Server -d $Database -E -C -f 65001 -b -i $script
if ($LASTEXITCODE -ne 0) { throw "Seed failed" }
