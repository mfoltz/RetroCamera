param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent (Split-Path -Parent $ScriptRoot)

$ProjectPath = Join-Path $RepoRoot "RetroCamera.csproj"
$ThunderstorePath = Join-Path $RepoRoot "thunderstore.toml"

function Set-TextPreservingUtf8Bom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $Bytes = [System.IO.File]::ReadAllBytes($Path)
    $HasUtf8Bom = $Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF
    $Encoding = [System.Text.UTF8Encoding]::new($HasUtf8Bom)
    [System.IO.File]::WriteAllText($Path, $Value, $Encoding)
}

function Update-FirstMatch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter(Mandatory = $true)]
        [string]$Replacement,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $Text = Get-Content -Raw -Path $Path
    $Regex = [regex]::new($Pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $Match = $Regex.Match($Text)
    if (-not $Match.Success) {
        throw "Unable to update $Description in $Path."
    }

    $Updated = $Text.Substring(0, $Match.Index) + $Regex.Replace($Match.Value, $Replacement, 1) + $Text.Substring($Match.Index + $Match.Length)
    Set-TextPreservingUtf8Bom -Path $Path -Value $Updated
}

Update-FirstMatch `
    -Path $ProjectPath `
    -Pattern '<Version>[^<]+</Version>' `
    -Replacement "<Version>$Version</Version>" `
    -Description "project version"

Update-FirstMatch `
    -Path $ThunderstorePath `
    -Pattern '^versionNumber = "[^"]+"' `
    -Replacement "versionNumber = `"$Version`"" `
    -Description "Thunderstore version"

Write-Host "Updated RetroCamera project and Thunderstore version metadata to $Version."
Write-Host "CHANGELOG.md was not changed; add the matching version entry manually before release validation."
