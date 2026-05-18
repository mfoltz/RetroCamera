param(
    [string]$BaseRef,

    [int]$LineThreshold = 120,

    [switch]$WarnOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent (Split-Path -Parent $ScriptRoot)
$ProjectPath = Join-Path $RepoRoot "RetroCamera.csproj"
$ChangelogPath = Join-Path $RepoRoot "CHANGELOG.md"

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Push-Location $RepoRoot
    try {
        & git -c core.autocrlf=false @Arguments
    }
    finally {
        Pop-Location
    }
}

function Write-Nudge {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if ($env:GITHUB_ACTIONS -eq "true") {
        Write-Host "::warning title=Release hygiene nudge::$Message"
    }
    else {
        Write-Warning $Message
    }

    $script:NudgeCount++
}

function Get-ProjectVersion {
    $Text = Get-Content -Raw -Path $ProjectPath
    $Match = [regex]::Match($Text, '<Version>(?<version>[^<]+)</Version>')

    if (-not $Match.Success) {
        return $null
    }

    return $Match.Groups["version"].Value.Trim()
}

function Get-LatestChangelogEntry {
    foreach ($Line in Get-Content -Path $ChangelogPath) {
        if ($Line -match '^`(?<version>\d+\.\d+\.\d+)`$') {
            return $Matches["version"]
        }
    }

    return $null
}

function Test-MeaningfulPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $NormalizedPath = $Path -replace '\\', '/'

    return $NormalizedPath -match '^(Behaviours|Configuration|Patches|Systems|Utilities)/' `
        -or $NormalizedPath -match '^Resources/[^/]+\.(cs|json|png)$' `
        -or $NormalizedPath -match '^\.codex/scripts/' `
        -or $NormalizedPath -match '^\.github/workflows/' `
        -or $NormalizedPath -match '^[^/]+\.(cs|csproj)$'
}

function Test-UsableGitRef {
    param(
        [string]$Ref
    )

    return -not [string]::IsNullOrWhiteSpace($Ref) -and $Ref -notmatch '^0+$'
}

if ([string]::IsNullOrWhiteSpace($BaseRef)) {
    $BaseRef = if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_BASE_REF)) {
        "origin/$($env:GITHUB_BASE_REF)"
    }
    elseif (Test-UsableGitRef $env:GITHUB_EVENT_BEFORE) {
        $env:GITHUB_EVENT_BEFORE
    }
    else {
        "HEAD^"
    }
}

$MergeBase = Invoke-Git -Arguments @("merge-base", "HEAD", $BaseRef) 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($MergeBase)) {
    Write-Host "release-nudge: unable to resolve merge-base for '$BaseRef'; skipping release hygiene gate."
    exit 0
}

$ChangedFiles = @(Invoke-Git -Arguments @("diff", "--name-only", $MergeBase) 2>$null)
$MeaningfulFiles = @($ChangedFiles | Where-Object { Test-MeaningfulPath $_ })

if ($MeaningfulFiles.Count -eq 0) {
    Write-Host "release-nudge: no meaningful source, script, or workflow changes detected."
    exit 0
}

$NumstatArguments = @("diff", "--numstat", $MergeBase, "--") + $MeaningfulFiles
$Numstat = @(Invoke-Git -Arguments $NumstatArguments 2>$null)
$ChangedLines = 0
foreach ($Line in $Numstat) {
    $Parts = $Line -split "`t"
    if ($Parts.Length -lt 2) {
        continue
    }

    $AddedLines = 0
    if ([int]::TryParse($Parts[0], [ref]$AddedLines)) {
        $ChangedLines += $AddedLines
    }

    $DeletedLines = 0
    if ([int]::TryParse($Parts[1], [ref]$DeletedLines)) {
        $ChangedLines += $DeletedLines
    }
}

$ProjectVersion = Get-ProjectVersion
$LatestChangelogEntry = Get-LatestChangelogEntry
$ChangelogChanged = @($ChangedFiles | Where-Object { $_ -eq "CHANGELOG.md" }).Count -gt 0
$script:NudgeCount = 0

if ([string]::IsNullOrWhiteSpace($ProjectVersion)) {
    Write-Nudge "RetroCamera.csproj does not expose a readable Version value."
}
elseif ([string]::IsNullOrWhiteSpace($LatestChangelogEntry)) {
    Write-Nudge "RetroCamera CHANGELOG.md does not contain a readable latest version entry."
}
elseif ($ProjectVersion -ne $LatestChangelogEntry -and -not $ChangelogChanged) {
    Write-Nudge "RetroCamera version metadata is $ProjectVersion, but the latest CHANGELOG.md entry is $LatestChangelogEntry. Add the matching human-owned changelog entry before release."
}
elseif ($ChangedLines -ge $LineThreshold -and -not $ChangelogChanged) {
    Write-Host "release-nudge: meaningful changes detected, and CHANGELOG.md remains human-owned with latest entry $LatestChangelogEntry."
}

if ($script:NudgeCount -eq 0) {
    Write-Host "release-nudge: no changelog or version metadata nudge needed."
}
elseif (-not $WarnOnly.IsPresent) {
    exit 1
}
