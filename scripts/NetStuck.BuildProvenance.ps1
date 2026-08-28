$script:NetStuckProductionSourcePaths = @(
    'src/NetStuck/NetOpsCore.cs',
    'src/NetStuck/NetStuck.UiFoundation.cs',
    'src/NetStuck/NetStuck.cs',
    'src/NetStuck/NetStuck.Features.cs',
    'src/NetStuck/NetStuck.Release1.cs',
    'src/NetStuck/NetStuck.V103.cs'
)

$script:NetStuckFrameworkReferenceNames = @(
    'mscorlib.dll',
    'System.dll',
    'System.Core.dll',
    'System.Data.dll',
    'System.Data.DataSetExtensions.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll',
    'System.Xml.dll'
)

function ConvertTo-NetStuckRelativePath {
    param([string]$Path)
    return ($Path -replace '\\', '/').TrimStart('/')
}

function Get-NetStuckProductionSourcePaths {
    return @($script:NetStuckProductionSourcePaths)
}

function Get-NetStuckFrameworkReferenceNames {
    return @($script:NetStuckFrameworkReferenceNames)
}

function Get-NetStuckRepositoryInputSpecifications {
    $specifications = New-Object System.Collections.Generic.List[object]
    foreach ($path in @(
        'scripts/Build-NetStuck.ps1',
        'scripts/NetStuck.BuildProvenance.ps1',
        'scripts/Package-NetStuck.ps1'
    )) {
        $specifications.Add([pscustomobject]@{ Role = 'build-recipe'; RelativePath = $path })
    }
    foreach ($path in $script:NetStuckProductionSourcePaths) {
        $specifications.Add([pscustomobject]@{ Role = 'production-source'; RelativePath = $path })
    }
    $specifications.Add([pscustomobject]@{ Role = 'win32-icon'; RelativePath = 'src/NetStuck/assets/netstuck-bright.ico' })
    return $specifications.ToArray()
}

function Get-NetStuckSha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-NetStuckUtf8Sha256 {
    param([string]$Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $encoding = New-Object System.Text.UTF8Encoding($false)
        return ([BitConverter]::ToString($sha.ComputeHash($encoding.GetBytes($Text)))).Replace('-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Get-NetStuckFileInventory {
    param(
        [string]$RootPath,
        [object[]]$Specifications,
        [switch]$SkipTracking
    )
    $root = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\', '/')
    $trackedPaths = @()
    if (-not $SkipTracking) {
        $trackedPaths = @(& git -C $root ls-files 2>$null | ForEach-Object { ConvertTo-NetStuckRelativePath $_ })
        if ($LASTEXITCODE -ne 0) { throw "Unable to enumerate tracked repository inputs under $root." }
    }
    $inventory = New-Object System.Collections.Generic.List[object]
    foreach ($specification in $Specifications) {
        $relative = ConvertTo-NetStuckRelativePath $specification.RelativePath
        if ([System.IO.Path]::IsPathRooted($relative) -or $relative -match '(^|/)\.\.(/|$)' -or $relative -match "[`t`r`n]") {
            throw "Unsafe provenance relative path: $relative"
        }
        $fullPath = Join-Path $root ($relative.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "Required provenance input is missing: $relative" }
        $item = Get-Item -LiteralPath $fullPath
        $inventory.Add([pscustomobject]@{
            Role = [string]$specification.Role
            RelativePath = $relative
            FullPath = $item.FullName
            Size = [int64]$item.Length
            Sha256 = Get-NetStuckSha256 -Path $item.FullName
            Tracked = $(if ($SkipTracking) { $null } else { [bool]($trackedPaths -contains $relative) })
        })
    }
    return $inventory.ToArray()
}

function Get-NetStuckCanonicalInventoryManifest {
    param([object[]]$Inventory)
    [string[]]$lines = @($Inventory | ForEach-Object {
        $role = [string]$_.Role
        $relative = ConvertTo-NetStuckRelativePath ([string]$_.RelativePath)
        $hash = ([string]$_.Sha256).ToLowerInvariant()
        if ($role -match "[`t`r`n]" -or $relative -match "[`t`r`n]" -or $hash -notmatch '^[0-9a-f]{64}$') {
            throw 'Invalid canonical provenance inventory entry.'
        }
        $role + "`t" + $relative + "`t" + ([int64]$_.Size).ToString([Globalization.CultureInfo]::InvariantCulture) + "`t" + $hash
    })
    [Array]::Sort($lines, [StringComparer]::Ordinal)
    return ($lines -join "`n") + "`n"
}

function Get-NetStuckInventoryFingerprint {
    param([object[]]$Inventory)
    return Get-NetStuckUtf8Sha256 -Text (Get-NetStuckCanonicalInventoryManifest -Inventory $Inventory)
}

function Assert-NetStuckProductionSourceSpecifications {
    param([string[]]$RelativePaths)
    [string[]]$expected = @($script:NetStuckProductionSourcePaths | ForEach-Object { ConvertTo-NetStuckRelativePath $_ })
    [string[]]$actual = @($RelativePaths | ForEach-Object { ConvertTo-NetStuckRelativePath $_ })
    [Array]::Sort($expected, [StringComparer]::Ordinal)
    [Array]::Sort($actual, [StringComparer]::Ordinal)
    if ($actual.Count -ne $expected.Count) {
        throw "Production source inventory mismatch: expected $($expected.Count), actual $($actual.Count)."
    }
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if (-not [String]::Equals($expected[$index], $actual[$index], [StringComparison]::Ordinal)) {
            throw "Production source inventory mismatch at index ${index}: expected '$($expected[$index])', actual '$($actual[$index])'."
        }
    }
    if (@($actual | Where-Object { $_ -match '(^|/)tests?/' }).Count -ne 0) {
        throw 'Test source entered the production source inventory.'
    }
}

function Assert-NetStuckProductionSourceDirectory {
    param([string]$RepositoryRoot)
    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $sourceRoot = Join-Path $root 'src\NetStuck'
    $actual = @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse -Filter '*.cs' | ForEach-Object {
        ConvertTo-NetStuckRelativePath $_.FullName.Substring($root.Length + 1)
    })
    Assert-NetStuckProductionSourceSpecifications -RelativePaths $actual
}

