[CmdletBinding()]
param([string]$RepositoryRoot)

$ErrorActionPreference = 'Stop'
if (-not $RepositoryRoot) { $RepositoryRoot = Split-Path -Parent $PSScriptRoot }
$repoRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
. (Join-Path $PSScriptRoot 'NetStuck.BuildProvenance.ps1')

$script:failures = 0
function Assert-ProvenanceCondition {
    param([string]$Name, [bool]$Condition, [string]$Detail)
    $status = if ($Condition) { 'PASS' } else { 'FAIL' }
    Write-Output ($status + " build provenance $Name - $Detail")
    if (-not $Condition) { $script:failures++ }
}

function Test-ExpectedFailure {
    param([scriptblock]$Action)
    try { & $Action; return $false } catch { return $true }
}

function Get-LegacyPingBuilderBlock {
    param([string]$Text)
    $start = $Text.IndexOf('void BuildPingPageLegacyV102()', [StringComparison]::Ordinal)
    $end = if ($start -ge 0) { $Text.IndexOf('void BuildTracePageLegacyV102()', $start, [StringComparison]::Ordinal) } else { -1 }
    if ($start -lt 0 -or $end -le $start) { throw 'Unable to isolate BuildPingPageLegacyV102.' }
    return (($Text.Substring($start, $end - $start) -replace "`r`n", "`n").Trim())
}

function Read-GitBlobUtf8 {
    param([string]$RepositoryRoot, [string]$ObjectSpecification)

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'git.exe'
    $startInfo.Arguments = '-C "' + $RepositoryRoot.Replace('"', '\"') + '" cat-file blob "' + $ObjectSpecification.Replace('"', '\"') + '"'
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $memory = New-Object System.IO.MemoryStream
    try {
        if (-not $process.Start()) { throw 'Unable to start git cat-file.' }
        $process.StandardOutput.BaseStream.CopyTo($memory)
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw ('Unable to read Git blob ' + $ObjectSpecification + ': ' + $standardError.Trim())
        }
        $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
        return $strictUtf8.GetString($memory.ToArray())
    }
    finally {
        $memory.Dispose()
        $process.Dispose()
    }
}

$ownedRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('NetStuck-build-provenance-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $ownedRoot | Out-Null
try {
    $compiler = Resolve-NetStuckCompilerPath
    $unicodeSegment = ([string][char]0x0E17) + [char]0x0E14 + [char]0x0E2A + [char]0x0E2D + [char]0x0E1A
    $output = Join-Path $ownedRoot ("Output Folder (Unicode_" + $unicodeSegment + ")\NetStuck.exe")
    $provenance = Get-NetStuckBuildProvenance -RepositoryRoot $repoRoot -OutputPath $output -CompilerPath $compiler
    $productionSources = @(Get-NetStuckProductionSourcePaths)

    $sourceDirectoryAccepted = $true
    try { Assert-NetStuckProductionSourceDirectory -RepositoryRoot $repoRoot } catch { $sourceDirectoryAccepted = $false }
    Assert-ProvenanceCondition 'canonical production source allowlist is exact' $sourceDirectoryAccepted ($productionSources.Count.ToString() + ' explicit source files')

    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $currentMainForm = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'src\NetStuck\NetStuck.cs'), $strictUtf8)
    $baselineMainForm = Read-GitBlobUtf8 -RepositoryRoot $repoRoot -ObjectSpecification 'v1.2.3:src/NetStuck/NetStuck.cs'
    $legacyEquivalent = [String]::Equals((Get-LegacyPingBuilderBlock -Text $currentMainForm), (Get-LegacyPingBuilderBlock -Text $baselineMainForm), [StringComparison]::Ordinal)
    Assert-ProvenanceCondition 'legacy Live Ping builder is baseline-equivalent' $legacyEquivalent 'BuildPingPageLegacyV102 has zero Phase A delta'

    $missingRejected = Test-ExpectedFailure { Assert-NetStuckProductionSourceSpecifications -RelativePaths @($productionSources | Select-Object -Skip 1) }
    Assert-ProvenanceCondition 'missing required production source is rejected' $missingRejected 'allowlist comparison returned non-success'

    $unexpectedRejected = Test-ExpectedFailure { Assert-NetStuckProductionSourceSpecifications -RelativePaths @($productionSources + 'src/NetStuck/Unexpected.cs') }
    Assert-ProvenanceCondition 'unexpected production source is rejected' $unexpectedRejected 'allowlist comparison returned non-success'

    $testSourceRejected = Test-ExpectedFailure { Assert-NetStuckProductionSourceSpecifications -RelativePaths @($productionSources + 'tests/FeatureTests.cs') }
    Assert-ProvenanceCondition 'test source entering production inventory is rejected' $testSourceRejected 'tests/FeatureTests.cs cannot enter compiler inventory'

    $arguments = @($provenance.NormalizedCompilerArguments)
    $explicitCompilerInputs = $arguments -contains '/noconfig' -and $arguments -contains '/nostdlib+' -and
        @($arguments | Where-Object { $_ -like '/reference:<FRAMEWORK>/*' }).Count -eq (Get-NetStuckFrameworkReferenceNames).Count -and
        @($arguments | Where-Object { $_ -match '(?i)csc\.rsp|(^|/)tests?/' }).Count -eq 0 -and
        @($arguments | Where-Object { $_ -eq '/reference:<FRAMEWORK>/mscorlib.dll' }).Count -eq 1
    Assert-ProvenanceCondition 'implicit compiler inputs are disabled and references are explicit' $explicitCompilerInputs ((Get-NetStuckFrameworkReferenceNames).Count.ToString() + ' hashed references')

    $actualArguments = @($provenance.ActualCompilerArguments)
    $argumentSpecifications = @($provenance.CompilerArgumentSpecifications)
    $oneToOneArgumentModel = $actualArguments.Count -eq $arguments.Count -and
        $argumentSpecifications.Count -eq $arguments.Count -and
        @($argumentSpecifications | Where-Object { $_.Role -eq 'win32-icon' }).Count -eq 1 -and
        @($arguments | Where-Object { $_ -eq '/win32icon:src/NetStuck/assets/netstuck-bright.ico' }).Count -eq 1 -and
        @($actualArguments | Where-Object { $_ -like ("/out:*Output Folder (Unicode_" + $unicodeSegment + ")*NetStuck.exe") }).Count -eq 1
    Assert-ProvenanceCondition 'actual and normalized compiler argv have one-to-one atomic entries' $oneToOneArgumentModel ("actual={0}; normalized={1}; specs={2}" -f $actualArguments.Count, $arguments.Count, $argumentSpecifications.Count)

    [string[]]$argvFixture = @(
        '/win32icon:C:\Path With Spaces\NetStuck.ico',
        '/reference:C:\Path With Spaces\Some Library.dll',
        '/define:ONE;TWO',
        '/out:C:\Temp Folder\NetStuck.exe',
        ("/reference:C:\" + $unicodeSegment + "\library.dll"),
        '/reference:C:\Path (Preview)_x64\Some.dll',
        "/reference:C:\Path`tWithTab\Some.dll",
        '/define:QUOTE=\"C:\\fixture\\value\"'
    )
    $fixtureFingerprint = Get-NetStuckArgumentFingerprint -Arguments $argvFixture
    $fixtureAtomic = $argvFixture.Count -eq 8 -and
        $argvFixture[0] -eq '/win32icon:C:\Path With Spaces\NetStuck.ico' -and
        $argvFixture[1] -eq '/reference:C:\Path With Spaces\Some Library.dll' -and
        $argvFixture[2] -eq '/define:ONE;TWO' -and
        $argvFixture[3] -eq '/out:C:\Temp Folder\NetStuck.exe' -and
        $argvFixture[4].Contains($unicodeSegment) -and $argvFixture[5] -match '\(Preview\)' -and
        $argvFixture[6].IndexOf("`t") -gt 0 -and $argvFixture[7] -eq '/define:QUOTE=\"C:\\fixture\\value\"'
    Assert-ProvenanceCondition 'canonical argv preserves supported special characters as atomic values' $fixtureAtomic 'space, tab, quote, backslash, colon, equals, parentheses, underscore and Unicode covered'

    [string[]]$reorderedFixture = @($argvFixture)
    $swap = $reorderedFixture[0]; $reorderedFixture[0] = $reorderedFixture[1]; $reorderedFixture[1] = $swap
    Assert-ProvenanceCondition 'argv reordering changes invocation identity' ((Get-NetStuckArgumentFingerprint -Arguments $reorderedFixture) -ne $fixtureFingerprint) 'indices are part of canonical binary serialization'

    [string[]]$contentChangedFixture = @($argvFixture)
    $contentChangedFixture[2] = '/define:ONE;THREE'
    Assert-ProvenanceCondition 'argv content change changes invocation identity' ((Get-NetStuckArgumentFingerprint -Arguments $contentChangedFixture) -ne $fixtureFingerprint) 'UTF-8 argument bytes are authoritative'

    $minimalDisplay = Format-NetStuckArgumentVector -Arguments $argvFixture -Style Minimal
    $quotedDisplay = Format-NetStuckArgumentVector -Arguments $argvFixture -Style AlwaysQuote
    $displayIndependent = $minimalDisplay -ne $quotedDisplay -and
        (Get-NetStuckArgumentFingerprint -Arguments $argvFixture) -eq $fixtureFingerprint
    Assert-ProvenanceCondition 'diagnostic display formatting does not change canonical identity' $displayIndependent 'minimal and always-quoted diagnostics differ while binary fingerprint remains fixed'

    $mutatedArguments = @($arguments)
    $optimizeIndex = [Array]::IndexOf($mutatedArguments, '/optimize+')
    $mutatedArguments[$optimizeIndex] = '/optimize-'
    $argumentDrift = (Get-NetStuckArgumentFingerprint -Arguments $mutatedArguments) -ne $provenance.BuildInvocationFingerprint
    Assert-ProvenanceCondition 'compiler argument drift changes invocation identity' $argumentDrift 'optimize flag mutation detected'

    $binaryA = Join-Path $ownedRoot 'binary-a'
    New-Item -ItemType Directory -Path $binaryA | Out-Null
    $binaryPath = Join-Path $binaryA 'fixture.bin'
    [System.IO.File]::WriteAllBytes($binaryPath, [byte[]](0, 13, 10, 255, 128, 1))
    $fixtureSpec = @([pscustomobject]@{ Role = 'binary-fixture'; RelativePath = 'fixture.bin' })
    $binaryBefore = @(Get-NetStuckFileInventory -RootPath $binaryA -Specifications $fixtureSpec -SkipTracking)
    $binaryBeforeFingerprint = Get-NetStuckInventoryFingerprint -Inventory $binaryBefore
    [System.IO.File]::WriteAllBytes($binaryPath, [byte[]](0, 13, 10, 254, 128, 1))
    $binaryAfter = @(Get-NetStuckFileInventory -RootPath $binaryA -Specifications $fixtureSpec -SkipTracking)
    $binaryAfterFingerprint = Get-NetStuckInventoryFingerprint -Inventory $binaryAfter
    Assert-ProvenanceCondition 'binary byte drift changes canonical fingerprint' ($binaryBeforeFingerprint -ne $binaryAfterFingerprint) 'raw SHA-256 changed without text decoding'

    $relocationLabel = "checkout with spaces (Unicode_" + $unicodeSegment + ")"
    $relocationA = Join-Path $ownedRoot $relocationLabel
    $relocationB = Join-Path $ownedRoot 'checkout-b'
    New-Item -ItemType Directory -Path $relocationA,$relocationB | Out-Null
    $sourceSpecs = @(Get-NetStuckRepositoryInputSpecifications)
    foreach ($specification in $sourceSpecs) {
        foreach ($destinationRoot in @($relocationA, $relocationB)) {
            $destination = Join-Path $destinationRoot ($specification.RelativePath.Replace('/', '\'))
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $repoRoot ($specification.RelativePath.Replace('/', '\'))) -Destination $destination
        }
    }
    $relocatedInventoryA = @(Get-NetStuckFileInventory -RootPath $relocationA -Specifications $sourceSpecs -SkipTracking)
    $relocatedInventoryB = @(Get-NetStuckFileInventory -RootPath $relocationB -Specifications $sourceSpecs -SkipTracking)
    $relocatedFingerprintA = Get-NetStuckInventoryFingerprint -Inventory $relocatedInventoryA
    $relocatedFingerprintB = Get-NetStuckInventoryFingerprint -Inventory $relocatedInventoryB
    $relocationPortable = $relocatedFingerprintA -eq $relocatedFingerprintB -and $relocatedFingerprintA -eq $provenance.SourceInputFingerprint -and
        (Get-NetStuckCanonicalInventoryManifest -Inventory $relocatedInventoryA).IndexOf($relocationA, [StringComparison]::OrdinalIgnoreCase) -lt 0
    Assert-ProvenanceCondition 'checkout relocation preserves portable source identity' $relocationPortable $relocatedFingerprintA

    $relocatedInvocation = Get-NetStuckBuildInvocation -RepositoryRoot $relocationA -OutputPath (Join-Path $ownedRoot 'Relocated Output\NetStuck.exe') -CompilerPath $compiler
    $relocatedIcon = @($relocatedInvocation.ActualArguments | Where-Object { $_ -like '/win32icon:*' })
    $relocatedArgumentPortable = $relocatedInvocation.ActualArguments.Count -eq $relocatedInvocation.NormalizedArguments.Count -and
        $relocatedIcon.Count -eq 1 -and $relocatedIcon[0].IndexOf($relocationLabel, [StringComparison]::Ordinal) -ge 0 -and
        $relocatedInvocation.Fingerprint -eq $provenance.BuildInvocationFingerprint
    Assert-ProvenanceCondition 'path relocation keeps icon argv atomic and portable identity stable' $relocatedArgumentPortable ("argv={0}; fingerprint={1}" -f $relocatedInvocation.ActualArguments.Count, $relocatedInvocation.Fingerprint)

    $expectedPackageInputs = @('CHANGELOG.md','NetStuck-Icon.png','NetStuck.exe','README-TH.md','README.md','TEST-REPORT.txt','tools/plink.exe','tools/PuTTY-LICENCE.txt')
    $packageExtraRejected = Test-ExpectedFailure { Assert-NetStuckExactRelativeInventory -ActualPaths @($expectedPackageInputs + 'tests/FeatureTests.exe') -ExpectedPaths $expectedPackageInputs -Label 'Package input' }
    $packageMissingRejected = Test-ExpectedFailure { Assert-NetStuckExactRelativeInventory -ActualPaths @($expectedPackageInputs | Select-Object -Skip 1) -ExpectedPaths $expectedPackageInputs -Label 'Package input' }
    Assert-ProvenanceCondition 'package input drift is rejected' ($packageExtraRejected -and $packageMissingRejected) 'extra and missing paths both fail exact inventory'

    $toolchainMutation = @($provenance.ToolchainInputs | ForEach-Object {
        [pscustomobject]@{ Role = $_.Role; RelativePath = $_.RelativePath; Size = $_.Size; Sha256 = $_.Sha256 }
    })
    $replacementHash = if ($toolchainMutation[0].Sha256 -eq ('0' * 64)) { 'f' * 64 } else { '0' * 64 }
    $toolchainMutation[0].Sha256 = $replacementHash
    $toolchainClassified = (Get-NetStuckInventoryFingerprint -Inventory $toolchainMutation) -ne $provenance.ToolchainFingerprint -and
        (Get-NetStuckInventoryFingerprint -Inventory $provenance.SourceInputs) -eq $provenance.SourceInputFingerprint
    Assert-ProvenanceCondition 'toolchain drift is classified separately from source drift' $toolchainClassified 'toolchain fingerprint changed; source fingerprint remained stable'

    $gitFingerprintA = Get-NetStuckBinarySafeGitDiffFingerprint -RepositoryRoot $repoRoot
    $gitFingerprintB = Get-NetStuckBinarySafeGitDiffFingerprint -RepositoryRoot $repoRoot
    Assert-ProvenanceCondition 'tracked diff fingerprint is binary-safe and stable in this host' ($gitFingerprintA -eq $gitFingerprintB -and $gitFingerprintA -match '^[0-9a-f]{40}$') $gitFingerprintA

    $canonicalManifest = Get-NetStuckCanonicalInventoryManifest -Inventory $provenance.SourceInputs
    Assert-ProvenanceCondition 'aggregate manifest uses UTF-8-safe LF records and normalized paths' ($canonicalManifest -notmatch "`r" -and $canonicalManifest -notmatch '\\') 'role<TAB>path<TAB>size<TAB>sha256 with LF'

    Write-Output "EVIDENCE source-input fingerprint $($provenance.SourceInputFingerprint)"
    Write-Output "EVIDENCE toolchain fingerprint $($provenance.ToolchainFingerprint)"
    Write-Output "EVIDENCE build-invocation fingerprint $($provenance.BuildInvocationFingerprint)"
    Write-Output "EVIDENCE canonical-argv fixture fingerprint $fixtureFingerprint"
    Write-Output "EVIDENCE compiler-argv count $($actualArguments.Count)"
    Write-Output "EVIDENCE reference-input fingerprint $($provenance.ReferenceInputFingerprint)"
    Write-Output "EVIDENCE tracked-diff fingerprint $gitFingerprintA"
}
catch {
    $script:failures++
    Write-Output "FAIL build provenance unexpected infrastructure exception - $($_.Exception.GetType().Name): $($_.Exception.Message)"
}
finally {
    $ownedFull = [System.IO.Path]::GetFullPath($ownedRoot)
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ($ownedFull.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and [System.IO.Path]::GetFileName($ownedFull).StartsWith('NetStuck-build-provenance-', [StringComparison]::Ordinal)) {
        try {
            if (Test-Path -LiteralPath $ownedFull) { Remove-Item -LiteralPath $ownedFull -Recurse -Force -ErrorAction Stop }
            if (Test-Path -LiteralPath $ownedFull) { throw 'Owned build-provenance root remains after cleanup.' }
        }
        catch {
            $script:failures++
            Write-Output "FAIL build provenance owned-root cleanup - $($_.Exception.GetType().Name)"
        }
    }
}

Write-Output "Failures: $script:failures"
if ($script:failures -ne 0) { exit 1 }
