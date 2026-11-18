param([string]$Root,[switch]$PrepareOnly)
$ErrorActionPreference='Stop'
if (-not $Root) {
    if ($env:DARWIN_BOOTSTRAP_FILE) { $Root=Split-Path -Parent $env:DARWIN_BOOTSTRAP_FILE }
    else { $Root=$PSScriptRoot }
}
$Root=[IO.Path]::GetFullPath($Root)
$core=Join-Path $Root 'DarwinCore.zip'
if (-not [IO.File]::Exists($core)) { throw 'Place DarwinCore.zip beside Start.cmd before starting.' }
$cacheRoot=Join-Path $Root '.runtime'
if ((Test-Path -LiteralPath $cacheRoot) -and ((Get-Item -LiteralPath $cacheRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Runtime directory must not be a link.' }
[IO.Directory]::CreateDirectory($cacheRoot) | Out-Null
$lock=$null;$archive=$null
function File-Hash([string]$path) {
    $stream=[IO.File]::OpenRead($path);$sha=[Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose();$stream.Dispose() }
}
try {
    # Only one launcher may prepare/start this installation. The game also owns its independent data lease.
    $lock=[IO.File]::Open((Join-Path $cacheRoot 'launcher.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $hash=File-Hash $core
    $archive=[IO.Compression.ZipFile]::OpenRead($core)
    $entries=[Collections.Generic.Dictionary[string,IO.Compression.ZipArchiveEntry]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$total=0
    function Safe-Name([string]$name) {
        if (-not $name -or $name.Contains('\') -or $name.Contains(':') -or $name.StartsWith('/')) { throw "Unsafe core path: $name" }
        foreach ($part in $name.Split('/')) {
            if (-not $part -or $part -eq '.' -or $part -eq '..' -or $part.EndsWith(' ') -or $part.EndsWith('.') -or $part -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?$') { throw "Unsafe core path: $name" }
        }
    }
    foreach ($entry in $archive.Entries) {
        Safe-Name $entry.FullName
        if ((($entry.ExternalAttributes -shr 16) -band 61440) -eq 40960) { throw 'Core links are not supported.' }
        $entries.Add($entry.FullName,$entry)
        $total+=$entry.Length
    }
    if ($entries.Count -gt 100000 -or $total -gt 17179869184) { throw 'Core exceeds size limits.' }
    if (-not $entries.ContainsKey('manifest.json') -or $entries['manifest.json'].Length -gt 16777216) { throw 'Missing or oversized core manifest.' }
    $reader=[IO.StreamReader]::new($entries['manifest.json'].Open(),[Text.Encoding]::UTF8)
    try { $manifest=$reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($manifest.schema -ne 1 -or $manifest.product -ne 'DarwinParadise.Server' -or $manifest.target -notin @('windows-headless','windows-server') -or $manifest.entry -ne 'DarwinServer.exe') { throw 'Unsupported server core.' }
    $files=@($manifest.files.PSObject.Properties)
    if ($files.Count -eq 0 -or $files.Count+1 -ne $entries.Count) { throw 'Core file list does not match manifest.' }
    $required=@('DarwinServer.exe','UnityPlayer.dll','Console.ps1','Stop.ps1','server.example.json')
    foreach ($name in $required) { if (-not $manifest.files.PSObject.Properties[$name]) { throw "Incomplete core: $name" } }
    foreach ($file in $files) {
        Safe-Name $file.Name
        if ($file.Name -ieq 'manifest.json' -or -not $entries.ContainsKey($file.Name) -or $file.Value.sha256 -notmatch '^[0-9a-fA-F]{64}$' -or $file.Value.size -lt 0 -or $file.Value.size -ne $entries[$file.Name].Length) { throw "Invalid core metadata: $($file.Name)" }
    }
    function Verify-Runtime([string]$directory) {
        if (-not [IO.Directory]::Exists($directory)) { return $false }
        if ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Runtime directory must not be a link.' }
        $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($directory);$count=0
        while ($pending.Count -gt 0) {
            foreach ($item in (Get-ChildItem -LiteralPath $pending.Pop() -Force)) {
                if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Runtime contents must not contain links.' }
                if ($item.PSIsContainer) { $pending.Push($item.FullName) } else { $count++ }
            }
        }
        if ($count -ne $files.Count+1) { return $false }
        foreach ($file in $files) {
            $path=Join-Path $directory $file.Name
            if (-not [IO.File]::Exists($path) -or ([IO.FileInfo]$path).Length -ne $file.Value.size -or (File-Hash $path) -ine $file.Value.sha256) { return $false }
        }
        return $true
    }
    $runtime=Join-Path $cacheRoot $hash
    $reference=Join-Path $cacheRoot 'current.json'
    if ([IO.File]::Exists($reference)) {
        try {
            $previous=[IO.File]::ReadAllText($reference) | ConvertFrom-Json
            if ($previous.coreHash -eq $hash -and $previous.directory -match ('^'+$hash+'(?:-[0-9a-f]{32})?$')) { $runtime=Join-Path $cacheRoot $previous.directory }
        } catch { Write-Host 'Rebuilding runtime reference.' }
    }
    if (-not (Verify-Runtime $runtime)) {
        # Use a new immutable directory if a cache was damaged; never remove a directory a running game might use.
        if (Test-Path -LiteralPath $runtime) { $runtime=Join-Path $cacheRoot ($hash+'-'+[Guid]::NewGuid().ToString('N')) }
        $staging=Join-Path $cacheRoot ('.extract-'+[Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($staging) | Out-Null
        $prefix=[IO.Path]::GetFullPath($staging)+[IO.Path]::DirectorySeparatorChar
        Write-Host 'Preparing bundled server runtime...'
        foreach ($file in $files) {
            $path=[IO.Path]::GetFullPath((Join-Path $staging $file.Name))
            if (-not $path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Core path escapes runtime directory.' }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
            $inputStream=$entries[$file.Name].Open();$outputStream=[IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose();$inputStream.Dispose() }
            if ((File-Hash $path) -ine $file.Value.sha256) { throw "Corrupt core file: $($file.Name)" }
        }
        $manifestStream=$entries['manifest.json'].Open();$manifestFile=[IO.File]::Create((Join-Path $staging 'manifest.json'))
        try { $manifestStream.CopyTo($manifestFile) } finally { $manifestFile.Dispose();$manifestStream.Dispose() }
        # Both resolved paths must remain inside the known cache root before a directory move.
        $cachePrefix=[IO.Path]::GetFullPath($cacheRoot)+[IO.Path]::DirectorySeparatorChar
        if (-not $staging.StartsWith($cachePrefix,[StringComparison]::OrdinalIgnoreCase) -or -not $runtime.StartsWith($cachePrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime move escapes cache root.' }
        [IO.Directory]::Move($staging,$runtime)
    }
    $referenceTemp=$reference+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
    [IO.File]::WriteAllText($referenceTemp,(@{coreHash=$hash;directory=[IO.Path]::GetFileName($runtime)} | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
# Use an explicit backup path: Windows PowerShell converts null to an empty path.
    if ([IO.File]::Exists($reference)) { [IO.File]::Replace($referenceTemp,$reference,$reference+'.previous') } else { [IO.File]::Move($referenceTemp,$reference) }
    $data=Join-Path $Root 'data'
    if ((Test-Path -LiteralPath $data) -and ((Get-Item -LiteralPath $data -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Data directory must not be a link.' }
    [IO.Directory]::CreateDirectory($data) | Out-Null
    $config=Join-Path $data 'server.json'
    if (-not [IO.File]::Exists($config)) { [IO.File]::Copy((Join-Path $runtime 'server.example.json'),$config) }
    if ($PrepareOnly) { Write-Host "Prepared runtime: $runtime";exit 0 }
    Write-Host "Configuration: $config"
    & (Join-Path $runtime 'Console.ps1') -Executable (Join-Path $runtime $manifest.entry) -DataDirectory $data
    exit $LASTEXITCODE
} finally {
    if ($null -ne $archive) { $archive.Dispose() }
    if ($null -ne $lock) { $lock.Dispose() }
}
