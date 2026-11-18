param(
    [string]$Executable=(Join-Path $PSScriptRoot 'DarwinServer.exe'),
    [string]$DataDirectory=(Join-Path $PSScriptRoot 'data')
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
    throw 'Unresolved stop.request. Inspect the previous save/stop result before restarting.'
}
# Read configuration before starting; never overwrite an existing server configuration.
$settings=Get-Content -LiteralPath $serverConfig -Raw | ConvertFrom-Json
$logPath=Join-Path $serverData ('console-'+[Guid]::NewGuid().ToString('N')+'.log')
$runId=[Guid]::NewGuid().ToString('N')
Write-Host "Configuration: $serverConfig"
Write-Host "Port: $($settings.port) | World: $($settings.worldName) | Seed: $($settings.seed)"
Write-Host 'Commands: status, stop (save and stop), help. Edit server.json while stopped, then restart.'
$arguments=@('-batchmode','-nographics','-server-data',('"'+$serverData+'"'),'-server-config',('"'+$serverConfig+'"'),'-server-run-id',$runId,'-logFile',('"'+$logPath+'"'))
$serverProcess=Start-Process -FilePath $serverExecutable -WorkingDirectory (Split-Path -Parent $serverExecutable) -ArgumentList $arguments -WindowStyle Hidden -PassThru
$reader=$null
$inputLine=''
$requested=$false
function Show-ServerStatus {
    $receipt=Join-Path $serverData 'server-status.json'
    if (Test-Path -LiteralPath $receipt) { Write-Host ([System.IO.File]::ReadAllText($receipt)) }
    else { Write-Host 'Waiting for server startup receipt...' }
}
try {
    while (-not $serverProcess.HasExited) {
        if ($null -eq $reader -and (Test-Path -LiteralPath $logPath)) {
            $stream=[System.IO.File]::Open($logPath,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
            $reader=[System.IO.StreamReader]::new($stream,[System.Text.Encoding]::UTF8)
        }
        if ($null -ne $reader) {
            while ($null -ne ($line=$reader.ReadLine())) { Write-Host $line }
        }
        if (-not [Console]::IsInputRedirected) {
            while ([Console]::KeyAvailable) {
                $key=[Console]::ReadKey($true)
                if ($key.Key -eq [ConsoleKey]::Enter) {
                    Write-Host ''
                    switch ($inputLine.Trim().ToLowerInvariant()) {
                        'stop' {
                            & (Join-Path $PSScriptRoot 'Stop.ps1') -DataDirectory $serverData
                            $requested=$true
                        }
                        'status' { Show-ServerStatus }
                        'help' { Write-Host 'status: show health; stop: save and stop; settings: edit server.json while stopped.' }
                        '' { }
                        default { Write-Host 'Unknown command. Type help.' }
                    }
                    $inputLine=''
                } elseif ($key.Key -eq [ConsoleKey]::Backspace) {
                    if ($inputLine.Length -gt 0) { $inputLine=$inputLine.Substring(0,$inputLine.Length-1);[Console]::Write("`b `b") }
                } elseif (-not [char]::IsControl($key.KeyChar)) {
                    $inputLine+=$key.KeyChar;[Console]::Write($key.KeyChar)
                }
            }
        }
        Start-Sleep -Milliseconds 100
        $serverProcess.Refresh()
    }
    if ($null -ne $reader) { while ($null -ne ($line=$reader.ReadLine())) { Write-Host $line } }
} finally {
    if ($null -ne $reader) { $reader.Dispose() }
    # A console interrupt asks the same running instance to save; it never force-kills the server.
    if (-not $serverProcess.HasExited) {
        $receipt=Join-Path $serverData 'server-status.json'
        $health=$null
        if (Test-Path -LiteralPath $receipt) { $health=Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json }
        if ($null -ne $health -and $health.runId -eq $runId -and $health.pid -eq $serverProcess.Id) {
            if (-not $requested) { & (Join-Path $PSScriptRoot 'Stop.ps1') -DataDirectory $serverData }
            if (-not $serverProcess.WaitForExit(60000)) { Write-Warning "Server still saving. PID $($serverProcess.Id); data: $serverData" }
        } else { Write-Warning "Server has not published this run's receipt; inspect PID $($serverProcess.Id) and $logPath" }
    }
}
if ($serverProcess.HasExited) {
    $serverProcess.WaitForExit()
    Write-Host "Server exit code: $($serverProcess.ExitCode)"
    exit $serverProcess.ExitCode
}
exit 1
