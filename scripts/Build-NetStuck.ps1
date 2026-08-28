[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$ProvenancePath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'NetStuck.BuildProvenance.ps1')

if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts\build' }
$outputDirectoryFull = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputDirectoryFull -Force | Out-Null
$output = Join-Path $outputDirectoryFull 'NetStuck.exe'
if (-not $ProvenancePath) { $ProvenancePath = Join-Path $outputDirectoryFull 'NetStuck.build-provenance.json' }
$provenancePathFull = [System.IO.Path]::GetFullPath($ProvenancePath)

$compiler = Resolve-NetStuckCompilerPath
$provenance = Get-NetStuckBuildProvenance -RepositoryRoot $repoRoot -OutputPath $output -CompilerPath $compiler
$arguments = @($provenance.ActualCompilerArguments)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "NetStuck compilation failed with exit code $LASTEXITCODE." }

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($output).FileVersion
$record = [ordered]@{
    SchemaVersion = 2
    GeneratedUtc = [DateTime]::UtcNow.ToString('o')
    Output = [ordered]@{
        Path = $output
        Size = [int64](Get-Item -LiteralPath $output).Length
        Sha256 = Get-NetStuckSha256 -Path $output
        FileVersion = $version
    }
    SourceInputFingerprint = $provenance.SourceInputFingerprint
    ToolchainFingerprint = $provenance.ToolchainFingerprint
    BuildInvocationFingerprint = $provenance.BuildInvocationFingerprint
    ActualCompilerArgumentFingerprint = $provenance.ActualCompilerArgumentFingerprint
    ReferenceInputFingerprint = $provenance.ReferenceInputFingerprint
    Compiler = $provenance.Compiler
    Runtime = $provenance.Runtime
    PowerShellHost = $provenance.PowerShellHost
    ImplicitInputs = $provenance.ImplicitInputs
    NormalizedCompilerArguments = @($provenance.NormalizedCompilerArguments)
    ActualCompilerArguments = @($provenance.ActualCompilerArguments)
    CompilerArgumentSpecifications = @($provenance.CompilerArgumentSpecifications)
    CompilerArgumentSerialization = $provenance.CompilerArgumentSerialization
    CompilerDiagnosticCommandLine = $provenance.CompilerDiagnosticCommandLine
    SourceInputs = @($provenance.SourceInputs)
    ToolchainInputs = @($provenance.ToolchainInputs)
    ReferenceInputs = @($provenance.ReferenceInputs)
}
$provenanceParent = Split-Path -Parent $provenancePathFull
if (-not (Test-Path -LiteralPath $provenanceParent)) { New-Item -ItemType Directory -Path $provenanceParent -Force | Out-Null }
[System.IO.File]::WriteAllText($provenancePathFull, ($record | ConvertTo-Json -Depth 8), (New-Object System.Text.UTF8Encoding($false)))

Write-Output "Built: $output"
Write-Output "File version: $version"
Write-Output "Source input fingerprint: $($provenance.SourceInputFingerprint)"
Write-Output "Toolchain fingerprint: $($provenance.ToolchainFingerprint)"
Write-Output "Build invocation fingerprint: $($provenance.BuildInvocationFingerprint)"
Write-Output "Reference input fingerprint: $($provenance.ReferenceInputFingerprint)"
Write-Output "Build provenance: $provenancePathFull"
