param([Parameter(Mandatory=$true)][string]$DataDirectory)
$ErrorActionPreference='Stop'
$serverData=(Resolve-Path -LiteralPath $DataDirectory).Path
[System.IO.File]::WriteAllText((Join-Path $serverData 'stop.request'),'save-and-stop',[System.Text.UTF8Encoding]::new($false))
Write-Output 'Requested save and stop. Wait for process exit 0 before replacing binaries.'
