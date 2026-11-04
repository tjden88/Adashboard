# Генерирует Refit-клиенты Adashboard.Client из эндпоинтов Adashboard.Server.
#
# Инструмент — локальный dotnet-tool GenerateAspNetCoreClient.Refit (манифест dotnet-tools.json в корне):
# он собирает и поднимает серверное приложение, берёт метаданные эндпоинтов (ApiExplorer) и пишет
# по одному интерфейсу на контроллер. Открытый OpenAPI-файл в репозитории для этого не нужен.
#
# Запуск: .\generate-clients.bat  или  powershell -NoProfile -File scripts\generate-api-clients.ps1
# Результат: Adashboard.Client/Generated/*.cs (каталог в .gitignore), затем — обычная сборка решения.
#
# Генератор собирает сервер, а сервер ссылается на клиент: значит старый содержимый Generated
# должен оставаться рабочим до генерации. Поэтому пишем во временный каталог и только потом
# заменяем содержимое Generated целиком — так не остаётся интерфейсов удалённых контроллеров.

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$serverProjectPath = 'Adashboard.Server'
$outputFolder = 'Adashboard.Client/Generated'
$tempFolder = 'Adashboard.Client/obj/api-clients-gen'

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'Не удалось восстановить локальные dotnet-инструменты.' }

# Генератор исполняет Program.cs сервера, поэтому старт наполнения базы и статики выключаем:
# иначе генерация создаёт побочную базу в bin и применяет миграции (см. Program.cs сервера).
$env:Adashboard__SkipStartupInitialization = 'true'

if (Test-Path $tempFolder) { Remove-Item $tempFolder -Recurse -Force }

dotnet tool run dotnet-generate-client $serverProjectPath `
    -o $tempFolder `
    -n Adashboard.Client.Services `
    --type-name-pattern 'I[controller]Client' `
    --access-modifier public `
    --add-cancellation-token true `
    --use-api-responses true

if ($LASTEXITCODE -ne 0) { throw 'Генерация Refit-клиентов завершилась с ошибкой.' }

$generatedFiles = @(Get-ChildItem $tempFolder -Filter '*.cs')
if ($generatedFiles.Count -eq 0) { throw 'Генератор не создал ни одного интерфейса: проверьте, что контроллеры найдены.' }

if (-not (Test-Path $outputFolder)) { New-Item -ItemType Directory -Path $outputFolder | Out-Null }
Get-ChildItem $outputFolder -Filter '*.cs' | Remove-Item -Force
$generatedFiles | Copy-Item -Destination $outputFolder -Force
Remove-Item $tempFolder -Recurse -Force

Write-Host "Сгенерировано клиентских интерфейсов: $($generatedFiles.Count) ($outputFolder)."