function Resolve-NetStuckCompilerPath {
    param([string]$CompilerPath = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
    if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) { throw "The .NET Framework compiler was not found: $CompilerPath" }
    return (Get-Item -LiteralPath $CompilerPath).FullName
}

function Get-NetStuckFrameworkReferenceInventory {
    param([string]$CompilerPath)
    $compiler = Resolve-NetStuckCompilerPath -CompilerPath $CompilerPath
    $frameworkRoot = Split-Path -Parent $compiler
    $inventory = New-Object System.Collections.Generic.List[object]
    foreach ($name in $script:NetStuckFrameworkReferenceNames) {
        $path = Join-Path $frameworkRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required framework reference is missing: $name" }
        $item = Get-Item -LiteralPath $path
        $inventory.Add([pscustomobject]@{
            Role = 'framework-reference'
            RelativePath = 'framework/' + $name
            FullPath = $item.FullName
            Size = [int64]$item.Length
            Sha256 = Get-NetStuckSha256 -Path $item.FullName
            Version = [Diagnostics.FileVersionInfo]::GetVersionInfo($item.FullName).FileVersion
        })
    }
    return $inventory.ToArray()
}

function Get-NetStuckToolchainInventory {
    param([string]$CompilerPath)
    $compiler = Resolve-NetStuckCompilerPath -CompilerPath $CompilerPath
    $compilerRoot = Split-Path -Parent $compiler
    $runtimeRoot = $compilerRoot
    $specifications = @(
        [pscustomobject]@{ Role = 'compiler'; RelativePath = 'toolchain/compiler/csc.exe'; FullPath = $compiler },
        [pscustomobject]@{ Role = 'compiler-config'; RelativePath = 'toolchain/compiler/csc.exe.config'; FullPath = (Join-Path $compilerRoot 'csc.exe.config') },
        [pscustomobject]@{ Role = 'resource-tool'; RelativePath = 'toolchain/compiler/cvtres.exe'; FullPath = (Join-Path $compilerRoot 'cvtres.exe') },
        [pscustomobject]@{ Role = 'runtime'; RelativePath = 'toolchain/runtime/clr.dll'; FullPath = (Join-Path $runtimeRoot 'clr.dll') }
    )
    $inventory = New-Object System.Collections.Generic.List[object]
    foreach ($specification in $specifications) {
        if (-not (Test-Path -LiteralPath $specification.FullPath -PathType Leaf)) { throw "Required toolchain input is missing: $($specification.RelativePath)" }
        $item = Get-Item -LiteralPath $specification.FullPath
        $inventory.Add([pscustomobject]@{
            Role = $specification.Role
            RelativePath = $specification.RelativePath
            FullPath = $item.FullName
            Size = [int64]$item.Length
            Sha256 = Get-NetStuckSha256 -Path $item.FullName
            Version = [Diagnostics.FileVersionInfo]::GetVersionInfo($item.FullName).FileVersion
        })
    }
    return $inventory.ToArray()
}

