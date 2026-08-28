[CmdletBinding()]
param(
    [ValidateRange(10, 28800)]
    [int]$SoakSeconds = 10,
    [string]$SummaryPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src\NetStuck'
$testRoot = Join-Path $repoRoot 'tests'
$outputRoot = Join-Path $repoRoot 'artifacts\test'
. (Join-Path $PSScriptRoot 'NetStuck.BuildProvenance.ps1')
$compiler = Resolve-NetStuckCompilerPath
$powerShellExecutable = (Get-Process -Id $PID).Path
$suiteResults = New-Object System.Collections.Generic.List[object]
$stageResults = New-Object System.Collections.Generic.List[object]
$requiredStageNames = @('Test host compilation', 'Development build')
$requiredSuiteMinimums = [ordered]@{
    'Test runner infrastructure' = 10
    'NetOpsCoreTests.exe' = 16
    'FeatureTests.exe' = 93
    'TracerouteLifecycleTests.exe' = 31
    'UiFoundationTests.exe' = 63
    'PerformanceTests.exe' = 10
    'PollingCadenceTests.exe' = 3
    'OvernightSoakTests.exe' = 8
    'Capture-UiFoundations infrastructure' = 39
    'Build provenance infrastructure' = 19
}
$requiredSuiteNames = @($requiredSuiteMinimums.Keys)

if (-not $SummaryPath) { $SummaryPath = Join-Path $outputRoot 'test-summary.json' }
$resolvedSummaryPath = [System.IO.Path]::GetFullPath($SummaryPath)

# Exercise redirected process input under the UTF-8 console mode used by
# Windows GitHub runners. This guards Collector passwords against code-page
# dependent BOM/preamble regressions while keeping the test fully local.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

function ConvertTo-NativeArgument {
    param([AllowNull()][AllowEmptyString()][string]$Value)
    if ($null -eq $Value -or $Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') {
            $backslashes++
            continue
        }
        if ($character -eq '"') {
            if ($backslashes -gt 0) { [void]$builder.Append(('\' * ($backslashes * 2))) }
            [void]$builder.Append('\"')
            $backslashes = 0
            continue
        }
        if ($backslashes -gt 0) {
            [void]$builder.Append(('\' * $backslashes))
            $backslashes = 0
        }
        [void]$builder.Append($character)
    }
    if ($backslashes -gt 0) { [void]$builder.Append(('\' * ($backslashes * 2))) }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function ConvertFrom-NativeOutput {
    param([string]$Text)
    if ([String]::IsNullOrEmpty($Text)) { return @() }
    return @($Text -split '\r?\n' | Where-Object { $_.Length -gt 0 })
}

function Invoke-NativeProcess {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList = @(),
        [string]$WorkingDirectory = $repoRoot
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = (@($ArgumentList | ForEach-Object { ConvertTo-NativeArgument -Value $_ }) -join ' ')
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        try {
            if (-not $process.Start()) { throw 'Process.Start returned false.' }
        }
        catch {
            return [pscustomobject]@{
                InvocationSucceeded = $false
                ExitCode = $null
                StdOutLines = @()
                StdErrLines = @()
                InvocationErrorType = $_.Exception.GetType().Name
                InvocationErrorMessage = $_.Exception.Message
            }
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $stdout = $stdoutTask.Result
        $stderr = $stderrTask.Result
        return [pscustomobject]@{
            InvocationSucceeded = $true
            ExitCode = [int]$process.ExitCode
            StdOutLines = @(ConvertFrom-NativeOutput -Text $stdout)
            StdErrLines = @(ConvertFrom-NativeOutput -Text $stderr)
            InvocationErrorType = $null
            InvocationErrorMessage = $null
        }
    }
    finally {
        $process.Dispose()
    }
}

function Write-NativeProcessOutput {
    param([object]$Result)
    @($Result.StdOutLines) | ForEach-Object { Write-Host $_ }
    @($Result.StdErrLines) | ForEach-Object { Write-Host ("[stderr] " + $_) }
}

function New-SuiteResult {
    param(
        [string]$Name,
        [string]$Command,
        [string[]]$Lines,
        [object]$NativeResult
    )
    $passed = @($Lines | Where-Object { $_ -match '^PASS(?: |$)' }).Count
    $failed = @($Lines | Where-Object { $_ -match '^FAIL(?: |$)' }).Count
    $skipped = @($Lines | Where-Object { $_ -match '^SKIP(?: |$)' }).Count
    $discovered = $passed + $failed + $skipped
    $infrastructureFailures = 0
    $issues = New-Object System.Collections.Generic.List[string]
    if (-not $NativeResult.InvocationSucceeded) {
        $infrastructureFailures++
        $issues.Add("native invocation failed [$($NativeResult.InvocationErrorType)]")
    }
    elseif ($NativeResult.ExitCode -ne 0) {
        if ($failed -eq 0) { $infrastructureFailures++ }
        $issues.Add("native exit $($NativeResult.ExitCode)")
    }
    if ($failed -ne 0) { $issues.Add("$failed parsed FAIL result(s)") }
    if ($skipped -ne 0) { $issues.Add("$skipped required SKIP result(s)") }
    if ($discovered -le 0) { $issues.Add('no test results discovered') }
    $status = if ($issues.Count -eq 0) { 'Passed' } else { 'Failed' }
    return [pscustomobject]@{
        Name = $Name
        Command = $Command
        InvocationSucceeded = [bool]$NativeResult.InvocationSucceeded
        NativeExitCode = $NativeResult.ExitCode
        StdErrLines = @($NativeResult.StdErrLines).Count
        Passed = [int]$passed
        Failed = [int]$failed
        Skipped = [int]$skipped
        Discovered = [int]$discovered
        InfrastructureFailures = [int]$infrastructureFailures
        Status = $status
        Issues = @($issues)
    }
}

function Add-SuiteResult {
    param(
        [string]$Name,
        [string]$Command,
        [string[]]$Lines,
        [object]$NativeResult
    )
    $suite = New-SuiteResult -Name $Name -Command $Command -Lines $Lines -NativeResult $NativeResult
    $minimumExpected = if ($requiredSuiteMinimums.Contains($Name)) { [int]$requiredSuiteMinimums[$Name] } else { 0 }
    $floorSatisfied = $minimumExpected -le 0 -or $suite.Discovered -ge $minimumExpected
    $suite | Add-Member -NotePropertyName MinimumExpected -NotePropertyValue $minimumExpected
    $suite | Add-Member -NotePropertyName FloorSatisfied -NotePropertyValue $floorSatisfied
    if (-not $floorSatisfied) {
        $suite.Issues = @($suite.Issues) + "discovered count $($suite.Discovered) is below minimum $minimumExpected"
        $suite.Status = 'Failed'
    }
    $suiteResults.Add($suite)
    Write-Host ("Stage result [{0}]: {1}; discovered={2}; floor={3}; floor-satisfied={4}; passed={5}; failed={6}; skipped={7}; infrastructure={8}; native-exit={9}" -f
        $suite.Name, $suite.Status.ToUpperInvariant(), $suite.Discovered, $suite.MinimumExpected, $suite.FloorSatisfied,
        $suite.Passed, $suite.Failed, $suite.Skipped, $suite.InfrastructureFailures,
        $(if ($null -eq $suite.NativeExitCode) { 'N/A' } else { $suite.NativeExitCode }))
    return $suite
}

function Add-StageResult {
    param(
        [string]$Name,
        [string]$Status,
        [object]$NativeExitCode,
        [int]$InfrastructureFailures,
        [string]$Detail
    )
    $stageResults.Add([pscustomobject]@{
        Name = $Name
        Status = $Status
        NativeExitCode = $NativeExitCode
        InfrastructureFailures = $InfrastructureFailures
        Detail = $Detail
    })
}

function Get-RunReconciliation {
    param(
        [object[]]$Suites,
        [object[]]$Stages,
        [string[]]$RequiredSuites,
        [string[]]$RequiredStages,
        [System.Collections.IDictionary]$MinimumExpectedCounts,
        [Exception]$RunError
    )
    $issues = New-Object System.Collections.Generic.List[string]
    foreach ($requiredStage in $RequiredStages) {
        $matches = @($Stages | Where-Object { $_.Name -eq $requiredStage })
        if ($matches.Count -ne 1) { $issues.Add("required stage '$requiredStage' executed $($matches.Count) time(s)"); continue }
        if ($matches[0].Status -ne 'Passed') { $issues.Add("required stage '$requiredStage' failed") }
    }
    foreach ($requiredSuite in $RequiredSuites) {
        $matches = @($Suites | Where-Object { $_.Name -eq $requiredSuite })
        if ($matches.Count -ne 1) { $issues.Add("required suite '$requiredSuite' executed $($matches.Count) time(s)"); continue }
        if ($matches[0].Status -ne 'Passed') { $issues.Add("required suite '$requiredSuite' failed") }
        if ($null -ne $MinimumExpectedCounts) {
            if (-not $MinimumExpectedCounts.Contains($requiredSuite)) {
                $issues.Add("required suite '$requiredSuite' has no authoritative minimum count")
            }
            else {
                $minimum = [int]$MinimumExpectedCounts[$requiredSuite]
                if ($matches[0].Discovered -lt $minimum) {
                    $issues.Add("required suite '$requiredSuite' discovered $($matches[0].Discovered), below minimum $minimum")
                }
            }
        }
    }
    $unexpectedSuites = @($Suites | Where-Object { $RequiredSuites -notcontains $_.Name })
    if ($unexpectedSuites.Count -ne 0) { $issues.Add("unexpected suite result(s): $($unexpectedSuites.Name -join ', ')") }
    $failed = ($Suites | Measure-Object -Property Failed -Sum).Sum
    $skipped = ($Suites | Measure-Object -Property Skipped -Sum).Sum
    $infrastructure = ($Suites | Measure-Object -Property InfrastructureFailures -Sum).Sum
    $infrastructure += ($Stages | Measure-Object -Property InfrastructureFailures -Sum).Sum
    if ($null -eq $failed) { $failed = 0 }
    if ($null -eq $skipped) { $skipped = 0 }
    if ($null -eq $infrastructure) { $infrastructure = 0 }
    if ($failed -ne 0) { $issues.Add("parsed failed tests total $failed") }
    if ($skipped -ne 0) { $issues.Add("required skipped tests total $skipped") }
    if ($infrastructure -ne 0) { $issues.Add("infrastructure failures total $infrastructure") }
    if ($null -ne $RunError) { $issues.Add("run error [$($RunError.GetType().Name)]: $($RunError.Message)") }
    $passed = $issues.Count -eq 0
    return [pscustomobject]@{
        Passed = $passed
        RecommendedExitCode = $(if ($passed) { 0 } else { 1 })
        Issues = @($issues)
        InfrastructureFailures = [int]$infrastructure
    }
}

function Invoke-Compiler {
    param([string[]]$Arguments)
    $result = Invoke-NativeProcess -FilePath $compiler -ArgumentList $Arguments
    Write-NativeProcessOutput -Result $result
    if (-not $result.InvocationSucceeded) {
        throw "C# compiler invocation failed [$($result.InvocationErrorType)]: $($result.InvocationErrorMessage)"
    }
    if ($result.ExitCode -ne 0) { throw "C# compilation failed with exit code $($result.ExitCode)." }
}

function Invoke-TestExecutable {
    param(
        [string]$Name,
        [string[]]$Arguments = @()
    )
    Write-Output "`n=== $Name ==="
    $result = Invoke-NativeProcess -FilePath (Join-Path $outputRoot $Name) -ArgumentList $Arguments -WorkingDirectory $outputRoot
    Write-NativeProcessOutput -Result $result
    $lines = @($result.StdOutLines) + @($result.StdErrLines)
    $suite = Add-SuiteResult -Name $Name -Command ($Name + $(if ($Arguments.Count) { ' ' + ($Arguments -join ' ') } else { '' })) -Lines $lines -NativeResult $result
    if ($suite.Status -ne 'Passed') { throw "$Name did not reconcile as a passing mandatory suite." }
}

function Invoke-CaptureInfrastructureTests {
    $name = 'Capture-UiFoundations infrastructure'
    Write-Output "`n=== $name ==="
    $captureScript = Join-Path $PSScriptRoot 'Capture-UiFoundations.ps1'
    $arguments = @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $captureScript, '-RunInfrastructureTests')
    $result = Invoke-NativeProcess -FilePath $powerShellExecutable -ArgumentList $arguments -WorkingDirectory $repoRoot
    Write-NativeProcessOutput -Result $result
    $lines = @($result.StdOutLines) + @($result.StdErrLines)
    $suite = Add-SuiteResult -Name $name -Command '.\scripts\Capture-UiFoundations.ps1 -RunInfrastructureTests' -Lines $lines -NativeResult $result
    if ($suite.Status -ne 'Passed') { throw "$name did not reconcile as a passing mandatory suite." }
}

function Invoke-BuildProvenanceInfrastructureTests {
    $name = 'Build provenance infrastructure'
    Write-Output "`n=== $name ==="
    $provenanceTestScript = Join-Path $PSScriptRoot 'Test-BuildProvenance.ps1'
    $arguments = @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $provenanceTestScript, '-RepositoryRoot', $repoRoot)
    $result = Invoke-NativeProcess -FilePath $powerShellExecutable -ArgumentList $arguments -WorkingDirectory $repoRoot
    Write-NativeProcessOutput -Result $result
    $lines = @($result.StdOutLines) + @($result.StdErrLines)
    $suite = Add-SuiteResult -Name $name -Command '.\scripts\Test-BuildProvenance.ps1' -Lines $lines -NativeResult $result
    if ($suite.Status -ne 'Passed') { throw "$name did not reconcile as a passing mandatory suite." }
}

function Get-NetStuckRepositoryTestStateResidue {
    $artifactDirectory = Join-Path $repoRoot 'artifacts'
    $operatorProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    $stateFiles = if (Test-Path -LiteralPath $artifactDirectory) {
        @(Get-ChildItem -LiteralPath $artifactDirectory -Recurse -Force -File -Filter 'state.json' -ErrorAction Stop)
    }
    else { @() }
    $profileResidues = 0
    $credentialResidues = 0
    $invalidStateFiles = 0
    foreach ($file in $stateFiles) {
        try {
            $raw = [System.IO.File]::ReadAllText($file.FullName)
            if (-not [String]::IsNullOrWhiteSpace($operatorProfile) -and
                $raw.IndexOf($operatorProfile, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $profileResidues++ }
            $state = $raw | ConvertFrom-Json
            foreach ($field in @('CollectorAuth1Pass','CollectorAuth2Pass','CollectorAuth1Secret','CollectorAuth2Secret','Password','Secret','PrivateKey')) {
                $property = $state.PSObject.Properties[$field]
                if ($null -ne $property -and -not [String]::IsNullOrWhiteSpace([string]$property.Value)) { $credentialResidues++ }
            }
        }
        catch { $invalidStateFiles++ }
    }
    return [pscustomobject]@{
        StateFiles = $stateFiles.Count
        OperatorProfileResidues = $profileResidues
        CredentialResidues = $credentialResidues
        InvalidStateFiles = $invalidStateFiles
    }
}

function Invoke-RunnerInfrastructureTests {
    $name = 'Test runner infrastructure'
    Write-Output "`n=== $name ==="
    $lines = New-Object System.Collections.Generic.List[string]
    $record = {
        param([string]$Assertion, [bool]$Condition)
        $lines.Add($(if ($Condition) { "PASS runner infrastructure $Assertion" } else { "FAIL runner infrastructure $Assertion" }))
    }

    $success = Invoke-NativeProcess -FilePath $powerShellExecutable -ArgumentList @(
        '-NoLogo', '-NoProfile', '-Command', '[Console]::Out.WriteLine("PASS native success"); exit 0'
    )
    $successPreserved = $success.InvocationSucceeded -and
        $success.ExitCode -eq 0 -and
        @($success.StdOutLines | Where-Object { $_ -eq 'PASS native success' }).Count -eq 1 -and
        @($success.StdErrLines).Count -eq 0
    & $record 'expected native success preserves stdout and exit zero' $successPreserved

    $expectedNegative = Invoke-NativeProcess -FilePath $powerShellExecutable -ArgumentList @(
        '-NoLogo', '-NoProfile', '-Command', '[Console]::Error.WriteLine("EXPECTED semantic stderr"); exit 7'
    )
    $negativePreserved = $expectedNegative.InvocationSucceeded -and
        $expectedNegative.ExitCode -eq 7 -and
        @($expectedNegative.StdErrLines | Where-Object { $_ -eq 'EXPECTED semantic stderr' }).Count -eq 1
    & $record 'expected semantic stderr and nonzero remain assertable by the parent' $negativePreserved

    $passedStage = [pscustomobject]@{ Name = 'Synthetic stage'; Status = 'Passed'; NativeExitCode = 0; InfrastructureFailures = 0; Detail = 'synthetic' }
    $unexpectedSuite = New-SuiteResult -Name 'Synthetic suite' -Command 'synthetic' -Lines (@($expectedNegative.StdOutLines) + @($expectedNegative.StdErrLines)) -NativeResult $expectedNegative
    $unexpectedReconciliation = Get-RunReconciliation -Suites @($unexpectedSuite) -Stages @($passedStage) -RequiredSuites @('Synthetic suite') -RequiredStages @('Synthetic stage') -RunError $null
    & $record 'unexpected child nonzero makes reconciliation fail' ($unexpectedSuite.Status -eq 'Failed' -and -not $unexpectedReconciliation.Passed)

    $missingExecutable = Join-Path $outputRoot ('missing-native-' + [Guid]::NewGuid().ToString('N') + '.exe')
    $invocationFailure = Invoke-NativeProcess -FilePath $missingExecutable
    $invocationSuite = New-SuiteResult -Name 'Synthetic suite' -Command 'missing native' -Lines @() -NativeResult $invocationFailure
    $invocationReconciliation = Get-RunReconciliation -Suites @($invocationSuite) -Stages @($passedStage) -RequiredSuites @('Synthetic suite') -RequiredStages @('Synthetic stage') -RunError $null
    $invocationClassified = -not $invocationFailure.InvocationSucceeded -and
        $invocationSuite.InfrastructureFailures -eq 1 -and
        -not $invocationReconciliation.Passed
    & $record 'native invocation failure is an infrastructure failure' $invocationClassified

    $falseGreenNative = [pscustomobject]@{ InvocationSucceeded = $true; ExitCode = 0; StdOutLines = @('FAIL synthetic mismatch'); StdErrLines = @(); InvocationErrorType = $null; InvocationErrorMessage = $null }
    $falseGreenSuite = New-SuiteResult -Name 'Synthetic suite' -Command 'false green' -Lines @($falseGreenNative.StdOutLines) -NativeResult $falseGreenNative
    $falseGreenReconciliation = Get-RunReconciliation -Suites @($falseGreenSuite) -Stages @($passedStage) -RequiredSuites @('Synthetic suite') -RequiredStages @('Synthetic stage') -RunError $null
    & $record 'parsed FAIL with child exit zero cannot pass' ($falseGreenSuite.Failed -eq 1 -and -not $falseGreenReconciliation.Passed)

    $goodNative = [pscustomobject]@{ InvocationSucceeded = $true; ExitCode = 0; StdOutLines = @('PASS synthetic'); StdErrLines = @(); InvocationErrorType = $null; InvocationErrorMessage = $null }
    $goodSuite = New-SuiteResult -Name 'Synthetic A' -Command 'synthetic' -Lines @($goodNative.StdOutLines) -NativeResult $goodNative
    $missingReconciliation = Get-RunReconciliation -Suites @($goodSuite) -Stages @($passedStage) -RequiredSuites @('Synthetic A', 'Synthetic B') -RequiredStages @('Synthetic stage') -RunError $null
    & $record 'missing mandatory suite cannot pass' (-not $missingReconciliation.Passed -and @($missingReconciliation.Issues | Where-Object { $_ -match "Synthetic B.*0 time" }).Count -eq 1)

    $syntheticFloors = [ordered]@{ 'Synthetic A' = 2 }
    $belowFloorReconciliation = Get-RunReconciliation -Suites @($goodSuite) -Stages @($passedStage) -RequiredSuites @('Synthetic A') -RequiredStages @('Synthetic stage') -MinimumExpectedCounts $syntheticFloors -RunError $null
    $belowFloorRejected = -not $belowFloorReconciliation.Passed -and
        @($belowFloorReconciliation.Issues | Where-Object { $_ -match "Synthetic A.*discovered 1, below minimum 2" }).Count -eq 1
    & $record 'per-suite discovered count below its authoritative floor cannot pass' $belowFloorRejected

    $cleanupNative = [pscustomobject]@{ InvocationSucceeded = $true; ExitCode = 1; StdOutLines = @(); StdErrLines = @('CLEANUP FAIL synthetic'); InvocationErrorType = $null; InvocationErrorMessage = $null }
    $cleanupSuite = New-SuiteResult -Name 'Synthetic suite' -Command 'cleanup failure' -Lines @($cleanupNative.StdErrLines) -NativeResult $cleanupNative
    $cleanupReconciliation = Get-RunReconciliation -Suites @($cleanupSuite) -Stages @($passedStage) -RequiredSuites @('Synthetic suite') -RequiredStages @('Synthetic stage') -RunError $null
    $cleanupClassified = $cleanupSuite.InfrastructureFailures -eq 1 -and
        -not $cleanupReconciliation.Passed -and
        $cleanupReconciliation.RecommendedExitCode -ne 0
    & $record 'cleanup failure makes overall reconciliation nonzero' $cleanupClassified

    $passReconciliation = Get-RunReconciliation -Suites @($goodSuite) -Stages @($passedStage) -RequiredSuites @('Synthetic A') -RequiredStages @('Synthetic stage') -RunError $null
    $verdictMatchesExit = $passReconciliation.Passed -and
        $passReconciliation.RecommendedExitCode -eq 0 -and
        $falseGreenReconciliation.RecommendedExitCode -ne 0
    & $record 'overall verdict and exit decision share one reconciliation result' $verdictMatchesExit

    $residue = Get-NetStuckRepositoryTestStateResidue
    $residueClean = $residue.OperatorProfileResidues -eq 0 -and $residue.CredentialResidues -eq 0 -and $residue.InvalidStateFiles -eq 0
    & $record 'repository test-state residue has no operator-profile or credential content' $residueClean

    $native = [pscustomobject]@{
        InvocationSucceeded = $true
        ExitCode = 0
        StdOutLines = @($lines)
        StdErrLines = @()
        InvocationErrorType = $null
        InvocationErrorMessage = $null
    }
    @($lines) | ForEach-Object { Write-Host $_ }
    $suite = Add-SuiteResult -Name $name -Command 'internal runner reconciliation checks' -Lines @($lines) -NativeResult $native
    if ($suite.Status -ne 'Passed') { throw "$name did not reconcile as a passing mandatory suite." }
}

function Write-TestSummary {
    param([Exception]$RunError, [object]$Reconciliation)
    $summaryParent = Split-Path -Parent $resolvedSummaryPath
    if (-not (Test-Path -LiteralPath $summaryParent)) { New-Item -ItemType Directory -Path $summaryParent -Force | Out-Null }
    $passed = ($suiteResults | Measure-Object -Property Passed -Sum).Sum
    $failed = ($suiteResults | Measure-Object -Property Failed -Sum).Sum
    $skipped = ($suiteResults | Measure-Object -Property Skipped -Sum).Sum
    if ($null -eq $passed) { $passed = 0 }
    if ($null -eq $failed) { $failed = 0 }
    if ($null -eq $skipped) { $skipped = 0 }
    $completedRequiredSuites = @($requiredSuiteNames | Where-Object { $required = $_; @($suiteResults | Where-Object { $_.Name -eq $required }).Count -eq 1 }).Count
    $summary = [ordered]@{
        SchemaVersion = 3
        GeneratedUtc = [DateTime]::UtcNow.ToString('o')
        Command = ".\scripts\Test-NetStuck.ps1 -SoakSeconds $SoakSeconds"
        SoakSeconds = $SoakSeconds
        Host = [ordered]@{
            Edition = $(if ($PSVersionTable.PSEdition) { $PSVersionTable.PSEdition } else { 'Desktop' })
            Version = $PSVersionTable.PSVersion.ToString()
            Executable = $powerShellExecutable
        }
        Status = $(if ($Reconciliation.Passed) { 'Passed' } else { 'Failed' })
        Verdict = $(if ($Reconciliation.Passed) { 'PASS' } else { 'FAIL' })
        RequiredStages = @($requiredStageNames)
        RequiredSuites = @($requiredSuiteNames)
        SuiteMinimums = $requiredSuiteMinimums
        Stages = @($stageResults | ForEach-Object { $_ })
        Suites = @($suiteResults | ForEach-Object { $_ })
        Totals = [ordered]@{
            Discovered = [int]($passed + $failed + $skipped)
            Passed = [int]$passed
            Failed = [int]$failed
            Skipped = [int]$skipped
            InfrastructureFailures = [int]$Reconciliation.InfrastructureFailures
            RequiredSuites = [int]$requiredSuiteNames.Count
            CompletedRequiredSuites = [int]$completedRequiredSuites
        }
        Reconciliation = [ordered]@{
            InventoryMatch = [bool]($completedRequiredSuites -eq $requiredSuiteNames.Count)
            Issues = @($Reconciliation.Issues)
            ExitCode = [int]$Reconciliation.RecommendedExitCode
        }
        Error = $(if ($null -eq $RunError) { $null } else { $RunError.GetType().Name + ': ' + $RunError.Message })
    }
    [System.IO.File]::WriteAllText($resolvedSummaryPath, ($summary | ConvertTo-Json -Depth 8), (New-Object System.Text.UTF8Encoding($false)))
    Write-Output "`nTest summary: $resolvedSummaryPath"
    Write-Output "PowerShell host: $($summary.Host.Edition) $($summary.Host.Version)"
    Write-Output "Run totals (completed stage output only): Discovered=$($summary.Totals.Discovered); Passed=$($summary.Totals.Passed); Failed=$($summary.Totals.Failed); Skipped=$($summary.Totals.Skipped); Infrastructure=$($summary.Totals.InfrastructureFailures)"
    Write-Output "Mandatory suite inventory: $($summary.Totals.CompletedRequiredSuites)/$($summary.Totals.RequiredSuites)"
    Write-Output "Overall: $($summary.Verdict)"
    if (-not $Reconciliation.Passed) { Write-Output "Failure stage: $($Reconciliation.Issues[0])" }
}

$appSources = @(Get-NetStuckProductionSourcePaths | ForEach-Object { Join-Path $repoRoot ($_.Replace('/', '\')) })
Assert-NetStuckProductionSourceDirectory -RepositoryRoot $repoRoot
$frameworkReferenceInventory = @(Get-NetStuckFrameworkReferenceInventory -CompilerPath $compiler)
$frameworkReferences = @($frameworkReferenceInventory | ForEach-Object { '/reference:' + $_.FullPath })
$compilerIsolationArguments = @('/nologo', '/noconfig', '/nostdlib+')
$uiLibrary = Join-Path $outputRoot 'NetStuck.UI.dll'

$runError = $null
$currentStage = 'Preflight'
try {
    if (-not (Test-Path -LiteralPath $compiler)) { throw "The .NET Framework compiler was not found: $compiler" }
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

    $currentStage = 'Test host compilation'
    Invoke-Compiler -Arguments ($compilerIsolationArguments + @('/target:library', '/optimize+', "/out:$uiLibrary") + $frameworkReferences + $appSources)
    Invoke-Compiler -Arguments ($compilerIsolationArguments + @('/target:exe', '/optimize+', "/out:$(Join-Path $outputRoot 'FakePlink.exe')") + $frameworkReferences + (Join-Path $testRoot 'FakePlink.cs'))
    Invoke-Compiler -Arguments ($compilerIsolationArguments + @('/target:exe', '/optimize+', "/out:$(Join-Path $outputRoot 'NetOpsCoreTests.exe')") + $frameworkReferences + (Join-Path $sourceRoot 'NetOpsCore.cs') + (Join-Path $testRoot 'NetOpsCoreTests.cs'))
    $uiTestReferences = $frameworkReferences + "/reference:$uiLibrary"
    foreach ($name in @('FeatureTests', 'TracerouteLifecycleTests', 'UiFoundationTests', 'PerformanceTests', 'PollingCadenceTests', 'OvernightSoakTests')) {
        Invoke-Compiler -Arguments ($compilerIsolationArguments + @('/target:exe', '/optimize+', "/out:$(Join-Path $outputRoot ($name + '.exe'))") + $uiTestReferences + (Join-Path $testRoot ($name + '.cs')))
    }
    Add-StageResult -Name $currentStage -Status 'Passed' -NativeExitCode 0 -InfrastructureFailures 0 -Detail 'All production and test hosts compiled.'

    $currentStage = 'Development build'
    $buildScript = Join-Path $PSScriptRoot 'Build-NetStuck.ps1'
    $buildResult = Invoke-NativeProcess -FilePath $powerShellExecutable -ArgumentList @(
        '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $buildScript,
        '-OutputDirectory', (Join-Path $repoRoot 'artifacts\build')
    ) -WorkingDirectory $repoRoot
    Write-NativeProcessOutput -Result $buildResult
    if (-not $buildResult.InvocationSucceeded) { throw "Development build invocation failed [$($buildResult.InvocationErrorType)]: $($buildResult.InvocationErrorMessage)" }
    if ($buildResult.ExitCode -ne 0) { throw "Development build failed with exit code $($buildResult.ExitCode)." }
    Add-StageResult -Name $currentStage -Status 'Passed' -NativeExitCode $buildResult.ExitCode -InfrastructureFailures 0 -Detail 'NetStuck.exe built successfully.'

    $currentStage = 'Test runner infrastructure'
    Invoke-RunnerInfrastructureTests
    $currentStage = 'NetOpsCoreTests.exe'
    Invoke-TestExecutable 'NetOpsCoreTests.exe'
    $currentStage = 'FeatureTests.exe'
    Invoke-TestExecutable 'FeatureTests.exe'
    $currentStage = 'TracerouteLifecycleTests.exe'
    Invoke-TestExecutable 'TracerouteLifecycleTests.exe'
    $currentStage = 'UiFoundationTests.exe'
    Invoke-TestExecutable 'UiFoundationTests.exe'
    $currentStage = 'PerformanceTests.exe'
    Invoke-TestExecutable 'PerformanceTests.exe'
    $currentStage = 'PollingCadenceTests.exe'
    Invoke-TestExecutable 'PollingCadenceTests.exe'
    $currentStage = 'OvernightSoakTests.exe'
    Invoke-TestExecutable 'OvernightSoakTests.exe' @('--seconds', $SoakSeconds.ToString())
    $currentStage = 'Capture-UiFoundations infrastructure'
    Invoke-CaptureInfrastructureTests
    $currentStage = 'Build provenance infrastructure'
    Invoke-BuildProvenanceInfrastructureTests
}
catch {
    $runError = $_.Exception
    if ($requiredStageNames -contains $currentStage -and @($stageResults | Where-Object { $_.Name -eq $currentStage }).Count -eq 0) {
        Add-StageResult -Name $currentStage -Status 'Failed' -NativeExitCode $null -InfrastructureFailures 1 -Detail ($runError.GetType().Name + ': ' + $runError.Message)
    }
}

$reconciliation = Get-RunReconciliation -Suites $suiteResults.ToArray() -Stages $stageResults.ToArray() -RequiredSuites $requiredSuiteNames -RequiredStages $requiredStageNames -MinimumExpectedCounts $requiredSuiteMinimums -RunError $runError
Write-TestSummary -RunError $runError -Reconciliation $reconciliation
if (-not $reconciliation.Passed) { exit $reconciliation.RecommendedExitCode }

Write-Output "`nAll NetStuck mandatory test stages passed."
