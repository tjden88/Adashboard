<#
.SYNOPSIS
    Генерирует компактный символьный индекс репозитория (project map) для контекста ИИ-агента.

.DESCRIPTION
    Индекс нужен, чтобы агент определял, ГДЕ лежит нужное, одним поиском, а не чтением файлов.
    В индекс попадают пути файлов и ключевые символы (типы, публичные методы, маршруты Razor,
    параметры компонентов, экспорты TypeScript) — по одной короткой строке на файл.

    Файл индекса не вставляется в контекст целиком: по нему ищут именем символа, и в контекст
    попадает только найденная строка. Поэтому важнее плотность строк, чем полнота.

    Проверяемые форматы: .cs, .razor, .ts. Каталоги bin/obj/node_modules/Generated/wwwroot/dist пропускаются.

    Файл сохраняется в UTF-8 с BOM: Windows PowerShell 5.1 иначе читает его как ANSI.

.EXAMPLE
    powershell -File scripts/project-map.ps1
    powershell -File scripts/project-map.ps1 -Projects Adashboard.Server -MaxSymbols 3
    powershell -File scripts/project-map.ps1 -Full
    powershell -File scripts/project-map.ps1 -Stdout
#>
[CmdletBinding()]
param(
    [string]$Root = '',
    [string[]]$Projects = @('Adashboard.Client', 'Adashboard.Server', 'Adashboard.Domain', 'Adashboard.Data', 'Adashboard.Extension'),
    [string]$Out = 'docs/project-map.md',
    [int]$MaxSymbols = 6,
    [int]$Budget = 13000,
    [switch]$Full,
    [switch]$Stdout
)

# Корень репозитория: у скрипта он определяется по расположению файла, при интерактивном вызове — по текущей папке.
if (-not $Root) {
    if ($PSScriptRoot) {
        $Root = Split-Path -Parent $PSScriptRoot
    }
    else {
        $Root = (Get-Location).Path
    }
}

if ($Full) {
    $MaxSymbols = 12
}

$skipDirs = 'bin', 'obj', 'node_modules', 'Generated', 'dist', 'wwwroot', 'publish', 'assets', 'icons'
$codeExtensions = '.cs', '.razor', '.ts'
$skipFilePatterns = '\.Designer\.cs$', '\.g\.cs$', 'vite-env\.d\.ts$'

# Символы, которые попадают в индекс. Порядок групп влияет на приоритет: сначала тип файла,
# затем публичные члены, затем строки, полезны для навигации (маршруты, DI-регистрации).
$patterns = @{
    '.cs' = @(
        '^\s*(public|internal)\s+(sealed\s+|static\s+|abstract\s+|partial\s+)*(class|record|interface|enum|struct)\s+\w+',
        '^\s*public\s+[^\s{=(]+\s+\w+\s*\([^;{]*\)',
        '^\s*public\s+(const|static readonly)\s+\w+\s+\w+\s*='
    )
    '.razor' = @(
        '^\s*@page\s+"[^"]+"',
        '^\s*\[Parameter\][^\r\n]*\r?\n\s*public\s+\w+(<[^>]*>)?\s+\w+',
        '^\s*(public|private)\s+(async\s+)?\w+(<[^>]*>)?\s+\w+\s*\([^;{]*\)',
        '^\s*@inject\s+\w+\s+\w+'
    )
    '.ts' = @(
        '^\s*export\s+(async\s+)?(function|const|let|interface|type|class|enum)\s+\w+'
    )
}

function Format-Symbol([string]$line) {
    # Индексу нужно имя, а не полное объявление: модификаторы и тела убираем, строку укорачиваем.
    $text = $line.Trim() -replace '\s+', ' '
    $text = $text -replace '^\s*(public|private|internal|static|sealed|abstract|partial|async)\s+', ''

    if ($text.Length -gt 70) {
        $text = $text.Substring(0, 70).TrimEnd()
    }

    return $text
}