function Write-NetStuckCanonicalInt32 {
    param(
        [System.IO.Stream]$Stream,
        [int]$Value
    )
    [byte[]]$bytes = [BitConverter]::GetBytes($Value)
    if (-not [BitConverter]::IsLittleEndian) { [Array]::Reverse($bytes) }
    $Stream.Write($bytes, 0, $bytes.Length)
}

function Get-NetStuckCanonicalArgumentBytes {
    param([AllowEmptyCollection()][string[]]$Arguments = @())
    if ($null -eq $Arguments) { $Arguments = @() }
    $encoding = New-Object System.Text.UTF8Encoding($false, $true)
    [byte[]]$header = [System.Text.Encoding]::ASCII.GetBytes("NetStuck.csc.argv.v2`0")
    $stream = New-Object System.IO.MemoryStream
    try {
        $stream.Write($header, 0, $header.Length)
        Write-NetStuckCanonicalInt32 -Stream $stream -Value $Arguments.Count
        for ($index = 0; $index -lt $Arguments.Count; $index++) {
            if ($null -eq $Arguments[$index]) { throw "Compiler argument at index $index is null." }
            if ($Arguments[$index].IndexOf([char]0) -ge 0) { throw "Compiler argument at index $index contains NUL." }
            [byte[]]$argumentBytes = $encoding.GetBytes([string]$Arguments[$index])
            Write-NetStuckCanonicalInt32 -Stream $stream -Value $index
            Write-NetStuckCanonicalInt32 -Stream $stream -Value $argumentBytes.Length
            $stream.Write($argumentBytes, 0, $argumentBytes.Length)
        }
        return $stream.ToArray()
    }
    finally { $stream.Dispose() }
}

