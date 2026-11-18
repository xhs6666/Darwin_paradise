$ErrorActionPreference = 'Stop'
python (Join-Path $PSScriptRoot 'main.py') gui
exit $LASTEXITCODE
