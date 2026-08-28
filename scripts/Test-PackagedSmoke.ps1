[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,
    [ValidateRange(5, 60)]
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = 'Stop'
$executable = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Packaged smoke executable is missing.' }
$ownedRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('NetStuck-packaged-smoke-' + [Guid]::NewGuid().ToString('N'))
$ownedRoot = [System.IO.Path]::GetFullPath($ownedRoot)
New-Item -ItemType Directory -Path $ownedRoot | Out-Null

$process = $null
$primaryError = $null
$processCleanupError = $null
$stateCleanupError = $null
try {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $executable
    $startInfo.WorkingDirectory = Split-Path -Parent $executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Normal
    $startInfo.EnvironmentVariables['NETSTUCK_TEST_ROOT'] = $ownedRoot
    [void]$startInfo.EnvironmentVariables.Remove('NETSTUCK_TEST_STATE_PATH')
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) { throw 'Packaged smoke Process.Start returned false.' }
    $inputIdle = $process.WaitForInputIdle($TimeoutSeconds * 1000)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $windowReady = $false
    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds -and -not $process.HasExited) {
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero -and $process.Responding) { $windowReady = $true; break }
        Start-Sleep -Milliseconds 50
    }
    if (-not $inputIdle -or -not $windowReady) { throw 'Packaged smoke did not reach a responsive native window before timeout.' }
    Write-Output 'PASS packaged smoke responsive native window reached'
    if (-not $process.CloseMainWindow()) { throw 'Packaged smoke native window rejected CloseMainWindow.' }
    Write-Output 'PASS packaged smoke graceful close requested'
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        throw 'Packaged smoke did not exit after graceful close; forced cleanup was required.'
    }
    if ($process.ExitCode -ne 0) { throw "Packaged smoke exited with code $($process.ExitCode)." }
    Write-Output 'PASS packaged smoke exited zero without process leak'

    $operatorProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    $ownedFiles = @(Get-ChildItem -LiteralPath $ownedRoot -Recurse -Force -File)
    $stateFiles = @($ownedFiles | Where-Object { $_.Name -ceq 'state.json' })
    if ($stateFiles.Count -ne 1) { throw "Packaged smoke expected one owned state.json; actual count is $($stateFiles.Count)." }
    foreach ($file in $ownedFiles) {
        $raw = [System.IO.File]::ReadAllText($file.FullName)
        if (-not [String]::IsNullOrWhiteSpace($operatorProfile) -and $raw.IndexOf($operatorProfile, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw 'Packaged smoke output retained an operator-profile path.'
        }
    }
    $credentialFields = @('CollectorAuth1Pass','CollectorAuth2Pass','CollectorAuth1Secret','CollectorAuth2Secret','Password','Secret','PrivateKey')
    foreach ($file in $stateFiles) {
        $raw = [System.IO.File]::ReadAllText($file.FullName)
        $state = $raw | ConvertFrom-Json
        foreach ($field in $credentialFields) {
            $property = $state.PSObject.Properties[$field]
            if ($null -ne $property -and -not [String]::IsNullOrWhiteSpace([string]$property.Value)) {
                throw 'Packaged smoke state retained credential content.'
            }
        }
    }
    Write-Output "PASS packaged smoke owned outputs validated: $($ownedFiles.Count) file(s), one state.json"
    Write-Output 'PASS packaged smoke outputs have no operator-profile path'
    Write-Output 'PASS packaged smoke state has no credential content'
}
catch { $primaryError = $_ }
finally {
    if ($null -ne $process) {
        try {
            if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        }
        catch { $processCleanupError = $_ }
        $process.Dispose()
    }
    try {
        $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $insideTemporaryRoot = $ownedRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)
        $ownedLeafName = [System.IO.Path]::GetFileName($ownedRoot)
        $hasOwnedPrefix = $ownedLeafName.StartsWith('NetStuck-packaged-smoke-', [StringComparison]::Ordinal)
        if (-not ($insideTemporaryRoot -and $hasOwnedPrefix)) {
            throw 'Packaged smoke cleanup target is not an owned temporary root.'
        }
        if (Test-Path -LiteralPath $ownedRoot) { Remove-Item -LiteralPath $ownedRoot -Recurse -Force -ErrorAction Stop }
        if (Test-Path -LiteralPath $ownedRoot) { throw 'Packaged smoke owned root remains after cleanup.' }
    }
    catch { $stateCleanupError = $_ }
}

$cleanupFailures = @($processCleanupError, $stateCleanupError | Where-Object { $null -ne $_ })
if ($null -ne $primaryError -and $cleanupFailures.Count -gt 0) {
    $cleanupTypes = @($cleanupFailures | ForEach-Object { $_.Exception.GetType().Name }) -join ', '
    throw "Packaged smoke failed and cleanup also failed. Primary: $($primaryError.Exception.GetType().Name); cleanup: $cleanupTypes."
}
if ($null -ne $primaryError) { throw $primaryError }
if ($cleanupFailures.Count -gt 0) {
    $cleanupTypes = @($cleanupFailures | ForEach-Object { $_.Exception.GetType().Name }) -join ', '
    throw "Packaged smoke cleanup failed: $cleanupTypes."
}
Write-Output 'PASS packaged smoke owned state root removed'