function Get-NetStuckArgumentFingerprint {
    param([AllowEmptyCollection()][string[]]$Arguments = @())
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        [byte[]]$canonical = Get-NetStuckCanonicalArgumentBytes -Arguments $Arguments
        return ([BitConverter]::ToString($sha.ComputeHash($canonical))).Replace('-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Format-NetStuckArgumentVector {
    param(
        [string[]]$Arguments,
        [ValidateSet('Minimal', 'AlwaysQuote')]
        [string]$Style = 'Minimal'
    )
    $rendered = foreach ($argument in @($Arguments)) {
        $escaped = ([string]$argument).Replace('\', '\\').Replace('"', '\"')
        if ($Style -eq 'AlwaysQuote' -or $argument -match '[\s"]') { '"' + $escaped + '"' } else { $escaped }
    }
    return ($rendered -join ' ')
}

function New-NetStuckCompilerArgumentSpecification {
    param(
        [string]$Role,
        [string]$Actual,
        [string]$Normalized
    )
    if ($null -eq $Actual -or $null -eq $Normalized) { throw "Compiler argument specification '$Role' contains a null value." }
    return [pscustomobject]@{ Role = $Role; Actual = $Actual; Normalized = $Normalized }
}

function Get-NetStuckBuildInvocation {
    param(
        [string]$RepositoryRoot,
        [string]$OutputPath,
        [string]$CompilerPath
    )
    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    Assert-NetStuckProductionSourceDirectory -RepositoryRoot $root
    $sources = Get-NetStuckProductionSourcePaths
    Assert-NetStuckProductionSourceSpecifications -RelativePaths $sources
    $references = @(Get-NetStuckFrameworkReferenceInventory -CompilerPath $CompilerPath)
    $iconRelative = 'src/NetStuck/assets/netstuck-bright.ico'
    $specifications = New-Object System.Collections.Generic.List[object]
    foreach ($argument in @('/nologo','/noconfig','/nostdlib+','/target:winexe','/optimize+','/debug-','/checked-','/unsafe-','/platform:anycpu')) {
        $specifications.Add((New-NetStuckCompilerArgumentSpecification -Role 'compiler-option' -Actual $argument -Normalized $argument))
    }
    $specifications.Add((New-NetStuckCompilerArgumentSpecification -Role 'output' -Actual ("/out:" + [System.IO.Path]::GetFullPath($OutputPath)) -Normalized '/out:<OUTPUT>/NetStuck.exe'))
    $specifications.Add((New-NetStuckCompilerArgumentSpecification -Role 'win32-icon' -Actual ("/win32icon:" + (Join-Path $root ($iconRelative.Replace('/', '\')))) -Normalized ("/win32icon:" + $iconRelative)))
    foreach ($reference in $references) {
        $specifications.Add((New-NetStuckCompilerArgumentSpecification -Role 'framework-reference' -Actual ('/reference:' + $reference.FullPath) -Normalized ('/reference:<FRAMEWORK>/' + [System.IO.Path]::GetFileName($reference.FullPath))))
    }
    foreach ($source in $sources) {
        $specifications.Add((New-NetStuckCompilerArgumentSpecification -Role 'production-source' -Actual (Join-Path $root ($source.Replace('/', '\'))) -Normalized $source))
    }
    $actualArguments = @($specifications | ForEach-Object { [string]$_.Actual })
    $normalizedArguments = @($specifications | ForEach-Object { [string]$_.Normalized })
    if ($actualArguments.Count -ne $normalizedArguments.Count) { throw 'Compiler argument specification cardinality drifted.' }
    return [pscustomobject]@{
        ArgumentSpecifications = $specifications.ToArray()
        ActualArguments = $actualArguments
        NormalizedArguments = $normalizedArguments
        Fingerprint = Get-NetStuckArgumentFingerprint -Arguments $normalizedArguments
        ActualFingerprint = Get-NetStuckArgumentFingerprint -Arguments $actualArguments
        DiagnosticCommandLine = Format-NetStuckArgumentVector -Arguments $actualArguments
        FrameworkReferences = $references
    }
}

function Get-NetStuckBuildProvenance {
    param(
        [string]$RepositoryRoot,
        [string]$OutputPath,
        [string]$CompilerPath
    )
    $compiler = Resolve-NetStuckCompilerPath -CompilerPath $CompilerPath
    $sourceInventory = @(Get-NetStuckFileInventory -RootPath $RepositoryRoot -Specifications (Get-NetStuckRepositoryInputSpecifications))
    $toolchainInventory = @(Get-NetStuckToolchainInventory -CompilerPath $compiler)
    $invocation = Get-NetStuckBuildInvocation -RepositoryRoot $RepositoryRoot -OutputPath $OutputPath -CompilerPath $compiler
    $referenceInventory = @($invocation.FrameworkReferences)
    $powerShellExecutable = (Get-Process -Id $PID).Path
    return [pscustomobject]@{
        SchemaVersion = 2
        SourceInputFingerprint = Get-NetStuckInventoryFingerprint -Inventory $sourceInventory
        ToolchainFingerprint = Get-NetStuckInventoryFingerprint -Inventory $toolchainInventory
        BuildInvocationFingerprint = $invocation.Fingerprint
        ActualCompilerArgumentFingerprint = $invocation.ActualFingerprint
        ReferenceInputFingerprint = Get-NetStuckInventoryFingerprint -Inventory $referenceInventory
        SourceInputs = $sourceInventory
        ToolchainInputs = $toolchainInventory
        ReferenceInputs = $referenceInventory
        NormalizedCompilerArguments = @($invocation.NormalizedArguments)
        ActualCompilerArguments = @($invocation.ActualArguments)
        CompilerArgumentSpecifications = @($invocation.ArgumentSpecifications)
        CompilerArgumentSerialization = 'NetStuck.csc.argv.v2; UTF-8; count/index/byte-length are little-endian Int32; no display quoting'
        CompilerDiagnosticCommandLine = $invocation.DiagnosticCommandLine
        Compiler = [pscustomobject]@{
            Path = $compiler
            Version = [Diagnostics.FileVersionInfo]::GetVersionInfo($compiler).FileVersion
            Sha256 = Get-NetStuckSha256 -Path $compiler
        }
        Runtime = [pscustomobject]@{
            CompilerFramework = '.NET Framework 4.x'
            CompilerClrVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path (Split-Path -Parent $compiler) 'clr.dll')).FileVersion
            CompilerRuntimeDirectory = Split-Path -Parent $compiler
            PowerShellHostClrVersion = [Environment]::Version.ToString()
        }
        PowerShellHost = [pscustomobject]@{
            Edition = $(if ($PSVersionTable.PSEdition) { $PSVersionTable.PSEdition } else { 'Desktop' })
            Version = $PSVersionTable.PSVersion.ToString()
            Executable = $powerShellExecutable
        }
        ImplicitInputs = [pscustomobject]@{
            CscResponseFileDisabled = $true
            DefaultStandardLibraryDisabled = $true
            ExplicitFrameworkReferenceCount = $referenceInventory.Count
        }
    }
}

function Get-NetStuckBinarySafeGitDiffFingerprint {
    param([string]$RepositoryRoot, [switch]$Cached)
    $temporary = [System.IO.Path]::GetTempFileName()
    $operationError = $null
    $cleanupError = $null
    $fingerprint = $null
    try {
        $arguments = @('-c', 'core.safecrlf=false', '-C', $RepositoryRoot, 'diff')
        if ($Cached) { $arguments += '--cached' }
        $arguments += @('--binary', '--no-ext-diff', '--no-color', "--output=$temporary")
        & git @arguments
        if ($LASTEXITCODE -ne 0) { throw 'Unable to write the binary Git diff fingerprint input.' }
        $fingerprint = (& git -C $RepositoryRoot hash-object $temporary).Trim()
        if ($LASTEXITCODE -ne 0 -or $fingerprint -notmatch '^[0-9a-f]{40}$') { throw 'Unable to hash the binary Git diff.' }
    }
    catch { $operationError = $_ }
    finally {
        try {
            if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force -ErrorAction Stop }
            if (Test-Path -LiteralPath $temporary) { throw 'Binary Git diff temporary file remains after cleanup.' }
        }
        catch { $cleanupError = $_ }
    }
    if ($null -ne $operationError -and $null -ne $cleanupError) {
        throw "Binary Git diff fingerprinting and cleanup both failed. Operation: $($operationError.Exception.GetType().Name); cleanup: $($cleanupError.Exception.GetType().Name)."
    }
    if ($null -ne $operationError) { throw $operationError }
    if ($null -ne $cleanupError) { throw $cleanupError }
    return $fingerprint
}

function Assert-NetStuckExactRelativeInventory {
    param([string[]]$ActualPaths, [string[]]$ExpectedPaths, [string]$Label)
    [string[]]$actual = @($ActualPaths | ForEach-Object { ConvertTo-NetStuckRelativePath $_ })
    [string[]]$expected = @($ExpectedPaths | ForEach-Object { ConvertTo-NetStuckRelativePath $_ })
    [Array]::Sort($actual, [StringComparer]::Ordinal)
    [Array]::Sort($expected, [StringComparer]::Ordinal)
    if ($actual.Count -ne $expected.Count) { throw "$Label inventory mismatch: expected $($expected.Count), actual $($actual.Count)." }
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if (-not [String]::Equals($actual[$index], $expected[$index], [StringComparison]::Ordinal)) {
            throw "$Label inventory mismatch at index ${index}: expected '$($expected[$index])', actual '$($actual[$index])'."
        }
    }
}
