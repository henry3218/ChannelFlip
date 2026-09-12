$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$catalog = [Xml.Linq.XElement]::Load((Join-Path $projectRoot 'src\Translations.xml'))
$keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($item in $catalog.Elements('text')) {
    $zh = [string]$item.Element('zh').Value; $en = [string]$item.Element('en').Value
    if (-not $keys.Add($zh)) { throw ('Duplicate translation key: ' + $zh) }
    if ([string]::IsNullOrWhiteSpace($zh) -or [string]::IsNullOrWhiteSpace($en)) { throw 'Translation text must not be empty.' }
    if ($en -match '[\p{IsCJKUnifiedIdeographs}]') { throw ('Untranslated English entry: ' + $zh) }
    $zhSlots = @([regex]::Matches($zh,'\{([0-9]+)[^}]*\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object) -join ','
    $enSlots = @([regex]::Matches($en,'\{([0-9]+)[^}]*\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object) -join ','
    if ($zhSlots -ne $enSlots) { throw ('Translation arguments differ: ' + $zh) }
}
$literalPattern = '@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"'
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs')) {
    $source = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($source,$literalPattern)) {
        if ($match.Value -notmatch '[\p{IsCJKUnifiedIdeographs}]') { continue }
        $key = [regex]::Unescape($match.Value.Substring(1,$match.Value.Length - 2))
        if ($source.Substring(0,$match.Index) -notmatch 'L10n\.(T|M)\(\s*$' -or -not $keys.Contains($key)) {
            throw ('Uncatalogued application text in ' + $file.Name + ': ' + $key)
        }
    }
}
$xaml = [Xml.Linq.XElement]::Load((Join-Path $projectRoot 'src\MainWindow.xaml'))
foreach ($element in @($xaml) + @($xaml.Descendants())) {
    foreach ($attribute in $element.Attributes()) {
        $value = $attribute.Value
        if ($value -notmatch '[\p{IsCJKUnifiedIdeographs}]' -or $value -in @('繁體中文','Language / 語言')) { continue }
        if ($value -notmatch '^\{DynamicResource Ui\.(.+)\}$' -or -not $keys.Contains($Matches[1])) { throw ('Uncatalogued XAML text: ' + $value) }
    }
}
Write-Output ('PASS {0} bilingual messages; source/XAML coverage and format arguments verified.' -f $keys.Count)
