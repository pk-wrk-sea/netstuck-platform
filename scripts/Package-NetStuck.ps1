[CmdletBinding()]
param(
    [string]$Version = '1.2.3',
    [string]$PlinkPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$releaseRoot = Join-Path $artifactsRoot 'release'
$stage = Join-Path $releaseRoot ("NetStuck-v.$Version")
$zip = "$stage.zip"
$expectedPlinkSha256 = '06861c22056919216f925892334ba29b4a2848a7a09c3611540b16e993fd6cc3'

if (-not $PlinkPath) {
    $PlinkPath = Join-Path $repoRoot 'tools\plink.exe'
}
if (-not (Test-Path -LiteralPath $PlinkPath)) {
    throw "plink.exe was not found. Pass -PlinkPath or place PuTTY 0.80 at tools\plink.exe."
}
$plinkHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $PlinkPath).Hash.ToLowerInvariant()
if ($plinkHash -ne $expectedPlinkSha256) {
    throw "Unexpected plink.exe SHA256: $plinkHash"
}

& (Join-Path $PSScriptRoot 'Test-NetStuck.ps1') -SoakSeconds 10
$builtExe = Join-Path $artifactsRoot 'build\NetStuck.exe'
$fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($builtExe).FileVersion
if ($fileVersion -ne "$Version.0") {
    throw "Executable version $fileVersion does not match requested package version $Version."
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
foreach ($target in @($stage, $zip)) {
    $full = [System.IO.Path]::GetFullPath($target)
    if (-not $full.StartsWith([System.IO.Path]::GetFullPath($releaseRoot), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe release target: $full"
    }
    if (Test-Path -LiteralPath $full) {
        Remove-Item -LiteralPath $full -Recurse -Force
    }
}

New-Item -ItemType Directory -Path (Join-Path $stage 'tools') -Force | Out-Null
Copy-Item -LiteralPath $builtExe -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\NetStuck\assets\netstuck-bright-icon.png') -Destination (Join-Path $stage 'NetStuck-Icon.png')
Copy-Item -LiteralPath $PlinkPath -Destination (Join-Path $stage 'tools\plink.exe')
Copy-Item -LiteralPath (Join-Path $repoRoot 'third-party\PuTTY-LICENCE.txt') -Destination (Join-Path $stage 'tools\PuTTY-LICENCE.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'README-TH.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\releases\v1.2.3\TEST-REPORT.txt') -Destination $stage

$manifest = Join-Path $stage 'SHA256SUMS.txt'
$entries = Get-ChildItem -LiteralPath $stage -Recurse -File |
    Where-Object { $_.FullName -ne $manifest } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
        "$hash  $relative"
    }
[System.IO.File]::WriteAllLines($manifest, $entries, [System.Text.UTF8Encoding]::new($false))
Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal

Write-Output "Package: $zip"
Write-Output "SHA256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant())"
