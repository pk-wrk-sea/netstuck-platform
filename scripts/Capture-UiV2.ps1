[CmdletBinding()]
param([string]$OutputDirectory = 'artifacts/ui-v2/screenshots', [switch]$NativePopups)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'NetStuck.BuildProvenance.ps1')
$compiler = Resolve-NetStuckCompilerPath
$exe = Join-Path $repoRoot 'artifacts/build/NetStuck.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build NetStuck first.' }
$hostPath = Join-Path $repoRoot 'artifacts/build/UiV2Snapshot.exe'
$references = @(Get-NetStuckFrameworkReferenceInventory -CompilerPath $compiler | ForEach-Object { '/reference:' + $_.FullPath })
& $compiler /nologo /noconfig /nostdlib+ /target:exe /optimize+ "/out:$hostPath" "/win32icon:$(Join-Path $repoRoot 'src/NetStuck/assets/netstuck-bright.ico')" "/win32manifest:$(Join-Path $repoRoot 'src/NetStuck/app.manifest')" "/reference:$exe" $references (Join-Path $repoRoot 'tests/UiV2Snapshot.cs')
if ($LASTEXITCODE -ne 0) { throw 'V2 capture compilation failed.' }
$output = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repoRoot $OutputDirectory }
if ($NativePopups) { & $hostPath $output '--native' } else { & $hostPath $output }
if ($LASTEXITCODE -ne 0) { throw 'V2 capture failed.' }
