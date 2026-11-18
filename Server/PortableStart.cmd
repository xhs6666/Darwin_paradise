@echo off
setlocal
set "DARWIN_BOOTSTRAP_FILE=%~f0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$text=[IO.File]::ReadAllText($env:DARWIN_BOOTSTRAP_FILE,[Text.Encoding]::UTF8);$marker='#'+' DARWIN_POWERSHELL';$offset=$text.IndexOf($marker,[StringComparison]::Ordinal);if($offset -lt 0){throw 'Incomplete Start.cmd'};& ([ScriptBlock]::Create($text.Substring($offset+$marker.Length)))"
set "DARWIN_EXIT_CODE=%ERRORLEVEL%"
if not "%DARWIN_EXIT_CODE%"=="0" pause
exit /b %DARWIN_EXIT_CODE%
# DARWIN_POWERSHELL
