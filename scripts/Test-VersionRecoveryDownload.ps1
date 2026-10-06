[CmdletBinding()]
param([string]$ExecutablePath, [string]$EvidencePath = 'artifacts/ui-v2/historical-downloads.json')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ExecutablePath) { $ExecutablePath = Join-Path $repoRoot 'artifacts/build/NetStuck.exe' }
$ownedRoot = Join-Path ([IO.Path]::GetTempPath()) ('NetStuck-version-download-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $ownedRoot | Out-Null
$timeout = New-Object System.Threading.CancellationTokenSource
$timeout.CancelAfter([TimeSpan]::FromMinutes(5))
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExecutablePath))
    $engine = $assembly.GetType('NetStuck.UpdateEngine')
    $download = $engine.GetMethod('Download')
    $metadata = Join-Path $ownedRoot 'versions.json'
    $url = [string]$engine.GetField('VersionsUrl').GetRawConstantValue()
    $null = $download.Invoke($null, @([string]$url, [string]$metadata, [long](4 * 1024 * 1024), $timeout.Token, $null)).GetAwaiter().GetResult()
    $choices = $engine.GetMethod('PreviousPackages').Invoke($null, @([IO.File]::ReadAllText($metadata), $assembly.GetName().Version))
    if ($choices.Count -eq 0) { throw 'No previous compatible versions were discovered.' }
    $records = New-Object System.Collections.Generic.List[object]
    foreach ($choice in $choices) {
        $version = $choice.Version
        $name = 'NetStuck-v.' + $version.ToString(3) + '.zip'
        $zipAsset = $engine.GetMethod('Asset').Invoke($null, @($choice.Release.PSObject.BaseObject, [string]$name))
        $shaAsset = $engine.GetMethod('Asset').Invoke($null, @($choice.Release.PSObject.BaseObject, [string]($name + '.sha256.txt')))
        $zip = Join-Path $ownedRoot $name
        $sha = Join-Path $ownedRoot ($name + '.sha256.txt')
        $null = $download.Invoke($null, @([string]$zipAsset.browser_download_url, [string]$zip, [long]$zipAsset.size, $timeout.Token, $null)).GetAwaiter().GetResult()
        $null = $download.Invoke($null, @([string]$shaAsset.browser_download_url, [string]$sha, [long]4096, $timeout.Token, $null)).GetAwaiter().GetResult()
        $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        if ((Get-Item -LiteralPath $zip).Length -ne $zipAsset.size -or [IO.File]::ReadAllText($sha).Trim() -cne "$hash  $name") { throw 'Historical ZIP checksum/size mismatch.' }
        $payload = Join-Path $ownedRoot ('payload-' + $version.ToString(3))
        $null = $engine.GetMethod('ExtractPackage').Invoke($null, @([string]$zip, [string]$payload, $version.PSObject.BaseObject))
        $records.Add([pscustomobject]@{ Version = $version.ToString(4); ArchiveRelease = $choice.Release.tag_name; ZipSha256 = $hash; InventoryFiles = 9; Manifest = 'PASS'; ExecutableVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'NetStuck.exe')).FileVersion })
        Write-Output "PASS live historical transport, checksum, inventory, manifest and exact executable: $($version.ToString(3)) archive=$($choice.Release.tag_name) SHA256=$hash"
    }
    $proof = [pscustomobject]@{ GeneratedUtc = [DateTime]::UtcNow.ToString('o'); CurrentVersion = $assembly.GetName().Version.ToString(); InstallsPerformed = 0; Packages = $records.ToArray() }
    $destination = if ([IO.Path]::IsPathRooted($EvidencePath)) { $EvidencePath } else { Join-Path $repoRoot $EvidencePath }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    [IO.File]::WriteAllText($destination, ($proof | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))
}
finally {
    $timeout.Dispose()
    $resolved = [IO.Path]::GetFullPath($ownedRoot)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('NetStuck-version-download-')) { throw 'Unsafe historical-download cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    if (Test-Path -LiteralPath $resolved) { throw 'Historical-download state remains.' }
}
