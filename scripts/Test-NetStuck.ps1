[CmdletBinding()]
param(
    [ValidateRange(10, 28800)]
    [int]$SoakSeconds = 10
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src\NetStuck'
$testRoot = Join-Path $repoRoot 'tests'
$outputRoot = Join-Path $repoRoot 'artifacts\test'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework compiler was not found: $compiler"
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

function Invoke-Compiler {
    param([string[]]$Arguments)
    & $compiler @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "C# compilation failed with exit code $LASTEXITCODE."
    }
}

function Invoke-TestExecutable {
    param(
        [string]$Name,
        [string[]]$Arguments = @()
    )
    Write-Output "`n=== $Name ==="
    & (Join-Path $outputRoot $Name) @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

$appSources = @(
    'NetOpsCore.cs',
    'NetStuck.cs',
    'NetStuck.Features.cs',
    'NetStuck.Release1.cs',
    'NetStuck.V103.cs'
) | ForEach-Object { Join-Path $sourceRoot $_ }
$frameworkReferences = @(
    'System.dll',
    'System.Core.dll',
    'System.Data.dll',
    'System.Data.DataSetExtensions.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll'
) | ForEach-Object { "/reference:$_" }
$uiLibrary = Join-Path $outputRoot 'NetStuck.UI.dll'

Invoke-Compiler -Arguments (@('/nologo', '/target:library', '/optimize+', "/out:$uiLibrary") + $frameworkReferences + $appSources)
Invoke-Compiler -Arguments @('/nologo', '/target:exe', '/optimize+', "/out:$(Join-Path $outputRoot 'FakePlink.exe')", '/reference:System.dll', '/reference:System.Core.dll', (Join-Path $testRoot 'FakePlink.cs'))
Invoke-Compiler -Arguments @('/nologo', '/target:exe', '/optimize+', "/out:$(Join-Path $outputRoot 'NetOpsCoreTests.exe')", '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Data.dll', (Join-Path $sourceRoot 'NetOpsCore.cs'), (Join-Path $testRoot 'NetOpsCoreTests.cs'))

$uiTestReferences = $frameworkReferences + "/reference:$uiLibrary"
foreach ($name in @('FeatureTests', 'PerformanceTests', 'PollingCadenceTests', 'OvernightSoakTests')) {
    Invoke-Compiler -Arguments (@('/nologo', '/target:exe', '/optimize+', "/out:$(Join-Path $outputRoot ($name + '.exe'))") + $uiTestReferences + (Join-Path $testRoot ($name + '.cs')))
}

& (Join-Path $PSScriptRoot 'Build-NetStuck.ps1') -OutputDirectory (Join-Path $repoRoot 'artifacts\build')

Push-Location $outputRoot
try {
    Invoke-TestExecutable 'NetOpsCoreTests.exe'
    Invoke-TestExecutable 'FeatureTests.exe'
    Invoke-TestExecutable 'PerformanceTests.exe'
    Invoke-TestExecutable 'PollingCadenceTests.exe'
    Invoke-TestExecutable 'OvernightSoakTests.exe' @('--seconds', $SoakSeconds.ToString())
}
finally {
    Pop-Location
}

Write-Output "`nAll NetStuck test suites passed."
