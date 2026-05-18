#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_FILE="${PROJECT_ROOT}/RetroCamera.csproj"
DESIRED_DOTNET_CHANNEL="${DOTNET_INSTALL_CHANNEL:-8.0}"
DOTNET_INSTALL_DIR="${PROJECT_ROOT}/.dotnet"
DOTNET_INSTALL_SCRIPT="${DOTNET_INSTALL_DIR}/dotnet-install.sh"

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required to download build dependencies." >&2
  exit 1
fi

DOTNET_CMD="dotnet"

if ! command -v "${DOTNET_CMD}" >/dev/null 2>&1; then
  DOTNET_CMD="${DOTNET_INSTALL_DIR}/dotnet"

  if [ -x "${DOTNET_CMD}" ]; then
    echo "dotnet CLI not found in PATH; reusing local installation at ${DOTNET_INSTALL_DIR}."
  else
    echo "dotnet CLI not found; installing into ${DOTNET_INSTALL_DIR}..."

    mkdir -p "${DOTNET_INSTALL_DIR}"

    if [ ! -f "${DOTNET_INSTALL_SCRIPT}" ]; then
      curl -sSL https://dot.net/v1/dotnet-install.sh -o "${DOTNET_INSTALL_SCRIPT}"
      chmod +x "${DOTNET_INSTALL_SCRIPT}"
    fi

    INSTALL_ARGS=(--install-dir "${DOTNET_INSTALL_DIR}")

    if [ -n "${DOTNET_INSTALL_VERSION:-}" ]; then
      INSTALL_ARGS+=(--version "${DOTNET_INSTALL_VERSION}")
    else
      INSTALL_ARGS+=(--channel "${DESIRED_DOTNET_CHANNEL}")
    fi

    "${DOTNET_INSTALL_SCRIPT}" "${INSTALL_ARGS[@]}"
  fi

  export DOTNET_ROOT="${DOTNET_INSTALL_DIR}"
  export PATH="${DOTNET_ROOT}:${PATH}"
else
  DOTNET_CMD="$(command -v dotnet)"
fi

if ! "${DOTNET_CMD}" --list-sdks | grep -q "^${DESIRED_DOTNET_CHANNEL}"; then
  echo "Ensuring .NET SDK channel ${DESIRED_DOTNET_CHANNEL} is available..."
  mkdir -p "${DOTNET_INSTALL_DIR}"
  if [ ! -f "${DOTNET_INSTALL_SCRIPT}" ]; then
    curl -sSL https://dot.net/v1/dotnet-install.sh -o "${DOTNET_INSTALL_SCRIPT}"
    chmod +x "${DOTNET_INSTALL_SCRIPT}"
  fi
  INSTALL_ARGS=(--install-dir "${DOTNET_INSTALL_DIR}")
  if [ -n "${DOTNET_INSTALL_VERSION:-}" ]; then
    INSTALL_ARGS+=(--version "${DOTNET_INSTALL_VERSION}")
  else
    INSTALL_ARGS+=(--channel "${DESIRED_DOTNET_CHANNEL}")
  fi
  "${DOTNET_INSTALL_SCRIPT}" "${INSTALL_ARGS[@]}"
  export DOTNET_ROOT="${DOTNET_INSTALL_DIR}"
  export PATH="${DOTNET_ROOT}:${PATH}"
  DOTNET_CMD="${DOTNET_INSTALL_DIR}/dotnet"
fi

RESTORE_SOURCES=(
  --source "https://api.nuget.org/v3/index.json"
  --source "https://nuget.bepinex.dev/v3/index.json"
)

echo "Using dotnet executable at: ${DOTNET_CMD}"

echo "Restoring NuGet packages..."
"${DOTNET_CMD}" restore "${PROJECT_FILE}" "${RESTORE_SOURCES[@]}"

BUILD_ARGS=(--configuration Release)

if [ -d "${PROJECT_ROOT}/.git" ]; then
  echo "Detected git repository; disabling README generation for local build."
  BUILD_ARGS+=(-p:RunGenerateREADME=false)
fi

echo "Building RetroCamera (${BUILD_ARGS[*]})..."
"${DOTNET_CMD}" build "${PROJECT_FILE}" "${BUILD_ARGS[@]}"
