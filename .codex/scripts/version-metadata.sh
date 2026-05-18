#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
CSPROJ_PATH="$REPO_ROOT/RetroCamera.csproj"
THUNDERSTORE_PATH="$REPO_ROOT/thunderstore.toml"
CHANGELOG_PATH="$REPO_ROOT/CHANGELOG.md"
CANONICAL_VERSION_PATTERN='^[0-9]+\.[0-9]+\.[0-9]+$'

fail() {
    local message="$1"
    echo "Error: $message" >&2
    exit 1
}

read_csproj_version() {
    local version
    version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$CSPROJ_PATH" | head -n 1 | tr -d '[:space:]')

    if [ -z "$version" ]; then
        fail "Unable to determine canonical version from $CSPROJ_PATH."
    fi

    printf '%s\n' "$version"
}

read_thunderstore_version() {
    local version
    version=$(sed -n 's/^versionNumber = "\([^"]*\)"$/\1/p' "$THUNDERSTORE_PATH" | head -n 1 | tr -d '[:space:]')

    if [ -z "$version" ]; then
        fail "Unable to determine Thunderstore version from $THUNDERSTORE_PATH."
    fi

    printf '%s\n' "$version"
}

read_latest_changelog_entry() {
    local entry
    entry=$(sed -n 's/^## v\([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\)$/\1/p' "$CHANGELOG_PATH" | head -n 1 | tr -d '\r')

    if [ -z "$entry" ]; then
        fail "Unable to determine the latest CHANGELOG entry from $CHANGELOG_PATH."
    fi

    printf '%s\n' "$entry"
}

validate_canonical_version_format() {
    local version="$1"
    local source_name="$2"

    if [[ ! "$version" =~ $CANONICAL_VERSION_PATTERN ]]; then
        fail "$source_name version '$version' must match canonical format X.Y.Z."
    fi
}

require_matching_version() {
    local source_name="$1"
    local source_version="$2"
    local canonical_version="$3"

    validate_canonical_version_format "$source_version" "$source_name"

    if [ "$source_version" != "$canonical_version" ]; then
        fail "$source_name version '$source_version' does not match canonical version '$canonical_version' from $CSPROJ_PATH."
    fi
}

emit_canonical_version() {
    local canonical_version="$1"

    if [ -n "${GITHUB_OUTPUT:-}" ]; then
        echo "canonical_version=$canonical_version" >> "$GITHUB_OUTPUT"
    fi

    echo "canonical_version=$canonical_version"
}

canonical_version=$(read_csproj_version)
validate_canonical_version_format "$canonical_version" "$CSPROJ_PATH"

require_matching_version "$THUNDERSTORE_PATH" "$(read_thunderstore_version)" "$canonical_version"
require_matching_version "$CHANGELOG_PATH latest entry" "$(read_latest_changelog_entry)" "$canonical_version"

emit_canonical_version "$canonical_version"
