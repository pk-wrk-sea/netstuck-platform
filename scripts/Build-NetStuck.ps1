[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src\NetStuck'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\build'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework compiler was not found: $compiler"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = Join-Path $OutputDirectory 'NetStuck.exe'
$icon = Join-Path $sourceRoot 'assets\netstuck-bright.ico'
$sources = @(
    'NetOpsCore.cs',
    'NetStuck.cs',
    'NetStuck.Features.cs',
    'NetStuck.Release1.cs',
    'NetStuck.V103.cs'
) | ForEach-Object { Join-Path $sourceRoot $_ }
$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Data.dll',
    'System.Data.DataSetExtensions.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll'
) | ForEach-Object { "/reference:$_" }

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    "/out:$output",
    "/win32icon:$icon"
) + $references + $sources

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "NetStuck compilation failed with exit code $LASTEXITCODE."
}

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($output).FileVersion
Write-Output "Built: $output"
Write-Output "File version: $version"
