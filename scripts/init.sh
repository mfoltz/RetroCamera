#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_FILE="${PROJECT_ROOT}/RetroCamera.csproj"
THIRD_PARTY_DIR="${PROJECT_ROOT}/third_party"
GAMEDATA_DLL="${THIRD_PARTY_DIR}/VRising.GameData.dll"
GAMEDATA_URL="${VRISING_GAMEDATA_URL:-https://thunderstore.io/package/download/adainrivers/VRising_GameData/0.2.2/}"
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

mkdir -p "${THIRD_PARTY_DIR}"

if [ ! -f "${GAMEDATA_DLL}" ]; then
  echo "Fetching VRising.GameData.dll from ${GAMEDATA_URL}..."
  TMP_ZIP="$(mktemp)"
  curl -fSL "${GAMEDATA_URL}" -o "${TMP_ZIP}"
  unzip -qjo "${TMP_ZIP}" "VRising.GameData.dll" -d "${THIRD_PARTY_DIR}"
  rm -f "${TMP_ZIP}"
fi

if [ -n "${VRISING_REFERENCE_ARCHIVE:-}" ]; then
  echo "Extracting additional reference assemblies from ${VRISING_REFERENCE_ARCHIVE}..."
  ARCHIVE_PATH="${VRISING_REFERENCE_ARCHIVE}"
  CLEANUP_ARCHIVE=0
  if [[ "${VRISING_REFERENCE_ARCHIVE}" =~ ^https?:// ]]; then
    ARCHIVE_PATH="$(mktemp)"
    CLEANUP_ARCHIVE=1
    curl -fSL "${VRISING_REFERENCE_ARCHIVE}" -o "${ARCHIVE_PATH}"
  fi
  unzip -qjo "${ARCHIVE_PATH}" "*.dll" -d "${THIRD_PARTY_DIR}" || {
    echo "Failed to extract reference archive." >&2
    [ "${CLEANUP_ARCHIVE}" -eq 1 ] && rm -f "${ARCHIVE_PATH}"
    exit 1
  }
  [ "${CLEANUP_ARCHIVE}" -eq 1 ] && rm -f "${ARCHIVE_PATH}"
fi

if [ ! -f "${GAMEDATA_DLL}" ]; then
  echo "Failed to obtain VRising.GameData.dll. Set VRISING_GAMEDATA_URL to a valid download." >&2
  exit 1
fi

# Track any managed VRising assemblies that are still absent after the automated
# downloads so we can gracefully skip the build instead of hard failing. This
# keeps the init script useful in CI containers that do not have the proprietary
# archives while still surfacing actionable guidance for developers who do.
MISSING_GAME_ASSEMBLIES=()

if [ ! -f "${THIRD_PARTY_DIR}/ProjectM.dll" ]; then
  MISSING_GAME_ASSEMBLIES+=("ProjectM.dll")
fi

RESTORE_SOURCES=(
  --source "https://api.nuget.org/v3/index.json"
  --source "https://nuget.bepinex.dev/v3/index.json"
)

echo "Using dotnet executable at: ${DOTNET_CMD}"

echo "Restoring NuGet packages..."
"${DOTNET_CMD}" restore "${PROJECT_FILE}" "${RESTORE_SOURCES[@]}"

if [ "${#MISSING_GAME_ASSEMBLIES[@]}" -ne 0 ]; then
  cat <<EOF

WARNING: The following managed VRising assemblies are still missing from
${THIRD_PARTY_DIR}: ${MISSING_GAME_ASSEMBLIES[*]}.

The init script will skip the build so automated environments without access to
the proprietary VRising client data can still run dependency setup. To perform
a full build, download the game's managed DLLs (for example by extracting them
from a VRising installation or the dedicated server) and place them in
${THIRD_PARTY_DIR}, or set VRISING_REFERENCE_ARCHIVE to a zip/URL containing
those assemblies before rerunning this script.
EOF
  exit 0
fi

BUILD_ARGS=(--configuration Release)

if [ -d "${PROJECT_ROOT}/.git" ]; then
  echo "Detected git repository; disabling README generation for local build."
  BUILD_ARGS+=(-p:RunGenerateREADME=false)
fi

echo "Building RetroCamera (${BUILD_ARGS[*]})..."
"${DOTNET_CMD}" build "${PROJECT_FILE}" "${BUILD_ARGS[@]}"
