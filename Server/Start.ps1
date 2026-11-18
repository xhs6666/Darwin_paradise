param(
    [string]$Executable=(Join-Path $PSScriptRoot 'DarwinServer.exe'),
    [Parameter(Mandatory=$true)][string]$DataDirectory
)
$ErrorActionPreference='Stop'
$serverExecutable=(Resolve-Path -LiteralPath $Executable).Path
$serverData=[System.IO.Path]::GetFullPath($DataDirectory)
New-Item -ItemType Directory -Path $serverData -Force | Out-Null
$serverConfig=Join-Path $serverData 'server.json'
if (-not (Test-Path -LiteralPath $serverConfig)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'server.example.json') -Destination $serverConfig
}
if (Test-Path -LiteralPath (Join-Path $serverData 'stop.request')) {
    throw 'A previous stop.request is present. Inspect the previous stop/save failure before removing it.'
}
& $serverExecutable -batchmode -nographics -server-data $serverData -server-config $serverConfig -logFile (Join-Path $serverData 'unity-server.log')
exit $LASTEXITCODE
