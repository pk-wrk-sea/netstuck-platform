[CmdletBinding()]
param(
    [string]$Version = '1.3.0',
    [string]$PlinkPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'NetStuck.BuildProvenance.ps1')
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$releaseRoot = Join-Path $artifactsRoot 'release'
$stage = Join-Path $releaseRoot ("NetStuck-v.$Version")
$zip = "$stage.zip"
$packageProvenancePath = "$stage.provenance.json"
$expectedPlinkSha256 = '06861c22056919216f925892334ba29b4a2848a7a09c3611540b16e993fd6cc3'
$compiler = Resolve-NetStuckCompilerPath
$expectedPackageInputs = @(
    'CHANGELOG.md',
    'NetStuck-Icon.png',
    'NetStuck.exe',
    'README-TH.md',
    'README.md',
    'TEST-REPORT.txt',
    'tools/plink.exe',
    'tools/PuTTY-LICENCE.txt'
)
$expectedPackageContent = @($expectedPackageInputs + 'SHA256SUMS.txt')

function Get-PackageInventory {
    param([string]$RootPath, [string[]]$RelativePaths, [string]$Role)
    $specifications = @($RelativePaths | ForEach-Object { [pscustomobject]@{ Role = $Role; RelativePath = $_ } })
    return @(Get-NetStuckFileInventory -RootPath $RootPath -Specifications $specifications -SkipTracking)
}

function Get-PackageRelativePaths {
    param([string]$RootPath)
    $root = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\', '/')
    return @(Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object {
        ConvertTo-NetStuckRelativePath $_.FullName.Substring($root.Length + 1)
    })
}

