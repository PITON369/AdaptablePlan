param([switch]$Force)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $env:LOCALAPPDATA 'AdaptablePlan\app'
$stamp = Join-Path $out '.buildstamp'

$dirty = (git -C $root status --porcelain) -ne $null

# Версия: дата сборки + количество коммитов, напр. 2026.10.08.42
$rev = [int](git -C $root rev-list --count HEAD)
$version = '{0:yyyy.MM.dd}.{1}' -f (Get-Date), $rev
if ($dirty) { $version += '-dirty' }

if (-not $Force) {
    if ($dirty) {
        Write-Host "Есть незакоммиченные изменения — публикация пропущена, установлена остаётся версия из последнего коммита. Для публикации WIP: publish.ps1 -force"
        exit 0
    }

    if (Test-Path $stamp) {
        $lastBuild = (Get-Item $stamp).LastWriteTime
        $srcDirs = 'AdaptablePlan.Core', 'AdaptablePlan.UI', 'AdaptablePlan.Desktop' | ForEach-Object { Join-Path $root $_ }
        $latest = Get-ChildItem $srcDirs -Recurse -File -Include *.cs, *.csproj, *.axaml, *.json, app.manifest |
            Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latest -and $latest.LastWriteTime -le $lastBuild) {
            Write-Host "Изменений с последней публикации ($lastBuild) нет — пропускаю."
            exit 0
        }
    }
}

dotnet publish "$root\AdaptablePlan.Desktop\AdaptablePlan.Desktop.csproj" -c Release -o $out -p:Version=$version --nologo

New-Item -ItemType File -Path $stamp -Force | Out-Null

$exe = Join-Path $out 'AdaptablePlan.Desktop.exe'
$lnk = "$env:USERPROFILE\Desktop\AdaptablePlan.lnk"
if (-not (Test-Path $lnk)) {
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut($lnk)
    $sc.TargetPath = $exe
    $sc.Save()
    Write-Host "Shortcut created: $lnk"
}

Write-Host "Опубликовано: $version"
