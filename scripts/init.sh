#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_FILE="${PROJECT_ROOT}/RetroCamera.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet CLI is required to build RetroCamera." >&2
  echo "Install the .NET SDK 6.0 or later and re-run this script." >&2
  exit 1
fi

RESTORE_SOURCES=(
  --source "https://api.nuget.org/v3/index.json"
  --source "https://nuget.bepinex.dev/v3/index.json"
)

echo "Restoring NuGet packages..."
dotnet restore "${PROJECT_FILE}" "${RESTORE_SOURCES[@]}"

BUILD_ARGS=(--configuration Release)

if [ -d "${PROJECT_ROOT}/.git" ]; then
  echo "Detected git repository; disabling README generation for local build."
  BUILD_ARGS+=(-p:RunGenerateREADME=false)
fi

echo "Building RetroCamera (${BUILD_ARGS[*]})..."
dotnet build "${PROJECT_FILE}" "${BUILD_ARGS[@]}"
