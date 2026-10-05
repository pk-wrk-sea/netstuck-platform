[CmdletBinding()]
param([string]$ExecutablePath)
$ErrorActionPreference='Stop'
if(-not $ExecutablePath){$ExecutablePath=Join-Path $PSScriptRoot '..\artifacts\build\NetStuck.exe'}
$owned=Join-Path ([IO.Path]::GetTempPath()) ('NetStuck-update-download-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $owned | Out-Null
try {
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    $assembly=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExecutablePath))
    $engine=$assembly.GetType('NetStuck.UpdateEngine')
    $download=$engine.GetMethod('Download')
    $cancel=[Threading.CancellationTokenSource]::new()
    $cancel.CancelAfter([TimeSpan]::FromMinutes(2))
    $metadata=Join-Path $owned 'release.json'
    $task=$download.Invoke($null,@('https://api.github.com/repos/pk-wrk-sea/netstuck-platform/releases/latest',[string]$metadata,[long]1048576,$cancel.Token,$null))
    $task.GetAwaiter().GetResult()
    $release=$engine.GetMethod('ParseRelease').Invoke($null,@([IO.File]::ReadAllText($metadata)))
    $version=$engine.GetMethod('ParseVersion').Invoke($null,@([string]$release.tag_name))
    $name='NetStuck-v.'+$version.ToString(3)+'.zip'
    $zipAsset=$engine.GetMethod('Asset').Invoke($null,@($release.PSObject.BaseObject,[string]$name))
    $shaAsset=$engine.GetMethod('Asset').Invoke($null,@($release.PSObject.BaseObject,[string]($name+'.sha256.txt')))
    $zip=Join-Path $owned $name
    $sha=Join-Path $owned 'zip.sha256'
    $download.Invoke($null,@([string]$zipAsset.browser_download_url,[string]$zip,[long]$zipAsset.size,$cancel.Token,$null)).GetAwaiter().GetResult()
    $download.Invoke($null,@([string]$shaAsset.browser_download_url,[string]$sha,[long]4096,$cancel.Token,$null)).GetAwaiter().GetResult()
    $hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if([IO.File]::ReadAllText($sha).Trim() -cne "$hash  $name"){throw 'ZIP checksum mismatch'}
    $engine.GetMethod('ExtractPackage').Invoke($null,@([string]$zip,[string](Join-Path $owned 'payload'),$version.PSObject.BaseObject))
    Write-Output "PASS live updater transport, metadata, ZIP hash, inventory, manifest and version: $($release.tag_name) SHA256=$hash"
}
finally {
    if($cancel){$cancel.Dispose()}
    $full=[IO.Path]::GetFullPath($owned)
    $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(-not $full.StartsWith($temp) -or -not [IO.Path]::GetFileName($full).StartsWith('NetStuck-update-download-')){throw 'Unsafe cleanup target'}
    Remove-Item -LiteralPath $full -Recurse -Force
}
