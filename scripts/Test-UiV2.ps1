[CmdletBinding()]
param([ValidateSet('Quick','UI','Functional','Closure')][string]$Profile = 'Quick')
$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom; [Console]::OutputEncoding = $utf8NoBom; $OutputEncoding = $utf8NoBom
$repoRoot = Split-Path -Parent $PSScriptRoot
if ($Profile -eq 'Closure') { & (Join-Path $PSScriptRoot 'Test-NetStuck.ps1') -SoakSeconds 10; return }
. (Join-Path $PSScriptRoot 'NetStuck.BuildProvenance.ps1')
$compiler = Resolve-NetStuckCompilerPath
$output = Join-Path $repoRoot 'artifacts/test'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @(Get-NetStuckFrameworkReferenceInventory -CompilerPath $compiler | ForEach-Object { '/reference:' + $_.FullPath })
$sources = @(Get-NetStuckProductionSourcePaths | ForEach-Object { Join-Path $repoRoot ($_.Replace('/', '\')) })
& $compiler /nologo /noconfig /nostdlib+ /target:library /optimize+ "/out:$(Join-Path $output 'NetStuck.UI.dll')" $references $sources
if ($LASTEXITCODE -ne 0) { throw 'V2 test library compilation failed.' }
& $compiler /nologo /noconfig /nostdlib+ /target:exe "/out:$(Join-Path $output 'LegacyVersionStub.exe')" $references (Join-Path $repoRoot 'tests/LegacyVersionStub.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test-only legacy version fixture compilation failed.' }
if ($Profile -eq 'Functional') {
    & $compiler /nologo /noconfig /nostdlib+ /target:exe "/out:$(Join-Path $output 'FakePlink.exe')" $references (Join-Path $repoRoot 'tests/FakePlink.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Collector fixture compilation failed.' }
}
$name = if ($Profile -eq 'Quick') { 'VersionRecoveryTests' } elseif ($Profile -eq 'Functional') { 'FeatureTests' } else { 'UiV2Tests' }
& $compiler /nologo /noconfig /nostdlib+ /target:exe /optimize+ "/out:$(Join-Path $output ($name + '.exe'))" "/win32manifest:$(Join-Path $repoRoot 'src/NetStuck/app.manifest')" "/reference:$(Join-Path $output 'NetStuck.UI.dll')" "/reference:$(Join-Path (Split-Path $compiler -Parent) 'Accessibility.dll')" $references (Join-Path $repoRoot ('tests/' + $name + '.cs'))
if ($LASTEXITCODE -ne 0) { throw 'V2 test harness compilation failed.' }
Push-Location -LiteralPath $output
try { & (Join-Path $output ($name + '.exe')) } finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'V2 focused tests failed.' }
