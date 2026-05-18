Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PrereleaseNotesPath = Join-Path $ScriptRoot "prerelease-notes.sh"
$BashPath = "C:\Program Files\Git\bin\bash.exe"

if (-not (Test-Path -LiteralPath $BashPath)) {
    throw "Git Bash was not found at $BashPath."
}

function Assert-Match {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

function New-Fixture {
    $FixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("retrocamera-prerelease-notes-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $FixtureRoot | Out-Null
    return $FixtureRoot
}

function Invoke-PrereleaseNotes {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $BashPath $PrereleaseNotesPath @Arguments 2>&1 | Out-String
}

function Test-PrereleaseNotesIncludesChangelogAndDetailsCard {
    $FixtureRoot = New-Fixture
    $PreviousRepository = $env:GITHUB_REPOSITORY
    try {
        $env:GITHUB_REPOSITORY = "mfoltz/RetroCamera"
        $ChangelogPath = Join-Path $FixtureRoot "CHANGELOG.md"
        $OutputPath = Join-Path $FixtureRoot "prerelease-notes.md"
        Set-Content -Path $ChangelogPath -Value @'
# Changelog

## Unreleased

## v1.2.3

- fixed the camera timing
- added release validation

## v1.2.2

- previous release
'@

        $Output = Invoke-PrereleaseNotes -Arguments @(
            "--changelog", $ChangelogPath,
            "--version", "1.2.3",
            "--tag", "v1.2.3-pre",
            "--branch", "main",
            "--commit", "1234567890abcdef",
            "--run-id", "42",
            "--output", $OutputPath)
        if ($LASTEXITCODE -ne 0) {
            throw "prerelease-notes.sh exited with $LASTEXITCODE`n$Output"
        }

        $Notes = Get-Content -Raw -Path $OutputPath
        if ($Notes -match '<details') {
            throw "Release notes should keep the Thunderstore handoff card visible without a dropdown."
        }

        Assert-Match -Text $Notes -Pattern '<img src="https://raw\.githubusercontent\.com/mfoltz/RetroCamera/1234567890abcdef/\.github/assets/ts_badge\.png"' -Message "Release notes did not include the Thunderstore badge image."
        Assert-Match -Text $Notes -Pattern '## Unreleased.*empty' -Message "Release notes did not describe changelog turnover."
        Assert-Match -Text $Notes -Pattern 'Branch.*main' -Message "Release notes did not include the branch cue."
        Assert-Match -Text $Notes -Pattern 'Commit.*1234567890ab' -Message "Release notes did not include the short commit."
        Assert-Match -Text $Notes -Pattern 'Run.*42' -Message "Release notes did not include the workflow run cue."
        Assert-Match -Text $Notes -Pattern 'Tag.*v1\.2\.3-pre' -Message "Release notes did not include the tag cue."
        Assert-Match -Text $Notes -Pattern 'Package.*1\.2\.3' -Message "Release notes did not include the package version."
        Assert-Match -Text $Notes -Pattern 'fixed the camera timing' -Message "Release notes did not include current version changelog notes."
    }
    finally {
        $env:GITHUB_REPOSITORY = $PreviousRepository
        Remove-Item -Recurse -Force -LiteralPath $FixtureRoot
    }
}

function Test-PrereleaseNotesRejectsMissingVersionEntry {
    $FixtureRoot = New-Fixture
    try {
        $ChangelogPath = Join-Path $FixtureRoot "CHANGELOG.md"
        Set-Content -Path $ChangelogPath -Value @'
# Changelog

## Unreleased

## v1.2.2

- previous release
'@

        $Output = Invoke-PrereleaseNotes -Arguments @(
            "--changelog", $ChangelogPath,
            "--version", "1.2.3",
            "--check-only")
        if ($LASTEXITCODE -eq 0) {
            throw "prerelease-notes.sh unexpectedly accepted a missing version entry."
        }

        Assert-Match -Text $Output -Pattern "does not contain notes for '1\.2\.3'" -Message "Missing version rejection message was not specific."
    }
    finally {
        Remove-Item -Recurse -Force -LiteralPath $FixtureRoot
    }
}

function Test-ReleaseWorkflowChecksOnlyDownloadedReleaseChangelog {
    $WorkflowPath = Join-Path (Split-Path -Parent (Split-Path -Parent $ScriptRoot)) ".github/workflows/release.yml"
    $WorkflowText = Get-Content -Raw -Path $WorkflowPath

    $PreserveMarker = "      - name: Preserve release helper scripts"
    $DownloadMarker = "      - name: Download Release"
    $DownloadedChangelogMarker = "      - name: Validate downloaded release changelog"

    $PreserveIndex = $WorkflowText.IndexOf($PreserveMarker, [StringComparison]::Ordinal)
    $DownloadIndex = $WorkflowText.IndexOf($DownloadMarker, [StringComparison]::Ordinal)
    $DownloadedChangelogIndex = $WorkflowText.IndexOf($DownloadedChangelogMarker, [StringComparison]::Ordinal)

    if ($PreserveIndex -lt 0) {
        throw "release.yml is missing the Preserve release helper scripts step."
    }

    if ($DownloadIndex -lt 0) {
        throw "release.yml is missing the Download Release step."
    }

    if ($DownloadedChangelogIndex -lt 0) {
        throw "release.yml is missing downloaded release changelog validation."
    }

    if ($DownloadIndex -lt $PreserveIndex) {
        throw "Release helper scripts must be preserved before downloading the selected release."
    }

    if ($DownloadedChangelogIndex -lt $DownloadIndex) {
        throw "Downloaded release changelog validation must run after the release download."
    }

    $BeforeDownload = $WorkflowText.Substring(0, $DownloadIndex)
    if ($BeforeDownload -match 'prerelease-notes\.sh[\s\S]*--check-only') {
        throw "release.yml must not run prerelease-notes.sh --check-only against the checkout before downloading the selected release tag."
    }

    if ($WorkflowText -notmatch '\$RUNNER_TEMP/prerelease-notes\.sh') {
        throw "release.yml should run changelog validation from the preserved helper script."
    }
}

Test-PrereleaseNotesIncludesChangelogAndDetailsCard
Test-PrereleaseNotesRejectsMissingVersionEntry
Test-ReleaseWorkflowChecksOnlyDownloadedReleaseChangelog

Write-Host "prerelease-notes tests passed"
