#!/usr/bin/env bash
set -e
BEPINEX_PLUGIN_DIR="/tmp/bepinex/plugins"

if ! command -v dotnet >/dev/null; then
    echo ".NET SDK is required. Run .codex/install.sh first." >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$SCRIPT_DIR/RetroCamera.csproj"
dotnet build --no-restore -p:RunGenerateREADME=false -c Release "$PROJECT"

DLL_PATH="$SCRIPT_DIR/bin/Release/net6.0/RetroCamera.dll"

if [ ! -f "$DLL_PATH" ]; then
    echo "Build failed: $DLL_PATH not found." >&2
    exit 1
fi

cp "$DLL_PATH" "$BEPINEX_PLUGIN_DIR"
echo "Copied $(basename \"$DLL_PATH\") to $BEPINEX_PLUGIN_DIR"
