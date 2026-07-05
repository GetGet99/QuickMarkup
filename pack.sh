#!/usr/bin/env bash

set -e

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

pack() {
    local project="$1"
    local path="$root/$project"

    printf '\033[0;32mPacking %s...\033[0m\n' "$project"

    dotnet pack "$path" --no-restore -c Release
}

case "${1:-}" in
    all)
        pack "QuickMarkup.Infra"
        # outside Windows, it is doing offline build
        # pack "Frameworks/QuickMarkup.WinUI"
        pack "Frameworks/QuickMarkup.Uno"
        pack "Frameworks/QuickMarkup.UWP"
        ;;
    infra)
        pack "QuickMarkup.Infra"
        ;;
    *)
        printf '\033[0;33mUsage: %s {all | infra}\033[0m\n' "$(basename "$0")"
        exit 1
        ;;
esac