function Assert-PackageManifest {
    param([string]$RootPath, [string[]]$InputPaths)
    $manifestPath = Join-Path $RootPath 'SHA256SUMS.txt'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Package SHA256SUMS.txt is missing.' }
    $lines = @(Get-Content -LiteralPath $manifestPath | Where-Object { $_.Length -gt 0 })
    if ($lines.Count -ne $InputPaths.Count) { throw "Package manifest count mismatch: expected $($InputPaths.Count), actual $($lines.Count)." }
    foreach ($relative in $InputPaths) {
        $path = Join-Path $RootPath ($relative.Replace('/', '\'))
        $hash = Get-NetStuckSha256 -Path $path
        $expectedLine = "$hash  $relative"
        if (@($lines | Where-Object { $_ -ceq $expectedLine }).Count -ne 1) { throw "Package manifest mismatch for $relative." }
    }
}

if (-not $PlinkPath) { $PlinkPath = Join-Path $repoRoot 'tools\plink.exe' }
if (-not (Test-Path -LiteralPath $PlinkPath -PathType Leaf)) {
    throw "plink.exe was not found. Pass -PlinkPath or place PuTTY 0.80 at tools\plink.exe."
}
$plinkHash = Get-NetStuckSha256 -Path $PlinkPath
if ($plinkHash -ne $expectedPlinkSha256) { throw "Unexpected plink.exe SHA256: $plinkHash" }

$builtExe = Join-Path $artifactsRoot 'build\NetStuck.exe'
$preBuildProvenance = Get-NetStuckBuildProvenance -RepositoryRoot $repoRoot -OutputPath $builtExe -CompilerPath $compiler
$testSummaryPath = Join-Path $artifactsRoot 'test\package-test-summary.json'
& (Join-Path $PSScriptRoot 'Test-NetStuck.ps1') -SoakSeconds 10 -SummaryPath $testSummaryPath
if (-not (Test-Path -LiteralPath $testSummaryPath -PathType Leaf)) { throw "Current test summary was not produced: $testSummaryPath" }
$testSummary = Get-Content -LiteralPath $testSummaryPath -Raw | ConvertFrom-Json
if ([int]$testSummary.SchemaVersion -ne 3 -or
    $testSummary.Status -ne 'Passed' -or
    $testSummary.Verdict -ne 'PASS' -or
    [int]$testSummary.Totals.Discovered -le 0 -or
    [int]$testSummary.Totals.Failed -ne 0 -or
    [int]$testSummary.Totals.Skipped -ne 0 -or
    [int]$testSummary.Totals.InfrastructureFailures -ne 0 -or
    [int]$testSummary.Totals.CompletedRequiredSuites -ne [int]$testSummary.Totals.RequiredSuites -or
    -not [bool]$testSummary.Reconciliation.InventoryMatch -or
    [int]$testSummary.Reconciliation.ExitCode -ne 0 -or
    @($testSummary.Suites | Where-Object {
        $_.Status -ne 'Passed' -or -not $_.InvocationSucceeded -or [int]$_.NativeExitCode -ne 0 -or
        -not [bool]$_.FloorSatisfied -or [int]$_.Discovered -lt [int]$_.MinimumExpected
    }).Count -ne 0 -or
    @($testSummary.Stages | Where-Object { $_.Status -ne 'Passed' -or [int]$_.InfrastructureFailures -ne 0 }).Count -ne 0) {
    throw 'Current test summary is not a passing run.'
}

$buildProvenancePath = Join-Path $artifactsRoot 'build\NetStuck.build-provenance.json'
if (-not (Test-Path -LiteralPath $builtExe -PathType Leaf) -or -not (Test-Path -LiteralPath $buildProvenancePath -PathType Leaf)) {
    throw 'Development build or its provenance record is missing after canonical verification.'
}
$buildRecord = Get-Content -LiteralPath $buildProvenancePath -Raw | ConvertFrom-Json
$postBuildProvenance = Get-NetStuckBuildProvenance -RepositoryRoot $repoRoot -OutputPath $builtExe -CompilerPath $compiler
foreach ($property in @('SourceInputFingerprint','ToolchainFingerprint','BuildInvocationFingerprint','ActualCompilerArgumentFingerprint','ReferenceInputFingerprint')) {
    if ([string]$buildRecord.$property -ne [string]$postBuildProvenance.$property) { throw "Build provenance record mismatch: $property" }
}
if ($preBuildProvenance.SourceInputFingerprint -ne $postBuildProvenance.SourceInputFingerprint) {
    throw "Source inputs changed during verification: before=$($preBuildProvenance.SourceInputFingerprint) after=$($postBuildProvenance.SourceInputFingerprint)"
}
$fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($builtExe).FileVersion
if ($fileVersion -ne "$Version.0") { throw "Executable version $fileVersion does not match requested package version $Version." }
$builtExeHash = Get-NetStuckSha256 -Path $builtExe
if ($builtExeHash -ne [string]$buildRecord.Output.Sha256) { throw 'Built executable hash does not match its build provenance record.' }

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
foreach ($target in @($stage, $zip, $packageProvenancePath)) {
    $full = [System.IO.Path]::GetFullPath($target)
    $releaseFull = [System.IO.Path]::GetFullPath($releaseRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($releaseFull, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe release target: $full" }
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
}

New-Item -ItemType Directory -Path (Join-Path $stage 'tools') -Force | Out-Null
Copy-Item -LiteralPath $builtExe -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\NetStuck\assets\netstuck-bright-icon.png') -Destination (Join-Path $stage 'NetStuck-Icon.png')
Copy-Item -LiteralPath $PlinkPath -Destination (Join-Path $stage 'tools\plink.exe')
Copy-Item -LiteralPath (Join-Path $repoRoot 'third-party\PuTTY-LICENCE.txt') -Destination (Join-Path $stage 'tools\PuTTY-LICENCE.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'README-TH.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Destination $stage

$branch = (& git -C $repoRoot rev-parse --abbrev-ref HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve the current Git branch for the package report.' }
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve the current Git HEAD for the package report.' }
$diffFingerprint = Get-NetStuckBinarySafeGitDiffFingerprint -RepositoryRoot $repoRoot
$stagedDiffFingerprint = Get-NetStuckBinarySafeGitDiffFingerprint -RepositoryRoot $repoRoot -Cached

$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add("NetStuck v.$Version - Current Verification Report")
$reportLines.Add(('=' * 48))
$reportLines.Add('')
$reportLines.Add('Build and source state')
$reportLines.Add('----------------------')
$reportLines.Add('')
$reportLines.Add('Build target: Windows .NET Framework 4.x, AnyCPU, optimized WinExe')
$reportLines.Add("Assembly/File version: $fileVersion")
$reportLines.Add("Executable SHA256: $builtExeHash")
$reportLines.Add("Verification generated UTC: $($testSummary.GeneratedUtc)")
$reportLines.Add("Current branch: $branch")
$reportLines.Add("HEAD: $head")
$reportLines.Add("Tracked unstaged diff fingerprint (Git blob): $diffFingerprint")
$reportLines.Add("Tracked staged diff fingerprint (Git blob): $stagedDiffFingerprint")
$reportLines.Add("SOURCE_INPUT_FINGERPRINT: $($postBuildProvenance.SourceInputFingerprint)")
$reportLines.Add("TOOLCHAIN_FINGERPRINT: $($postBuildProvenance.ToolchainFingerprint)")
$reportLines.Add("BUILD_INVOCATION_FINGERPRINT: $($postBuildProvenance.BuildInvocationFingerprint)")
$reportLines.Add("REFERENCE_INPUT_FINGERPRINT: $($postBuildProvenance.ReferenceInputFingerprint)")
$reportLines.Add("Compiler: $($postBuildProvenance.Compiler.Path)")
$reportLines.Add("Compiler version: $($postBuildProvenance.Compiler.Version)")
$reportLines.Add("Compiler SHA256: $($postBuildProvenance.Compiler.Sha256)")
$reportLines.Add("Compiler runtime: $($postBuildProvenance.Runtime.CompilerFramework); CLR $($postBuildProvenance.Runtime.CompilerClrVersion)")
$reportLines.Add("Build PowerShell host: $($buildRecord.PowerShellHost.Edition) $($buildRecord.PowerShellHost.Version) [$([System.IO.Path]::GetFileName([string]$buildRecord.PowerShellHost.Executable))]")
$reportLines.Add('CSC.RSP disabled: true (/noconfig)')
$reportLines.Add('Default standard library disabled: true (/nostdlib+)')
$reportLines.Add('Normalized compiler arguments:')
foreach ($argument in $postBuildProvenance.NormalizedCompilerArguments) { $reportLines.Add("- $argument") }
$reportLines.Add('Repository source/recipe inputs:')
foreach ($input in ($postBuildProvenance.SourceInputs | Sort-Object RelativePath)) {
    $tracking = if ($input.Tracked) { 'tracked' } else { 'untracked' }
    $reportLines.Add("- [$($input.Role)] $($input.RelativePath); $tracking; $($input.Size) bytes; SHA256 $($input.Sha256)")
}
$reportLines.Add('Explicit framework reference inputs:')
foreach ($input in ($postBuildProvenance.ReferenceInputs | Sort-Object RelativePath)) {
    $reportLines.Add("- $($input.RelativePath); $($input.Size) bytes; SHA256 $($input.Sha256); version $($input.Version)")
}
$reportLines.Add('Toolchain inputs:')
foreach ($input in ($postBuildProvenance.ToolchainInputs | Sort-Object RelativePath)) {
    $reportLines.Add("- $($input.RelativePath); $($input.Size) bytes; SHA256 $($input.Sha256); version $($input.Version)")
}
$reportLines.Add('')
$reportLines.Add('Current automated verification')
$reportLines.Add('------------------------------')
$reportLines.Add('')
$reportLines.Add("Command: $($testSummary.Command)")
$reportLines.Add("PowerShell host: $($testSummary.Host.Edition) $($testSummary.Host.Version) [$([System.IO.Path]::GetFileName([string]$testSummary.Host.Executable))]")
$reportLines.Add("Mandatory suite inventory: $($testSummary.Totals.CompletedRequiredSuites)/$($testSummary.Totals.RequiredSuites)")
foreach ($suite in $testSummary.Suites) {
    $reportLines.Add("- $($suite.Name): $($suite.Passed)/$($suite.Discovered) passed; floor $($suite.MinimumExpected) satisfied=$($suite.FloorSatisfied); $($suite.Failed) failed; $($suite.Skipped) skipped; infrastructure $($suite.InfrastructureFailures); native exit $($suite.NativeExitCode)")
}
$reportLines.Add("- Total: $($testSummary.Totals.Passed)/$($testSummary.Totals.Discovered) passed; $($testSummary.Totals.Failed) failed; $($testSummary.Totals.Skipped) skipped; $($testSummary.Totals.InfrastructureFailures) infrastructure failures")
$reportLines.Add('- Build: passed with explicit production inputs and compiler provenance')
$reportLines.Add('- Capture infrastructure gate: exact transaction rollback, PNG decode/dimensions/structure, metadata privacy, stale-output and cleanup negative paths passed')
$reportLines.Add('- Canonical screenshot determinism is a separate serial multi-run gate and is not inferred from this package run.')
$reportLines.Add('')
$reportLines.Add('Verification boundaries')
$reportLines.Add('-----------------------')
$reportLines.Add('')
$reportLines.Add('- Automated checks do not substitute for manual screen-reader traversal.')
$reportLines.Add('- Windows High Contrast and 125%, 150% and 200% DPI remain manual acceptance gates.')
$reportLines.Add('- Package-input/content fingerprints and ZIP hash are recorded in the external package provenance sidecar after this report is finalized.')
$reportLines.Add('- This report describes the current working-tree build; it is not the historical v1.2.3 baseline report.')
[System.IO.File]::WriteAllLines((Join-Path $stage 'TEST-REPORT.txt'), $reportLines, (New-Object System.Text.UTF8Encoding($false)))

$actualPackageInputs = Get-PackageRelativePaths -RootPath $stage
Assert-NetStuckExactRelativeInventory -ActualPaths $actualPackageInputs -ExpectedPaths $expectedPackageInputs -Label 'Package input'
$packageInputInventory = @(Get-PackageInventory -RootPath $stage -RelativePaths $expectedPackageInputs -Role 'package-input')
$packageInputFingerprint = Get-NetStuckInventoryFingerprint -Inventory $packageInputInventory

[string[]]$manifestPaths = @($expectedPackageInputs)
[Array]::Sort($manifestPaths, [StringComparer]::Ordinal)
$manifestLines = @($manifestPaths | ForEach-Object { (Get-NetStuckSha256 -Path (Join-Path $stage ($_.Replace('/', '\')))) + '  ' + $_ })
[System.IO.File]::WriteAllLines((Join-Path $stage 'SHA256SUMS.txt'), $manifestLines, (New-Object System.Text.UTF8Encoding($false)))
Assert-PackageManifest -RootPath $stage -InputPaths $expectedPackageInputs

$actualPackageContent = Get-PackageRelativePaths -RootPath $stage
Assert-NetStuckExactRelativeInventory -ActualPaths $actualPackageContent -ExpectedPaths $expectedPackageContent -Label 'Package content'
$packageContentInventory = @(Get-PackageInventory -RootPath $stage -RelativePaths $expectedPackageContent -Role 'package-content')
$packageContentFingerprint = Get-NetStuckInventoryFingerprint -Inventory $packageContentInventory
if ((Get-NetStuckSha256 -Path (Join-Path $stage 'NetStuck.exe')) -ne $builtExeHash) { throw 'Staged executable differs from the verified build output.' }

Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal
$zipHash = Get-NetStuckSha256 -Path $zip
$verificationRoot = Join-Path $releaseRoot ('.package-verify-' + [Guid]::NewGuid().ToString('N'))
$verificationError = $null
try {
    New-Item -ItemType Directory -Path $verificationRoot | Out-Null
    Expand-Archive -LiteralPath $zip -DestinationPath $verificationRoot
    $extractedStage = Join-Path $verificationRoot ([System.IO.Path]::GetFileName($stage))
    if (-not (Test-Path -LiteralPath $extractedStage -PathType Container)) { throw 'ZIP did not contain the expected portable root directory.' }
    $extractedPaths = Get-PackageRelativePaths -RootPath $extractedStage
    Assert-NetStuckExactRelativeInventory -ActualPaths $extractedPaths -ExpectedPaths $expectedPackageContent -Label 'Extracted package content'
    Assert-PackageManifest -RootPath $extractedStage -InputPaths $expectedPackageInputs
    $extractedInventory = @(Get-PackageInventory -RootPath $extractedStage -RelativePaths $expectedPackageContent -Role 'package-content')
    $extractedContentFingerprint = Get-NetStuckInventoryFingerprint -Inventory $extractedInventory
    if ($extractedContentFingerprint -ne $packageContentFingerprint) {
        throw "Extracted package content differs: stage=$packageContentFingerprint extracted=$extractedContentFingerprint"
    }
}
catch { $verificationError = $_ }
finally {
    $verificationFull = [System.IO.Path]::GetFullPath($verificationRoot)
    $releaseFull = [System.IO.Path]::GetFullPath($releaseRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if ($verificationFull.StartsWith($releaseFull, [StringComparison]::OrdinalIgnoreCase) -and [System.IO.Path]::GetFileName($verificationFull).StartsWith('.package-verify-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $verificationFull -Recurse -Force -ErrorAction SilentlyContinue
    }
}
if ($verificationError) { throw $verificationError }

$packageRecord = [ordered]@{
    SchemaVersion = 2
    GeneratedUtc = [DateTime]::UtcNow.ToString('o')
    Version = $Version
    Branch = $branch
    Head = $head
    TrackedDiffFingerprint = $diffFingerprint
    StagedDiffFingerprint = $stagedDiffFingerprint
    SourceInputFingerprint = $postBuildProvenance.SourceInputFingerprint
    ToolchainFingerprint = $postBuildProvenance.ToolchainFingerprint
    BuildInvocationFingerprint = $postBuildProvenance.BuildInvocationFingerprint
    ActualCompilerArgumentFingerprint = $postBuildProvenance.ActualCompilerArgumentFingerprint
    ReferenceInputFingerprint = $postBuildProvenance.ReferenceInputFingerprint
    PackageInputFingerprint = $packageInputFingerprint
    PackageContentFingerprint = $packageContentFingerprint
    PackageContentDisposition = 'PROVENANCE_VERIFIED'
    ZipContainer = [ordered]@{ Path = $zip; Size = [int64](Get-Item -LiteralPath $zip).Length; Sha256 = $zipHash }
    Executable = [ordered]@{ FileVersion = $fileVersion; Size = [int64](Get-Item -LiteralPath $builtExe).Length; Sha256 = $builtExeHash }
    PackageInputs = @($packageInputInventory)
    PackageContent = @($packageContentInventory)
    Compiler = $postBuildProvenance.Compiler
    Runtime = $postBuildProvenance.Runtime
    PowerShellHost = $postBuildProvenance.PowerShellHost
    NormalizedCompilerArguments = @($postBuildProvenance.NormalizedCompilerArguments)
    ActualCompilerArguments = @($postBuildProvenance.ActualCompilerArguments)
    CompilerArgumentSerialization = $postBuildProvenance.CompilerArgumentSerialization
    ReferenceInputs = @($postBuildProvenance.ReferenceInputs)
    ToolchainInputs = @($postBuildProvenance.ToolchainInputs)
}
[System.IO.File]::WriteAllText($packageProvenancePath, ($packageRecord | ConvertTo-Json -Depth 8), (New-Object System.Text.UTF8Encoding($false)))

Write-Output "Package: $zip"
Write-Output "ZIP SHA256: $zipHash"
Write-Output "SOURCE_INPUT_FINGERPRINT: $($postBuildProvenance.SourceInputFingerprint)"
Write-Output "TOOLCHAIN_FINGERPRINT: $($postBuildProvenance.ToolchainFingerprint)"
Write-Output "BUILD_INVOCATION_FINGERPRINT: $($postBuildProvenance.BuildInvocationFingerprint)"
Write-Output "PACKAGE_INPUT_FINGERPRINT: $packageInputFingerprint"
Write-Output "PACKAGE_CONTENT_FINGERPRINT: $packageContentFingerprint"
Write-Output "Package provenance: $packageProvenancePath"
