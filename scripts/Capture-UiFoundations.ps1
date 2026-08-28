[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$RunInfrastructureTests,
    [ValidateRange(2, 5)]
    [int]$DeterminismRuns = 2
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src\NetStuck'
$testRoot = Join-Path $repoRoot 'tests'
$artifactRoot = Join-Path $repoRoot 'artifacts\ui-foundations'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

$expected = @(
    [pscustomobject]@{ Name = 'main-shell-1100x900.png'; Scenario = 'main-shell-1100x900'; Width = 1100; Height = 900 },
    [pscustomobject]@{ Name = 'main-shell-1460x900.png'; Scenario = 'main-shell-1460x900'; Width = 1460; Height = 900 },
    [pscustomobject]@{ Name = 'pilot-calculators-1100x700.png'; Scenario = 'pilot-calculators-1100x700'; Width = 1100; Height = 700 },
    [pscustomobject]@{ Name = 'pilot-calculators-1100x900.png'; Scenario = 'pilot-calculators-1100x900'; Width = 1100; Height = 900 },
    [pscustomobject]@{ Name = 'pilot-calculators-1460x900.png'; Scenario = 'pilot-calculators-1460x900'; Width = 1460; Height = 900 },
    [pscustomobject]@{ Name = 'pilot-calculators-validation-1100x900.png'; Scenario = 'pilot-calculators-validation-1100x900'; Width = 1100; Height = 900 },
    [pscustomobject]@{ Name = 'pilot-event-log-empty-1100x700.png'; Scenario = 'pilot-event-log-empty-1100x700'; Width = 1100; Height = 700 },
    [pscustomobject]@{ Name = 'pilot-event-log-empty-1100x900.png'; Scenario = 'pilot-event-log-empty-1100x900'; Width = 1100; Height = 900 },
    [pscustomobject]@{ Name = 'pilot-event-log-filtered-empty-1460x900.png'; Scenario = 'pilot-event-log-filtered-empty-1460x900'; Width = 1460; Height = 900 }
)

function New-OwnedTemporaryDirectory {
    param([string]$Prefix)
    $path = Join-Path ([System.IO.Path]::GetTempPath()) ($Prefix + [Guid]::NewGuid().ToString('N'))
    $path = [System.IO.Path]::GetFullPath($path)
    New-Item -ItemType Directory -Path $path | Out-Null
    return $path
}

function Remove-OwnedDirectory {
    param(
        [string]$Path,
        [string]$OwnedParent,
        [string]$RequiredLeafPrefix
    )
    $full = [System.IO.Path]::GetFullPath($Path)
    $parent = [System.IO.Path]::GetFullPath($OwnedParent).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $leaf = [System.IO.Path]::GetFileName($full)
    if (-not $full.StartsWith($parent, [System.StringComparison]::OrdinalIgnoreCase) -or -not $leaf.StartsWith($RequiredLeafPrefix, [System.StringComparison]::Ordinal)) {
        throw "Refusing to remove a directory that is not owned by UI capture: $leaf"
    }
    if (Test-Path -LiteralPath $full) {
        Remove-Item -LiteralPath $full -Recurse -Force -ErrorAction Stop
    }
    if (Test-Path -LiteralPath $full) {
        throw "Owned UI-capture directory still exists after cleanup: $leaf"
    }
}

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

function Assert-NativeProcessSucceeded {
    param([object]$Result, [string]$Stage)
    @($Result.StdOutLines) | ForEach-Object { Write-Host $_ }
    @($Result.StdErrLines) | ForEach-Object { Write-Host $_ }
    if (-not $Result.InvocationSucceeded) {
        throw "$Stage invocation failed [$($Result.InvocationErrorType)]: $($Result.InvocationErrorMessage)"
    }
    if ($Result.ExitCode -ne 0) { throw "$Stage failed with exit code $($Result.ExitCode)." }
}

function Invoke-CaptureHost {
    param(
        [string]$CandidateDirectory,
        [string]$Fault = ''
    )
    $priorFault = [Environment]::GetEnvironmentVariable('NETSTUCK_CAPTURE_FAULT')
    try {
        [Environment]::SetEnvironmentVariable('NETSTUCK_CAPTURE_FAULT', $(if ($Fault) { $Fault } else { $null }))
        $native = Invoke-NativeProcess -FilePath $captureExecutable -ArgumentList @($CandidateDirectory)
        if (-not $native.InvocationSucceeded) {
            throw "Capture host invocation failed [$($native.InvocationErrorType)]: $($native.InvocationErrorMessage)"
        }
        return [pscustomobject]@{
            InvocationSucceeded = $true
            ExitCode = $native.ExitCode
            StdOutLines = @($native.StdOutLines)
            StdErrLines = @($native.StdErrLines)
            Lines = @($native.StdOutLines) + @($native.StdErrLines)
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('NETSTUCK_CAPTURE_FAULT', $priorFault)
    }
}

function Assert-SemanticResults {
    param([string[]]$Lines)
    $failures = @($Lines | Where-Object { $_ -match '^SEMANTIC FAIL ' })
    if ($failures.Count -ne 0) {
        throw "Semantic capture reported a failure: $($failures -join ' | ')"
    }
    $actual = @($Lines | ForEach-Object {
        if ($_ -match '^SEMANTIC PASS (?<scenario>[^ ]+) ') { $Matches['scenario'] }
    } | Where-Object { $_ })
    $expectedNames = @($expected | ForEach-Object { $_.Scenario })
    if ($actual.Count -ne $expectedNames.Count) {
        throw "Semantic scenario count mismatch: expected $($expectedNames.Count), actual $($actual.Count)."
    }
    foreach ($name in $expectedNames) {
        if (@($actual | Where-Object { $_ -eq $name }).Count -ne 1) {
            throw "Semantic scenario did not pass exactly once: $name"
        }
    }
}

function Read-BigEndianUInt32 {
    param([byte[]]$Bytes, [int]$Offset)
    return [uint32]((([uint32]$Bytes[$Offset]) -shl 24) -bor
        (([uint32]$Bytes[$Offset + 1]) -shl 16) -bor
        (([uint32]$Bytes[$Offset + 2]) -shl 8) -bor
        ([uint32]$Bytes[$Offset + 3]))
}

function Set-BigEndianUInt32 {
    param([byte[]]$Bytes, [int]$Offset, [uint32]$Value)
    $Bytes[$Offset] = [byte](($Value -shr 24) -band 0xff)
    $Bytes[$Offset + 1] = [byte](($Value -shr 16) -band 0xff)
    $Bytes[$Offset + 2] = [byte](($Value -shr 8) -band 0xff)
    $Bytes[$Offset + 3] = [byte]($Value -band 0xff)
}

function Get-Crc32 {
    param([byte[]]$Bytes, [int]$Offset, [int]$Count)
    [uint32]$crc = [uint32]::MaxValue
    # Use the decimal UInt32 value because Windows PowerShell 5.1 parses the
    # equivalent hexadecimal literal as a negative Int32 before conversion.
    [uint32]$polynomial = 3988292384
    for ($index = 0; $index -lt $Count; $index++) {
        $crc = [uint32]($crc -bxor [uint32]$Bytes[$Offset + $index])
        for ($bit = 0; $bit -lt 8; $bit++) {
            if (($crc -band 1) -ne 0) {
                $crc = [uint32](($crc -shr 1) -bxor $polynomial)
            }
            else {
                $crc = [uint32]($crc -shr 1)
            }
        }
    }
    return [uint32]($crc -bxor [uint32]::MaxValue)
}

function Get-PngChunkTypes {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $fileName = [System.IO.Path]::GetFileName($Path)
    $signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    $allowedChunkTypes = @('IHDR', 'sRGB', 'gAMA', 'pHYs', 'IDAT', 'IEND')
    if ($bytes.Length -lt 20) { throw "PNG is too short: $fileName" }
    for ($i = 0; $i -lt $signature.Length; $i++) {
        if ($bytes[$i] -ne $signature[$i]) { throw "Invalid PNG signature: $fileName" }
    }

    $types = New-Object System.Collections.Generic.List[string]
    $offset = 8
    while ($offset -lt $bytes.Length) {
        if ($offset -gt $bytes.Length - 12) { throw "PNG chunk header is truncated: $fileName" }
        [uint32]$lengthValue = Read-BigEndianUInt32 -Bytes $bytes -Offset $offset
        [int64]$length = $lengthValue
        [int64]$chunkEnd = [int64]$offset + 12L + $length
        if ($length -gt [int]::MaxValue -or $chunkEnd -gt $bytes.Length) {
            throw "PNG chunk data is truncated or has an invalid length in $fileName."
        }
        $type = [System.Text.Encoding]::ASCII.GetString($bytes, $offset + 4, 4)
        if ($type -notmatch '^[A-Za-z]{4}$') { throw "Invalid PNG chunk type in $fileName." }
        if ($allowedChunkTypes -notcontains $type) {
            $kind = if ([Char]::IsUpper($type[0])) { 'critical ' } else { '' }
            throw "Unexpected ${kind}PNG chunk '$type' in $fileName."
        }
        $storedCrcOffset = $offset + 8 + [int]$length
        [uint32]$storedCrc = Read-BigEndianUInt32 -Bytes $bytes -Offset $storedCrcOffset
        [uint32]$actualCrc = Get-Crc32 -Bytes $bytes -Offset ($offset + 4) -Count (4 + [int]$length)
        if ($storedCrc -ne $actualCrc) { throw "PNG CRC mismatch for chunk '$type' in $fileName." }
        if ($type -eq 'IHDR' -and $length -ne 13) { throw "Invalid IHDR length in $fileName." }
        if ($type -eq 'IEND' -and $length -ne 0) { throw "Invalid IEND length in $fileName." }
        $types.Add($type)
        $offset += 12 + [int]$length
        if ($type -eq 'IEND') { break }
    }

    if ($types.Count -eq 0 -or $types[0] -ne 'IHDR') { throw "PNG IHDR must be the first chunk: $fileName" }
    if (@($types | Where-Object { $_ -eq 'IHDR' }).Count -ne 1) { throw "PNG must contain exactly one IHDR chunk: $fileName" }
    if (@($types | Where-Object { $_ -eq 'IEND' }).Count -ne 1 -or $types[$types.Count - 1] -ne 'IEND') {
        throw "PNG must contain exactly one terminal IEND chunk: $fileName"
    }

    $idatIndexes = @()
    for ($index = 0; $index -lt $types.Count; $index++) {
        if ($types[$index] -eq 'IDAT') { $idatIndexes += $index }
    }
    if ($idatIndexes.Count -eq 0) { throw "PNG IEND appears before image data or IDAT is missing: $fileName" }
    if (($idatIndexes[$idatIndexes.Count - 1] - $idatIndexes[0] + 1) -ne $idatIndexes.Count) {
        throw "PNG IDAT chunks must be contiguous: $fileName"
    }

    foreach ($singleton in @('sRGB', 'gAMA', 'pHYs')) {
        $indexes = @()
        for ($index = 0; $index -lt $types.Count; $index++) {
            if ($types[$index] -eq $singleton) { $indexes += $index }
        }
        if ($indexes.Count -gt 1) { throw "PNG singleton chunk '$singleton' is duplicated in $fileName." }
        if ($indexes.Count -eq 1 -and $indexes[0] -gt $idatIndexes[0]) {
            throw "PNG chunk '$singleton' must appear before the first IDAT in $fileName."
        }
    }
    if ($offset -ne $bytes.Length) { throw "PNG contains data or chunks after IEND: $fileName" }
    return $types.ToArray()
}

function Test-ScreenshotSet {
    param([string]$CandidateDirectory)
    $candidate = [System.IO.Path]::GetFullPath($CandidateDirectory)
    if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { throw "Screenshot candidate directory is missing." }
    $files = @(Get-ChildItem -LiteralPath $candidate -File -Recurse -Filter '*.png')
    if ($files.Count -ne $expected.Count) {
        throw "Screenshot inventory mismatch: expected $($expected.Count), actual $($files.Count)."
    }
    $expectedNames = @($expected | ForEach-Object { $_.Name })
    foreach ($file in $files) {
        if ($expectedNames -notcontains $file.Name) { throw "Unexpected screenshot: $($file.Name)" }
        if ($file.DirectoryName -ne $candidate) { throw "Screenshots must be direct children of the candidate directory: $($file.Name)" }
    }

    Add-Type -AssemblyName System.Drawing
    foreach ($item in $expected) {
        $path = Join-Path $candidate $item.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing expected screenshot: $($item.Name)" }
        $image = [System.Drawing.Image]::FromFile($path)
        try {
            if ($image.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid) { throw "Unexpected image format for $($item.Name)." }
            if ($image.Width -ne $item.Width -or $image.Height -ne $item.Height) {
                throw "Unexpected screenshot dimensions for $($item.Name): $($image.Width)x$($image.Height)"
            }
        }
        finally { $image.Dispose() }
        [void]@(Get-PngChunkTypes -Path $path)
    }
}

function Copy-ScreenshotSet {
    param([string]$SourceDirectory, [string]$DestinationDirectory)
    if (-not (Test-Path -LiteralPath $DestinationDirectory)) {
        New-Item -ItemType Directory -Path $DestinationDirectory | Out-Null
    }
    foreach ($item in $expected) {
        Copy-Item -LiteralPath (Join-Path $SourceDirectory $item.Name) -Destination (Join-Path $DestinationDirectory $item.Name)
    }
}

function Get-ScreenshotHashMap {
    param([string]$Directory)
    $hashes = @{}
    foreach ($item in $expected) {
        $hashes[$item.Name] = (Get-FileHash -LiteralPath (Join-Path $Directory $item.Name) -Algorithm SHA256).Hash
    }
    return $hashes
}

function Test-ScreenshotHashMap {
    param([hashtable]$ExpectedHashes, [string]$Directory)
    foreach ($item in $expected) {
        $path = Join-Path $Directory $item.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $ExpectedHashes[$item.Name]) { return $false }
    }
    return $true
}

function Set-UnexpectedPngChunkType {
    param([string]$Path)
    [byte[]]$bytes = [System.IO.File]::ReadAllBytes($Path)
    $offset = 8
    $changed = $false
    while ($offset + 12 -le $bytes.Length) {
        [int]$length = [int](Read-BigEndianUInt32 -Bytes $bytes -Offset $offset)
        $type = [System.Text.Encoding]::ASCII.GetString($bytes, $offset + 4, 4)
        if ($type -in @('sRGB', 'gAMA', 'pHYs')) {
            [byte[]]$replacement = [System.Text.Encoding]::ASCII.GetBytes('vpAg')
            [Array]::Copy($replacement, 0, $bytes, $offset + 4, 4)
            [uint32]$crc = Get-Crc32 -Bytes $bytes -Offset ($offset + 4) -Count (4 + $length)
            Set-BigEndianUInt32 -Bytes $bytes -Offset ($offset + 8 + $length) -Value $crc
            $changed = $true
            break
        }
        $offset += 12 + $length
        if ($type -eq 'IEND') { break }
    }
    if (-not $changed) { throw 'Unable to create the unexpected PNG chunk fixture.' }
    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

function Set-PngPhysicalResolutionFixtureByte {
    param([string]$Path)
    [byte[]]$bytes = [System.IO.File]::ReadAllBytes($Path)
    $offset = 8
    while ($offset + 12 -le $bytes.Length) {
        [int]$length = [int](Read-BigEndianUInt32 -Bytes $bytes -Offset $offset)
        $type = [System.Text.Encoding]::ASCII.GetString($bytes, $offset + 4, 4)
        if ($type -eq 'pHYs') {
            if ($length -ne 9) { throw 'Fixture pHYs chunk has an unexpected length.' }
            $bytes[$offset + 8] = [byte]($bytes[$offset + 8] -bxor 1)
            [uint32]$crc = Get-Crc32 -Bytes $bytes -Offset ($offset + 4) -Count (4 + $length)
            Set-BigEndianUInt32 -Bytes $bytes -Offset ($offset + 8 + $length) -Value $crc
            [System.IO.File]::WriteAllBytes($Path, $bytes)
            return
        }
        $offset += 12 + $length
    }
    throw 'Fixture PNG does not contain pHYs.'
}

function Get-PngRawChunkRecords {
    param([string]$Path)
    [byte[]]$bytes = [System.IO.File]::ReadAllBytes($Path)
    $records = New-Object System.Collections.Generic.List[object]
    $offset = 8
    while ($offset -lt $bytes.Length) {
        if ($offset -gt $bytes.Length - 12) { throw 'Fixture PNG chunk header is truncated.' }
        [int64]$length = Read-BigEndianUInt32 -Bytes $bytes -Offset $offset
        [int64]$total = 12L + $length
        if ($length -gt [int]::MaxValue -or $offset + $total -gt $bytes.Length) { throw 'Fixture PNG chunk is truncated.' }
        $type = [System.Text.Encoding]::ASCII.GetString($bytes, $offset + 4, 4)
        [byte[]]$raw = New-Object byte[] ([int]$total)
        [Array]::Copy($bytes, $offset, $raw, 0, [int]$total)
        $records.Add([pscustomobject]@{ Type = $type; Raw = $raw })
        $offset += [int]$total
    }
    return $records.ToArray()
}

function Write-PngRawChunkRecords {
    param([string]$Path, [object[]]$Records)
    [byte[]]$signature = 137, 80, 78, 71, 13, 10, 26, 10
    $stream = New-Object System.IO.MemoryStream
    try {
        $stream.Write($signature, 0, $signature.Length)
        foreach ($record in $Records) {
            [byte[]]$raw = $record.Raw
            $stream.Write($raw, 0, $raw.Length)
        }
        [System.IO.File]::WriteAllBytes($Path, $stream.ToArray())
    }
    finally { $stream.Dispose() }
}

function New-PngRawChunkRecord {
    param([string]$Type, [byte[]]$Data = [byte[]]@())
    if ($Type -notmatch '^[A-Za-z]{4}$') { throw "Invalid fixture chunk type: $Type" }
    [byte[]]$raw = New-Object byte[] (12 + $Data.Length)
    Set-BigEndianUInt32 -Bytes $raw -Offset 0 -Value ([uint32]$Data.Length)
    [byte[]]$typeBytes = [System.Text.Encoding]::ASCII.GetBytes($Type)
    [Array]::Copy($typeBytes, 0, $raw, 4, 4)
    if ($Data.Length -gt 0) { [Array]::Copy($Data, 0, $raw, 8, $Data.Length) }
    [uint32]$crc = Get-Crc32 -Bytes $raw -Offset 4 -Count (4 + $Data.Length)
    Set-BigEndianUInt32 -Bytes $raw -Offset (8 + $Data.Length) -Value $crc
    return [pscustomobject]@{ Type = $Type; Raw = $raw }
}

function Move-PngChunkAfterIdat {
    param([string]$Path, [string]$ChunkType)
    $records = New-Object System.Collections.Generic.List[object]
    @(Get-PngRawChunkRecords -Path $Path) | ForEach-Object { $records.Add($_) }
    $chunkIndex = -1
    for ($index = 0; $index -lt $records.Count; $index++) { if ($records[$index].Type -eq $ChunkType) { $chunkIndex = $index; break } }
    if ($chunkIndex -lt 0) { throw "Fixture PNG does not contain $ChunkType." }
    $record = $records[$chunkIndex]
    $records.RemoveAt($chunkIndex)
    $lastIdat = -1
    for ($index = 0; $index -lt $records.Count; $index++) { if ($records[$index].Type -eq 'IDAT') { $lastIdat = $index } }
    if ($lastIdat -lt 0) { throw 'Fixture PNG does not contain IDAT.' }
    $records.Insert($lastIdat + 1, $record)
    Write-PngRawChunkRecords -Path $Path -Records $records.ToArray()
}

function Duplicate-PngChunk {
    param([string]$Path, [string]$ChunkType)
    $records = New-Object System.Collections.Generic.List[object]
    @(Get-PngRawChunkRecords -Path $Path) | ForEach-Object { $records.Add($_) }
    $chunkIndex = -1
    for ($index = 0; $index -lt $records.Count; $index++) { if ($records[$index].Type -eq $ChunkType) { $chunkIndex = $index; break } }
    if ($chunkIndex -lt 0) { throw "Fixture PNG does not contain $ChunkType." }
    [byte[]]$copy = New-Object byte[] $records[$chunkIndex].Raw.Length
    [Array]::Copy($records[$chunkIndex].Raw, $copy, $copy.Length)
    $records.Insert($chunkIndex + 1, [pscustomobject]@{ Type = $ChunkType; Raw = $copy })
    Write-PngRawChunkRecords -Path $Path -Records $records.ToArray()
}

function Move-PngIendBeforeIdat {
    param([string]$Path)
    $records = New-Object System.Collections.Generic.List[object]
    @(Get-PngRawChunkRecords -Path $Path) | ForEach-Object { $records.Add($_) }
    $iend = $records | Where-Object { $_.Type -eq 'IEND' } | Select-Object -First 1
    if ($null -eq $iend) { throw 'Fixture PNG does not contain IEND.' }
    [void]$records.Remove($iend)
    $firstIdat = -1
    for ($index = 0; $index -lt $records.Count; $index++) { if ($records[$index].Type -eq 'IDAT') { $firstIdat = $index; break } }
    if ($firstIdat -lt 0) { throw 'Fixture PNG does not contain IDAT.' }
    $records.Insert($firstIdat, $iend)
    Write-PngRawChunkRecords -Path $Path -Records $records.ToArray()
}

function Append-PngChunkAfterIend {
    param([string]$Path)
    $records = New-Object System.Collections.Generic.List[object]
    @(Get-PngRawChunkRecords -Path $Path) | ForEach-Object { $records.Add($_) }
    $metadata = $records | Where-Object { $_.Type -in @('sRGB', 'gAMA', 'pHYs') } | Select-Object -First 1
    if ($null -eq $metadata) { throw 'Fixture PNG has no supported metadata chunk.' }
    $records.Add($metadata)
    Write-PngRawChunkRecords -Path $Path -Records $records.ToArray()
}

function Split-PngIdatWithMetadata {
    param([string]$Path)
    $records = New-Object System.Collections.Generic.List[object]
    @(Get-PngRawChunkRecords -Path $Path) | ForEach-Object { $records.Add($_) }
    $idatIndex = -1
    for ($index = 0; $index -lt $records.Count; $index++) { if ($records[$index].Type -eq 'IDAT') { $idatIndex = $index; break } }
    if ($idatIndex -lt 0) { throw 'Fixture PNG does not contain IDAT.' }
    $idat = $records[$idatIndex]
    [int]$length = [int](Read-BigEndianUInt32 -Bytes $idat.Raw -Offset 0)
    if ($length -lt 2) { throw 'Fixture IDAT is too short to split.' }
    [int]$firstLength = [int][Math]::Floor($length / 2.0)
    [byte[]]$firstData = New-Object byte[] $firstLength
    [byte[]]$secondData = New-Object byte[] ($length - $firstLength)
    [Array]::Copy($idat.Raw, 8, $firstData, 0, $firstData.Length)
    [Array]::Copy($idat.Raw, 8 + $firstData.Length, $secondData, 0, $secondData.Length)
    $records.RemoveAt($idatIndex)
    $metadataIndex = -1
    for ($index = 0; $index -lt $records.Count; $index++) { if ($records[$index].Type -eq 'pHYs') { $metadataIndex = $index; break } }
    if ($metadataIndex -lt 0) { throw 'Fixture PNG does not contain pHYs.' }
    $metadata = $records[$metadataIndex]
    $records.RemoveAt($metadataIndex)
    $iendIndex = -1
    for ($index = 0; $index -lt $records.Count; $index++) { if ($records[$index].Type -eq 'IEND') { $iendIndex = $index; break } }
    $records.Insert($iendIndex, (New-PngRawChunkRecord -Type 'IDAT' -Data $firstData))
    $records.Insert($iendIndex + 1, $metadata)
    $records.Insert($iendIndex + 2, (New-PngRawChunkRecord -Type 'IDAT' -Data $secondData))
    Write-PngRawChunkRecords -Path $Path -Records $records.ToArray()
}

function Set-PngChunkType {
    param([string]$Path, [string]$FromType, [string]$ToType)
    [byte[]]$bytes = [System.IO.File]::ReadAllBytes($Path)
    $offset = 8
    while ($offset + 12 -le $bytes.Length) {
        [int]$length = [int](Read-BigEndianUInt32 -Bytes $bytes -Offset $offset)
        $type = [System.Text.Encoding]::ASCII.GetString($bytes, $offset + 4, 4)
        if ($type -eq $FromType) {
            [byte[]]$replacement = [System.Text.Encoding]::ASCII.GetBytes($ToType)
            [Array]::Copy($replacement, 0, $bytes, $offset + 4, 4)
            [uint32]$crc = Get-Crc32 -Bytes $bytes -Offset ($offset + 4) -Count (4 + $length)
            Set-BigEndianUInt32 -Bytes $bytes -Offset ($offset + 8 + $length) -Value $crc
            [System.IO.File]::WriteAllBytes($Path, $bytes)
            return
        }
        $offset += 12 + $length
    }
    throw "Fixture PNG does not contain $FromType."
}

function Assert-OutputTarget {
    param([string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $root = [System.IO.Path]::GetPathRoot($full).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ([String]::IsNullOrWhiteSpace([System.IO.Path]::GetFileName($full)) -or $full -eq $root -or $full -eq [System.IO.Path]::GetFullPath($repoRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)) {
        throw "Unsafe screenshot output target."
    }
    return $full
}

function Publish-ScreenshotSet {
    param(
        [string]$CandidateDirectory,
        [string]$TargetDirectory,
        [string[]]$Faults = @()
    )
    $target = Assert-OutputTarget -Path $TargetDirectory
    $parent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $leaf = [System.IO.Path]::GetFileName($target)
    $promotionPrefix = '.' + $leaf + '.promotion-'
    $backupPrefix = '.' + $leaf + '.backup-'
    $promotion = Join-Path $parent ($promotionPrefix + [Guid]::NewGuid().ToString('N'))
    $backup = Join-Path $parent ($backupPrefix + [Guid]::NewGuid().ToString('N'))
    $failedPrefix = '.' + $leaf + '.failed-'
    $failed = Join-Path $parent ($failedPrefix + [Guid]::NewGuid().ToString('N'))
    $targetExisted = Test-Path -LiteralPath $target
    $priorHashes = if ($targetExisted) { Get-ScreenshotHashMap -Directory $target } else { $null }
    $backupCreated = $false
    $targetInstalled = $false

    if (Test-Path -LiteralPath $target) {
        $existing = @(Get-ChildItem -LiteralPath $target -Force)
        $expectedNames = @($expected | ForEach-Object { $_.Name })
        $unexpected = @($existing | Where-Object { $_.PSIsContainer -or $expectedNames -notcontains $_.Name })
        if ($unexpected.Count -ne 0) {
            throw "Refusing to replace screenshot directory because it contains unrelated entries: $($unexpected.Name -join ', ')"
        }
    }

    New-Item -ItemType Directory -Path $promotion | Out-Null
    try {
        foreach ($item in $expected) {
            Copy-Item -LiteralPath (Join-Path $CandidateDirectory $item.Name) -Destination (Join-Path $promotion $item.Name)
        }
        Test-ScreenshotSet -CandidateDirectory $promotion
        if ($Faults -contains 'BeforeCanonicalReplacement') { throw 'Injected publish failure before canonical replacement.' }
        if (Test-Path -LiteralPath $target) {
            Move-Item -LiteralPath $target -Destination $backup
            $backupCreated = $true
        }
        if ($Faults -contains 'AfterBackupBeforePromotion') { throw 'Injected publish failure after backup and before promotion.' }
        Move-Item -LiteralPath $promotion -Destination $target
        $targetInstalled = $true
        if ($Faults -contains 'PostPublishValidation') { throw 'Injected post-publish validation failure.' }
        Test-ScreenshotSet -CandidateDirectory $target
        if ($backupCreated) {
            Remove-OwnedDirectory -Path $backup -OwnedParent $parent -RequiredLeafPrefix $backupPrefix
            $backupCreated = $false
        }
    }
    catch {
        $publishError = $_
        $rollbackError = $null
        try {
            if ($backupCreated -and (Test-Path -LiteralPath $backup)) {
                if ($Faults -contains 'RollbackRestore') { throw 'Injected rollback restoration failure.' }
                if (Test-Path -LiteralPath $target) {
                    Move-Item -LiteralPath $target -Destination $failed
                    $targetInstalled = $false
                }
                Move-Item -LiteralPath $backup -Destination $target
                $backupCreated = $false
                Test-ScreenshotSet -CandidateDirectory $target
                if (-not (Test-ScreenshotHashMap -ExpectedHashes $priorHashes -Directory $target)) {
                    throw 'Restored canonical screenshot hashes do not match the pre-publish set.'
                }
                if (Test-Path -LiteralPath $failed) {
                    Remove-OwnedDirectory -Path $failed -OwnedParent $parent -RequiredLeafPrefix $failedPrefix
                }
            }
            elseif (-not $targetExisted -and $targetInstalled -and (Test-Path -LiteralPath $target)) {
                Remove-OwnedDirectory -Path $target -OwnedParent $parent -RequiredLeafPrefix $leaf
                $targetInstalled = $false
            }
        }
        catch { $rollbackError = $_ }
        if ($null -ne $rollbackError) {
            throw "Screenshot publish failed and rollback also failed. Publish [$($publishError.Exception.GetType().Name)]: $($publishError.Exception.Message) Rollback [$($rollbackError.Exception.GetType().Name)]: $($rollbackError.Exception.Message) Recoverable backup retained=$([bool](Test-Path -LiteralPath $backup))."
        }
        throw $publishError
    }
    finally {
        if (Test-Path -LiteralPath $promotion) {
            Remove-OwnedDirectory -Path $promotion -OwnedParent $parent -RequiredLeafPrefix $promotionPrefix
        }
    }
}

function Invoke-CapturePipeline {
    param(
        [string]$CandidateDirectory,
        [string]$TargetDirectory,
        [string]$Fault = ''
    )
    $capture = $null
    try {
        $capture = Invoke-CaptureHost -CandidateDirectory $CandidateDirectory -Fault $Fault
        if ($capture.ExitCode -ne 0) { throw "UI capture failed with exit code $($capture.ExitCode)." }
        Assert-SemanticResults -Lines $capture.Lines
        Test-ScreenshotSet -CandidateDirectory $CandidateDirectory
        Publish-ScreenshotSet -CandidateDirectory $CandidateDirectory -TargetDirectory $TargetDirectory
        return [pscustomobject]@{ Succeeded = $true; Capture = $capture; Error = $null }
    }
    catch {
        return [pscustomobject]@{ Succeeded = $false; Capture = $capture; Error = $_.Exception }
    }
}

function Assert-InfrastructureCondition {
    param([string]$Name, [bool]$Condition)
    if (-not $Condition) { throw "Capture infrastructure assertion failed: $Name" }
    $script:CaptureInfrastructurePassCount++
    Write-Output "PASS capture infrastructure $Name"
}

function Invoke-CaptureCandidate {
    param([string]$CandidateDirectory)
    $capture = $null
    try {
        $capture = Invoke-CaptureHost -CandidateDirectory $CandidateDirectory
        if ($capture.ExitCode -ne 0) { throw "UI capture failed with exit code $($capture.ExitCode)." }
        Assert-SemanticResults -Lines $capture.Lines
        Test-ScreenshotSet -CandidateDirectory $CandidateDirectory
        return [pscustomobject]@{ Succeeded = $true; Capture = $capture; Error = $null }
    }
    catch { return [pscustomobject]@{ Succeeded = $false; Capture = $capture; Error = $_.Exception } }
}

function Test-PngFixtureRejected {
    param([string]$Path, [string]$MessagePattern)
    try {
        [void]@(Get-PngChunkTypes -Path $Path)
        return $false
    }
    catch {
        return $_.Exception.Message -match $MessagePattern
    }
}

function Invoke-InfrastructureTests {
    $script:CaptureInfrastructurePassCount = 0
    $ownedRoot = New-OwnedTemporaryDirectory -Prefix 'NetStuck-ui-capture-infra-'
    $primaryError = $null
    $cleanupError = $null
    try {
        $good = Join-Path $ownedRoot 'good'
        New-Item -ItemType Directory -Path $good | Out-Null
        $goodRun = Invoke-CaptureHost -CandidateDirectory $good
        if ($goodRun.ExitCode -ne 0) { throw "Good capture fixture failed with exit code $($goodRun.ExitCode): $($goodRun.Lines -join ' | ')" }
        Assert-SemanticResults -Lines $goodRun.Lines
        Test-ScreenshotSet -CandidateDirectory $good
        Assert-InfrastructureCondition 'complete semantic set validates' $true
        $canonicalPng = Join-Path $good $expected[0].Name
        $canonicalTypes = @(Get-PngChunkTypes -Path $canonicalPng)
        $canonicalFirstIdat = [Array]::IndexOf($canonicalTypes, 'IDAT')
        $canonicalOrdering = $canonicalFirstIdat -gt 0
        foreach ($metadataType in @('sRGB', 'gAMA', 'pHYs')) {
            $metadataIndex = [Array]::IndexOf($canonicalTypes, $metadataType)
            $canonicalOrdering = $canonicalOrdering -and $metadataIndex -ge 0 -and $metadataIndex -lt $canonicalFirstIdat
        }
        Assert-InfrastructureCondition 'canonical PNG metadata precedes contiguous image data' $canonicalOrdering

        $semantic = Join-Path $ownedRoot 'semantic-failure'
        New-Item -ItemType Directory -Path $semantic | Out-Null
        $semanticRun = Invoke-CaptureHost -CandidateDirectory $semantic -Fault 'calculator-idle'
        Assert-InfrastructureCondition 'semantic failure returns nonzero' ($semanticRun.ExitCode -ne 0)
        Assert-InfrastructureCondition 'failed current run emits no scenario PASS' (-not [bool](@($semanticRun.Lines | Where-Object { $_ -match '^SEMANTIC PASS ' }).Count))
        $semanticStderrCaptured = @($semanticRun.StdErrLines | Where-Object { $_ -match '^SEMANTIC FAIL ' }).Count -gt 0
        $semanticStdoutClean = @($semanticRun.StdOutLines | Where-Object { $_ -match '^SEMANTIC FAIL ' }).Count -eq 0
        Assert-InfrastructureCondition 'expected semantic stderr is captured separately' ($semanticStderrCaptured -and $semanticStdoutClean)
        Assert-InfrastructureCondition 'wrong expected UI state is identified' ([bool](@($semanticRun.Lines | Where-Object { $_ -match '^SEMANTIC FAIL pilot-calculators-1100x700 .*instead of Success' }).Count))

        $cleanup = Join-Path $ownedRoot 'cleanup-failure'
        New-Item -ItemType Directory -Path $cleanup | Out-Null
        $cleanupRun = Invoke-CaptureHost -CandidateDirectory $cleanup -Fault 'cleanup'
        Assert-InfrastructureCondition 'cleanup failure cannot yield success' ($cleanupRun.ExitCode -ne 0 -and [bool](@($cleanupRun.Lines | Where-Object { $_ -match '^CLEANUP FAIL ' }).Count))

        $staleTarget = Join-Path $ownedRoot 'stale-target'
        Publish-ScreenshotSet -CandidateDirectory $good -TargetDirectory $staleTarget
        $priorHashes = Get-ScreenshotHashMap -Directory $staleTarget
        $staleCandidate = Join-Path $ownedRoot 'stale-current-run'
        New-Item -ItemType Directory -Path $staleCandidate | Out-Null
        $combined = Invoke-CapturePipeline -CandidateDirectory $staleCandidate -TargetDirectory $staleTarget -Fault 'calculator-idle'
        Assert-InfrastructureCondition 'exact stale-promotion current run fails nonzero' (-not $combined.Succeeded -and $null -ne $combined.Capture -and $combined.Capture.ExitCode -ne 0)
        Assert-InfrastructureCondition 'exact stale-promotion emits no scenario PASS' (-not [bool](@($combined.Capture.Lines | Where-Object { $_ -match '^SEMANTIC PASS ' }).Count))
        $currentCandidateRejected = $false
        try { Test-ScreenshotSet -CandidateDirectory $staleCandidate } catch { $currentCandidateRejected = $true }
        Assert-InfrastructureCondition 'prior PNGs cannot satisfy current candidate validation' $currentCandidateRejected
        Assert-InfrastructureCondition 'prior authoritative PNG hashes remain unchanged' (Test-ScreenshotHashMap -ExpectedHashes $priorHashes -Directory $staleTarget)
        Test-ScreenshotSet -CandidateDirectory $staleTarget
        $promotionResidue = @(Get-ChildItem -LiteralPath $ownedRoot -Force | Where-Object {
            $_.Name -like '.stale-target.promotion-*' -or $_.Name -like '.stale-target.backup-*'
        })
        Assert-InfrastructureCondition 'failed publish path leaves no mixed set or promotion residue' ($promotionResidue.Count -eq 0)

        $transactionCandidate = Join-Path $ownedRoot 'transaction-candidate'
        Copy-ScreenshotSet -SourceDirectory $good -DestinationDirectory $transactionCandidate
        Set-PngPhysicalResolutionFixtureByte -Path (Join-Path $transactionCandidate $expected[0].Name)
        Test-ScreenshotSet -CandidateDirectory $transactionCandidate

        $postFailureTarget = Join-Path $ownedRoot 'post-failure-target'
        Publish-ScreenshotSet -CandidateDirectory $good -TargetDirectory $postFailureTarget
        $postFailurePreHashes = Get-ScreenshotHashMap -Directory $postFailureTarget
        $postFailureRejected = $false
        try { Publish-ScreenshotSet -CandidateDirectory $transactionCandidate -TargetDirectory $postFailureTarget -Faults @('PostPublishValidation') }
        catch { $postFailureRejected = $_.Exception.Message -match 'post-publish validation failure' }
        Assert-InfrastructureCondition 'post-publish validation failure returns nonzero' $postFailureRejected
        Assert-InfrastructureCondition 'post-publish failure restores exact canonical pre-hashes' (Test-ScreenshotHashMap -ExpectedHashes $postFailurePreHashes -Directory $postFailureTarget)
        $postFailureResidue = @(Get-ChildItem -LiteralPath $ownedRoot -Force | Where-Object { $_.Name -like '.post-failure-target.*' })
        Assert-InfrastructureCondition 'post-publish failure removes backup and promotion staging' ($postFailureResidue.Count -eq 0)

        $preFailureTarget = Join-Path $ownedRoot 'pre-failure-target'
        Publish-ScreenshotSet -CandidateDirectory $good -TargetDirectory $preFailureTarget
        $preFailureHashes = Get-ScreenshotHashMap -Directory $preFailureTarget
        $preFailureRejected = $false
        try { Publish-ScreenshotSet -CandidateDirectory $transactionCandidate -TargetDirectory $preFailureTarget -Faults @('BeforeCanonicalReplacement') }
        catch { $preFailureRejected = $_.Exception.Message -match 'before canonical replacement' }
        Assert-InfrastructureCondition 'pre-publish failure returns nonzero' $preFailureRejected
        Assert-InfrastructureCondition 'pre-publish failure leaves canonical hashes unchanged' (Test-ScreenshotHashMap -ExpectedHashes $preFailureHashes -Directory $preFailureTarget)
        Assert-InfrastructureCondition 'pre-publish failure leaves no backup or staging residue' (@(Get-ChildItem -LiteralPath $ownedRoot -Force | Where-Object { $_.Name -like '.pre-failure-target.*' }).Count -eq 0)

        $partialFailureTarget = Join-Path $ownedRoot 'partial-failure-target'
        Publish-ScreenshotSet -CandidateDirectory $good -TargetDirectory $partialFailureTarget
        $partialFailureHashes = Get-ScreenshotHashMap -Directory $partialFailureTarget
        $partialFailureRejected = $false
        try { Publish-ScreenshotSet -CandidateDirectory $transactionCandidate -TargetDirectory $partialFailureTarget -Faults @('AfterBackupBeforePromotion') }
        catch { $partialFailureRejected = $_.Exception.Message -match 'after backup and before promotion' }
        Assert-InfrastructureCondition 'failure between backup and whole-set promotion returns nonzero' $partialFailureRejected
        Assert-InfrastructureCondition 'interrupted whole-set promotion restores complete canonical set' (Test-ScreenshotHashMap -ExpectedHashes $partialFailureHashes -Directory $partialFailureTarget)
        Assert-InfrastructureCondition 'interrupted whole-set promotion leaves no transaction residue' (@(Get-ChildItem -LiteralPath $ownedRoot -Force | Where-Object { $_.Name -like '.partial-failure-target.*' }).Count -eq 0)

        $rollbackFailureTarget = Join-Path $ownedRoot 'rollback-failure-target'
        Publish-ScreenshotSet -CandidateDirectory $good -TargetDirectory $rollbackFailureTarget
        $rollbackFailureHashes = Get-ScreenshotHashMap -Directory $rollbackFailureTarget
        $rollbackFailureMessage = ''
        try { Publish-ScreenshotSet -CandidateDirectory $transactionCandidate -TargetDirectory $rollbackFailureTarget -Faults @('PostPublishValidation','RollbackRestore') }
        catch { $rollbackFailureMessage = $_.Exception.Message }
        $rollbackBackups = @(Get-ChildItem -LiteralPath $ownedRoot -Force | Where-Object { $_.Name -like '.rollback-failure-target.backup-*' })
        $rollbackFailureExplicit = $rollbackFailureMessage -match 'publish failed and rollback also failed' -and
            $rollbackFailureMessage -match 'post-publish validation failure' -and $rollbackFailureMessage -match 'rollback restoration failure' -and
            $rollbackBackups.Count -eq 1
        Assert-InfrastructureCondition 'forced rollback failure retains both failure diagnostics and no false PASS' $rollbackFailureExplicit
        Remove-OwnedDirectory -Path $rollbackFailureTarget -OwnedParent $ownedRoot -RequiredLeafPrefix 'rollback-failure-target'
        Move-Item -LiteralPath $rollbackBackups[0].FullName -Destination $rollbackFailureTarget
        Assert-InfrastructureCondition 'forced rollback fixture preserves recoverable original evidence' (Test-ScreenshotHashMap -ExpectedHashes $rollbackFailureHashes -Directory $rollbackFailureTarget)
        Assert-InfrastructureCondition 'transaction success path installed a complete validated set' ((Test-ScreenshotHashMap -ExpectedHashes $postFailurePreHashes -Directory $postFailureTarget) -and (Test-Path -LiteralPath $postFailureTarget -PathType Container))

        $missing = Join-Path $ownedRoot 'missing'
        New-Item -ItemType Directory -Path $missing | Out-Null
        foreach ($item in $expected | Select-Object -Skip 1) { Copy-Item -LiteralPath (Join-Path $good $item.Name) -Destination (Join-Path $missing $item.Name) }
        $missingRejected = $false
        try { Test-ScreenshotSet -CandidateDirectory $missing } catch { $missingRejected = $_.Exception.Message -match 'inventory|Missing' }
        Assert-InfrastructureCondition 'missing expected screenshot is rejected' $missingRejected

        $extra = Join-Path $ownedRoot 'extra'
        Copy-ScreenshotSet -SourceDirectory $good -DestinationDirectory $extra
        Copy-Item -LiteralPath (Join-Path $good $expected[0].Name) -Destination (Join-Path $extra 'unexpected-extra.png')
        $extraRejected = $false
        try { Test-ScreenshotSet -CandidateDirectory $extra } catch { $extraRejected = $_.Exception.Message -match 'inventory|Unexpected' }
        Assert-InfrastructureCondition 'unexpected screenshot is rejected' $extraRejected

        $badCrc = Join-Path $ownedRoot 'bad-crc'
        Copy-ScreenshotSet -SourceDirectory $good -DestinationDirectory $badCrc
        $badCrcPath = Join-Path $badCrc $expected[0].Name
        [byte[]]$badCrcBytes = [System.IO.File]::ReadAllBytes($badCrcPath)
        $badCrcBytes[$badCrcBytes.Length - 1] = [byte]($badCrcBytes[$badCrcBytes.Length - 1] -bxor 1)
        [System.IO.File]::WriteAllBytes($badCrcPath, $badCrcBytes)
        $badCrcRejected = $false
        try { Test-ScreenshotSet -CandidateDirectory $badCrc } catch { $badCrcRejected = $_.Exception.Message -match 'CRC mismatch' }
        Assert-InfrastructureCondition 'bad PNG CRC is rejected' $badCrcRejected

        $trailing = Join-Path $ownedRoot 'trailing-bytes'
        Copy-ScreenshotSet -SourceDirectory $good -DestinationDirectory $trailing
        $trailingPath = Join-Path $trailing $expected[0].Name
        [byte[]]$trailingBytes = [System.IO.File]::ReadAllBytes($trailingPath)
        [System.IO.File]::WriteAllBytes($trailingPath, [byte[]]($trailingBytes + [byte[]](1, 2, 3)))
        $trailingRejected = $false
        try { Test-ScreenshotSet -CandidateDirectory $trailing } catch { $trailingRejected = $_.Exception.Message -match 'after IEND' }
        Assert-InfrastructureCondition 'PNG trailing payload is rejected' $trailingRejected

        $unexpectedChunk = Join-Path $ownedRoot 'unexpected-chunk'
        Copy-ScreenshotSet -SourceDirectory $good -DestinationDirectory $unexpectedChunk
        Set-UnexpectedPngChunkType -Path (Join-Path $unexpectedChunk $expected[0].Name)
        $unexpectedChunkRejected = $false
        try { Test-ScreenshotSet -CandidateDirectory $unexpectedChunk } catch { $unexpectedChunkRejected = $_.Exception.Message -match 'Unexpected PNG chunk' }
        Assert-InfrastructureCondition 'unexpected PNG chunk is rejected' $unexpectedChunkRejected

        foreach ($metadataType in @('pHYs', 'gAMA', 'sRGB')) {
            $fixture = Join-Path $ownedRoot ("after-idat-" + $metadataType + '.png')
            Copy-Item -LiteralPath $canonicalPng -Destination $fixture
            Move-PngChunkAfterIdat -Path $fixture -ChunkType $metadataType
            Assert-InfrastructureCondition ("$metadataType after IDAT is rejected") (Test-PngFixtureRejected -Path $fixture -MessagePattern "must appear before the first IDAT")
        }

        $duplicateIhdr = Join-Path $ownedRoot 'duplicate-ihdr.png'
        Copy-Item -LiteralPath $canonicalPng -Destination $duplicateIhdr
        Duplicate-PngChunk -Path $duplicateIhdr -ChunkType 'IHDR'
        Assert-InfrastructureCondition 'duplicate IHDR is rejected' (Test-PngFixtureRejected -Path $duplicateIhdr -MessagePattern 'exactly one IHDR')

        $earlyIend = Join-Path $ownedRoot 'early-iend.png'
        Copy-Item -LiteralPath $canonicalPng -Destination $earlyIend
        Move-PngIendBeforeIdat -Path $earlyIend
        Assert-InfrastructureCondition 'IEND before final image data is rejected' (Test-PngFixtureRejected -Path $earlyIend -MessagePattern 'before image data|IDAT is missing')

        $afterIend = Join-Path $ownedRoot 'chunk-after-iend.png'
        Copy-Item -LiteralPath $canonicalPng -Destination $afterIend
        Append-PngChunkAfterIend -Path $afterIend
        Assert-InfrastructureCondition 'valid chunk after IEND is rejected' (Test-PngFixtureRejected -Path $afterIend -MessagePattern 'after IEND')

        $truncatedChunk = Join-Path $ownedRoot 'truncated-chunk.png'
        [byte[]]$truncatedBytes = [System.IO.File]::ReadAllBytes($canonicalPng)
        [byte[]]$shortBytes = New-Object byte[] ($truncatedBytes.Length - 3)
        [Array]::Copy($truncatedBytes, $shortBytes, $shortBytes.Length)
        [System.IO.File]::WriteAllBytes($truncatedChunk, $shortBytes)
        Assert-InfrastructureCondition 'truncated PNG chunk is rejected' (Test-PngFixtureRejected -Path $truncatedChunk -MessagePattern 'truncated|invalid length')

        $nonContiguousIdat = Join-Path $ownedRoot 'non-contiguous-idat.png'
        Copy-Item -LiteralPath $canonicalPng -Destination $nonContiguousIdat
        Split-PngIdatWithMetadata -Path $nonContiguousIdat
        Assert-InfrastructureCondition 'non-contiguous IDAT is rejected' (Test-PngFixtureRejected -Path $nonContiguousIdat -MessagePattern 'IDAT chunks must be contiguous')

        $unexpectedCritical = Join-Path $ownedRoot 'unexpected-critical.png'
        Copy-Item -LiteralPath $canonicalPng -Destination $unexpectedCritical
        Set-PngChunkType -Path $unexpectedCritical -FromType 'pHYs' -ToType 'PLTE'
        Assert-InfrastructureCondition 'unexpected critical PNG chunk is rejected' (Test-PngFixtureRejected -Path $unexpectedCritical -MessagePattern 'Unexpected critical PNG chunk')

        $duplicateSingleton = Join-Path $ownedRoot 'duplicate-singleton.png'
        Copy-Item -LiteralPath $canonicalPng -Destination $duplicateSingleton
        Duplicate-PngChunk -Path $duplicateSingleton -ChunkType 'sRGB'
        Assert-InfrastructureCondition 'duplicate singleton metadata chunk is rejected' (Test-PngFixtureRejected -Path $duplicateSingleton -MessagePattern "singleton chunk 'sRGB' is duplicated")
    }
    catch { $primaryError = $_ }

    try {
        Remove-OwnedDirectory -Path $ownedRoot -OwnedParent ([System.IO.Path]::GetTempPath()) -RequiredLeafPrefix 'NetStuck-ui-capture-infra-'
    }
    catch { $cleanupError = $_ }

    if ($primaryError -and $cleanupError) {
        throw "Capture infrastructure failed and cleanup also failed. Primary: $($primaryError.Exception.GetType().Name); cleanup: $($cleanupError.Exception.GetType().Name)."
    }
    if ($primaryError) { throw $primaryError }
    if ($cleanupError) { throw $cleanupError }
    Write-Output "UI capture infrastructure tests passed: $script:CaptureInfrastructurePassCount/$script:CaptureInfrastructurePassCount"
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework compiler was not found: $compiler"
}
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

$sources = @(
    'NetOpsCore.cs',
    'NetStuck.UiFoundation.cs',
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

$uiLibrary = Join-Path $artifactRoot 'NetStuck.UI.dll'
$captureExecutable = Join-Path $artifactRoot 'UiFoundationSnapshot.exe'
$libraryArguments = @('/nologo', '/target:library', '/optimize+', "/out:$uiLibrary") + $references + $sources
$libraryCompile = Invoke-NativeProcess -FilePath $compiler -ArgumentList $libraryArguments
Assert-NativeProcessSucceeded -Result $libraryCompile -Stage 'UI capture library compilation'
$captureArguments = @('/nologo', '/target:exe', '/optimize+', "/out:$captureExecutable") + $references + "/reference:$uiLibrary" + (Join-Path $testRoot 'UiFoundationSnapshot.cs')
$hostCompile = Invoke-NativeProcess -FilePath $compiler -ArgumentList $captureArguments
Assert-NativeProcessSucceeded -Result $hostCompile -Stage 'UI capture host compilation'

if ($RunInfrastructureTests) {
    Invoke-InfrastructureTests
    return
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot 'docs\ui-foundations\screenshots'
}
$OutputDirectory = Assert-OutputTarget -Path $OutputDirectory
$stageRoot = New-OwnedTemporaryDirectory -Prefix 'NetStuck-ui-capture-stage-'
$primaryError = $null
$cleanupError = $null
try {
    $baselineHashes = $null
    $publishCandidate = $null
    for ($run = 1; $run -le $DeterminismRuns; $run++) {
        $candidate = Join-Path $stageRoot ("candidate-" + $run.ToString('D2'))
        New-Item -ItemType Directory -Path $candidate | Out-Null
        $candidateResult = Invoke-CaptureCandidate -CandidateDirectory $candidate
        if ($null -ne $candidateResult.Capture) { $candidateResult.Capture.Lines | ForEach-Object { Write-Output $_ } }
        if (-not $candidateResult.Succeeded) { throw $candidateResult.Error }
        if ($run -eq 1) {
            $publishCandidate = $candidate
            $baselineHashes = Get-ScreenshotHashMap -Directory $candidate
        }
        elseif (-not (Test-ScreenshotHashMap -ExpectedHashes $baselineHashes -Directory $candidate)) {
            $differences = @($expected | Where-Object {
                (Get-FileHash -LiteralPath (Join-Path $candidate $_.Name) -Algorithm SHA256).Hash -ne $baselineHashes[$_.Name]
            } | ForEach-Object { $_.Name })
            throw "Screenshot determinism failed on run $run/$DeterminismRuns for: $($differences -join ', ')"
        }
    }
    Publish-ScreenshotSet -CandidateDirectory $publishCandidate -TargetDirectory $OutputDirectory
    foreach ($item in $expected) {
        Write-Output "DETERMINISM PASS $($item.Scenario) runs=$DeterminismRuns sha256=$($baselineHashes[$item.Name].ToLowerInvariant())"
    }
}
catch { $primaryError = $_ }

try {
    Remove-OwnedDirectory -Path $stageRoot -OwnedParent ([System.IO.Path]::GetTempPath()) -RequiredLeafPrefix 'NetStuck-ui-capture-stage-'
}
catch { $cleanupError = $_ }

if ($primaryError -and $cleanupError) {
    throw "UI capture failed and staging cleanup also failed. Primary: $($primaryError.Exception.GetType().Name); cleanup: $($cleanupError.Exception.GetType().Name)."
}
if ($primaryError) { throw $primaryError }
if ($cleanupError) { throw $cleanupError }

Write-Output "UI foundation screenshots verified: $($expected.Count)/$($expected.Count); semantic assertions PASS; PNG/privacy gate PASS; determinism $DeterminismRuns/$DeterminismRuns"
