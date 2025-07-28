#!/usr/bin/env bash
set -e

PLUGIN_DIR="/path/to/BepInEx/plugins"

while [[ $# -gt 0 ]]; do
  case $1 in
    --plugin-dir)
      PLUGIN_DIR="$2"
      shift 2
      ;;
    *)
      echo "Usage: $0 [--plugin-dir <path>]" >&2
      exit 1
      ;;
  esac
done

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
REPO_NAME="$(basename "$REPO_ROOT")"

mkdir -p "$REPO_ROOT/.codex"
mkdir -p "$REPO_ROOT/.project-management/closed-prd"
mkdir -p "$REPO_ROOT/.project-management/current-prd"

cat > "$REPO_ROOT/.codex/install.sh" <<EOS
#!/usr/bin/env bash
set -e
SCRIPT_DIR="\$(cd \"\$(dirname \"$0\")\" && pwd)"
ROOT_DIR="\$(cd "\$SCRIPT_DIR/.." && pwd)"
DOTNET_VERSION="6.0"

if ! command -v dotnet >/dev/null; then
    echo "Installing .NET \$DOTNET_VERSION SDK..."
    curl -sSL https://dot.net/v1/dotnet-install.sh -o "\$SCRIPT_DIR/dotnet-install.sh"
    bash "\$SCRIPT_DIR/dotnet-install.sh" --channel "\$DOTNET_VERSION"
    export PATH="\$HOME/.dotnet:\$PATH"
fi

dotnet restore "\$ROOT_DIR/$REPO_NAME.csproj"
EOS
chmod +x "$REPO_ROOT/.codex/install.sh"

cat > "$REPO_ROOT/dev_init.sh" <<EOS
#!/usr/bin/env bash
set -e
BEPINEX_PLUGIN_DIR="$PLUGIN_DIR"

if ! command -v dotnet >/dev/null; then
    echo ".NET SDK is required. Run .codex/install.sh first." >&2
    exit 1
fi

PROJECT="\$(dirname \"$0\")/$REPO_NAME.csproj"
dotnet build --no-restore -p:RunGenerateREADME=false "\$PROJECT"

DLL_PATH="\$(dirname \"$0\")/bin/Release/net6.0/$REPO_NAME.dll"

if [ ! -f "\$DLL_PATH" ]; then
    echo "Build failed: \$DLL_PATH not found." >&2
    exit 1
fi

cp "\$DLL_PATH" "\$BEPINEX_PLUGIN_DIR"
echo "Copied \$(basename \"\$DLL_PATH\") to \$BEPINEX_PLUGIN_DIR"
EOS
chmod +x "$REPO_ROOT/dev_init.sh"

cat > "$REPO_ROOT/.project-management/create-prd.md" <<'EOS'
# Creating a Product Requirement Document

1. Summarize the problem and objectives.
2. Outline features and acceptance criteria.
3. Provide timeline estimates and stakeholders.
EOS

cat > "$REPO_ROOT/.project-management/create-task-list.md" <<'EOS'
# Creating a Task List

1. Break the PRD into actionable tasks.
2. Assign owners and due dates.
3. Track progress in the `current-prd` folder.
EOS

cat > "$REPO_ROOT/.project-management/process-tasks.md" <<'EOS'
# Processing Tasks

1. Review open tasks regularly.
2. Update status and communicate blockers.
3. Move completed items to `closed-prd`.
EOS

cat > "$REPO_ROOT/.project-management/close-prd.md" <<'EOS'
# Closing a Product Requirement

1. Ensure all tasks are complete.
2. Summarize results and lessons learned.
3. Move documentation to `closed-prd`.
EOS

if [ ! -f "$REPO_ROOT/NuGet.config" ]; then
cat > "$REPO_ROOT/NuGet.config" <<'EOS'
<configuration>
  <packageSources>
    <add key="NuGet official" value="https://api.nuget.org/v3/index.json" />
    <add key="BepInEx" value="https://nuget.bepinex.dev/v3/index.json" />
  </packageSources>
</configuration>
EOS
fi

if [ -f "$REPO_ROOT/README.md" ] && ! grep -q "## Codex Workflow" "$REPO_ROOT/README.md"; then
cat >> "$REPO_ROOT/README.md" <<EOS

## Codex Workflow

1. Run \`.codex/install.sh\` once to install dependencies
2. Build and deploy locally with \`./dev_init.sh\`
3. Update message hashes using:
   \`dotnet run --project $REPO_NAME.csproj -p:RunGenerateREADME=false -- generate-messages .\`
4. Use the keywords (**CreatePrd**, **CreateTasks**, **TaskMaster**, **ClosePrd**) to manage PRDs and tasks

Current PRDs and task lists are stored in \`.project-management/current-prd/\`, while completed items are moved to \`.project-management/closed-prd/\`.
EOS
fi

echo "Codex bootstrap complete for $REPO_NAME."