#!/bin/bash
# Bash-аналог publish.ps1 (PowerShell-скрипты заблокированы GPO).
# Запуск из git bash: ./publish.sh [--force]
set -e

force=false
[ "$1" = "--force" ] || [ "$1" = "-force" ] && force=true

root="$(cd "$(dirname "$0")" && pwd)"
out="$LOCALAPPDATA/AdaptablePlan/app"
stamp="$out/.buildstamp"

rev=$(git -C "$root" rev-list --count HEAD)
version="$(date +%Y.%m.%d).$rev"
dirty=false
if [ -n "$(git -C "$root" status --porcelain)" ]; then
    version="$version-dirty"
    dirty=true
fi

if ! $force; then
    if $dirty; then
        echo "Есть незакоммиченные изменения — публикация пропущена. Для публикации WIP: ./publish.sh --force"
        exit 0
    fi
    if [ -f "$stamp" ]; then
        latest=$(find "$root/AdaptablePlan.Core" "$root/AdaptablePlan.UI" "$root/AdaptablePlan.Desktop" \
            -type f \( -name '*.cs' -o -name '*.csproj' -o -name '*.axaml' -o -name '*.json' -o -name 'app.manifest' \) \
            -newer "$stamp" -not -path '*/bin/*' -not -path '*/obj/*' | head -1)
        if [ -z "$latest" ]; then
            echo "Изменений с последней публикации нет — пропускаю."
            exit 0
        fi
    fi
fi

dotnet publish "$root/AdaptablePlan.Desktop/AdaptablePlan.Desktop.csproj" -c Release -o "$out" -p:Version="$version" --nologo

touch "$stamp"

exe="$out/AdaptablePlan.Desktop.exe"
lnk="$USERPROFILE/Desktop/AdaptablePlan.lnk"
if [ ! -f "$lnk" ]; then
    powershell -ExecutionPolicy Bypass -Command "\$ws = New-Object -ComObject WScript.Shell; \$sc = \$ws.CreateShortcut('$lnk'); \$sc.TargetPath = '$exe'; \$sc.Save(); Write-Host 'Shortcut created'"
fi

echo "Опубликовано: $version"
