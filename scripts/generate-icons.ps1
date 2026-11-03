# Генерация справочника классов иконок Font Awesome: Adashboard.Domain/Reference/FontAwesomeIcons.cs
#
# Справочник строится по pro-css: он определяет, что реально есть в устанавливаемом pro-комплекте
# (6.5.1), а не по документации — в pro-наборе иконок больше, чем в free.
#
# Имя иконки:
#   * правило content: может перечислять несколько имён — canonical и его алиасы;
#   * имя, уже принятое в справочнике, сохраняется (оно взято из metadata Font Awesome 6);
#   * для новых имён используется первое имя правила: порядок имён в css не всегда canonical
#     (например, `chevron-circle-right` стоит раньше `circle-chevron-right`);
#   * алиасы не добавляются: в UI достаточно canonical-имени, css рисует иконку и по алиасу.
#
# Стили: у классических иконок доступны solid и regular (regular-шрифт pro — полный),
# у брендовых только brands. Тонкие и duotone-стили в справочник не включаются: их шрифты не публикуются.

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$css = Join-Path $root 'Adashboard.Client/wwwroot/lib/fa/css/fa.all.min.css'
$target = Join-Path $root 'Adashboard.Domain/Reference/FontAwesomeIcons.cs'

# canonical-имена, уже принятые в проекте (справочник собран по metadata Font Awesome 6):
# для имён из pro-css они сохраняются, иначе порядок имён в правиле css даёт алиас вместо canonical
$styles = @('solid', 'regular', 'brands', 'sharp-solid')
$known = @{}
if (Test-Path $target) {
    $existing = Get-Content $target -Encoding UTF8 -Raw
    foreach ($m in ([regex]::Matches($existing, 'fa-([a-z0-9-]+)"'))) {
        $name = $m.Groups[1].Value
        if ($styles -contains $name) { continue }
        $known[$name] = $true
    }
}

$raw = Get-Content $css -Encoding UTF8 -Raw
$brandsStart = $raw.IndexOf('--fa-style-family-brands')
$sections = @(
    @{ style = 'fa-solid'; text = $raw.Substring(0, $brandsStart) },
    @{ style = 'fa-brands'; text = $raw.Substring($brandsStart) }
)

$names = @{}
foreach ($section in $sections) {
    # правило вида ".fa-name:before,.fa-alias:before{content:`"\f123`}"; имена берутся из селектора
    foreach ($rule in ([regex]::Matches($section.text, '[^\{}]+?\{content:"\\[0-9a-f]{2,6}"'))) {
        $ruleNames = @()
        foreach ($name in ([regex]::Matches($rule.Value, '\.fa-([a-z0-9-]+)'))) {
            $ruleNames += $name.Groups[1].Value
        }
        if ($ruleNames.Count -eq 0) { continue }

        # из нескольких имён правила выбираем принятое (canonical), иначе первое
        $chosen = $ruleNames | Where-Object { $known.ContainsKey($_) } | Select-Object -First 1
        if (-not $chosen) { $chosen = $ruleNames[0] }
        if (-not $names.ContainsKey($chosen)) { $names[$chosen] = $section.style }
    }
}

$classic = @($names.Keys | Where-Object { $names[$_] -eq 'fa-solid' } | Sort-Object)
$brands = @($names.Keys | Where-Object { $names[$_] -eq 'fa-brands' } | Sort-Object)

$sb = New-Object System.Text.StringBuilder
function Add-Line([string]$text) { $null = $sb.AppendLine($text) }

Add-Line 'namespace Adashboard.Domain.Reference;'
Add-Line ''
Add-Line '/// <summary>'
Add-Line '/// Справочник классов иконок Font Awesome pro-комплекта: комбинации «стиль + имя», доступные в подключённых шрифтах.'
Add-Line '/// Собирается по css комплекта скриптом scripts/generate-icons.ps1, поэтому соответствует устанавливаемому набору, а не документации.'
Add-Line '/// Имена canonical: у классических иконок доступны solid и regular, у брендовых — только brands.'
Add-Line '/// </summary>'
Add-Line 'public static class FontAwesomeIcons'
Add-Line '{'
Add-Line '    /// <summary>Классы иконок, сгруппированные по стилю; внутри группы отсортированы по имени.</summary>'
Add-Line '    public static readonly string[] Icons ='
Add-Line '    ['

foreach ($style in @('fa-solid', 'fa-regular', 'fa-brands')) {
    Add-Line ('        // ' + $style)
    $items = if ($style -eq 'fa-brands') { $brands } else { $classic }
    foreach ($name in $items) { Add-Line ('        "' + $style + ' fa-' + $name + '",') }
}

Add-Line '    ];'
Add-Line '}'

[System.IO.File]::WriteAllText($target, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Output ('classic: ' + $classic.Count + ' | brands: ' + $brands.Count + ' | icons: ' + ($classic.Count * 2 + $brands.Count))
Write-Output ('written ' + $target)