function Get-FileSymbols([string]$filePath, [string[]]$filePatterns, [int]$limit) {
    $symbols = New-Object System.Collections.Generic.List[string]

    foreach ($line in Get-Content -LiteralPath $filePath -Encoding UTF8) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -lt 6) {
            continue
        }

        foreach ($pattern in $filePatterns) {
            if ($trimmed -match $pattern) {
                $symbol = Format-Symbol $trimmed

                # DI-регистрации сервера: без них непонятно, какой сервис доступен компоненту и контроллеру.
                if ($trimmed -match 'Add(Scoped|Singleton|Transient|HttpClient)<\w+>\(') {
                    $symbol = $trimmed -replace '\s+', ' '
                }

                $symbols.Add($symbol)
                break
            }
        }

        if ($symbols.Count -ge $limit) {
            break
        }
    }

    return $symbols
}

$builder = New-Object System.Text.StringBuilder
$indexedFiles = 0
$skippedFiles = 0
$perProject = [ordered]@{}

foreach ($project in $Projects) {
    $projectPath = Join-Path $Root $project
    if (-not (Test-Path -LiteralPath $projectPath)) {
        continue
    }

    $files = Get-ChildItem -LiteralPath $projectPath -Recurse -File |
        Where-Object { $codeExtensions -contains $_.Extension } |
        Where-Object { $_.FullName -notmatch ('\\(' + ($skipDirs -join '|') + ')\\') } |
        Where-Object { $_.Name -notmatch ($skipFilePatterns -join '|') } |
        Sort-Object FullName

    $fileLines = New-Object System.Collections.Generic.List[string]

    foreach ($file in $files) {
        $symbols = Get-FileSymbols $file.FullName $patterns[$file.Extension] $MaxSymbols
        if ($symbols.Count -eq 0) {
            $skippedFiles++
            continue
        }

        $relative = $file.FullName.Substring($Root.Length + 1).Replace('\', '/')
        # Одна строка на файл: путь + символы. Так индекс читается поиском, а не полным чтением.
        $fileLines.Add($relative + ': ' + (($symbols | Select-Object -First $MaxSymbols) -join ' | '))
        $indexedFiles++
    }

    $perProject[$project] = $fileLines
}

[void]$builder.AppendLine('<!--')
[void]$builder.AppendLine('Символьный индекс проекта. Сгенерирован scripts/project-map.ps1 — вручную не редактируется.')
[void]$builder.AppendLine('Как использовать: ищи здесь имя символа, открывай только файл из строки, читай его по месту, а не целиком.')
[void]$builder.AppendLine('Дата генерации: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm'))
[void]$builder.AppendLine('-->')
[void]$builder.AppendLine('')

foreach ($project in $Projects) {
    if (-not $perProject[$project]) {
        continue
    }

    [void]$builder.AppendLine('## ' + $project)
    foreach ($line in $perProject[$project]) {
        [void]$builder.AppendLine($line)
    }

    [void]$builder.AppendLine('')
}

$result = $builder.ToString()
$estimatedTokens = [int]($result.Length / 3.2)

if ($Stdout) {
    Write-Output $result
}
else {
    $outPath = Join-Path $Root $Out
    $directory = Split-Path -Parent $outPath
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($outPath, $result, (New-Object System.Text.UTF8Encoding($true)))
    Write-Output ("Индекс записан: {0}" -f $outPath)
}

Write-Output ("Файлов в индексе: {0}, пропущено без символов: {1}, размер: {2} символов (~{3} токенов)" -f $indexedFiles, $skippedFiles, $result.Length, $estimatedTokens)

if ($result.Length -gt $Budget) {
    Write-Warning ("Индекс превышает бюджет {0}: уменьши -MaxSymbols или сузь -Projects (превышение {1} символов)." -f $Budget, ($result.Length - $Budget))
}
