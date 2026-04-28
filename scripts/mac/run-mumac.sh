#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
export PATH="${HOME}/.dotnet:${HOME}/.dotnet/tools:${PATH}"
export MGFXC_WINE_PATH="${MGFXC_WINE_PATH:-${HOME}/.mgfxc-wine}"
cd "$ROOT"
exec dotnet run --project MuMac/MuMac.csproj -f net10.0 -c "${1:-Debug}"
