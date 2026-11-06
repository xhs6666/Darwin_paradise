param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference='Stop'
# ★ 新增：离线使用本机SDK内置Roslyn；显式执行DLL，避免Windows首次运行apphost的启动阻塞。
$toolProject=Join-Path $PSScriptRoot 'FlowDocs.csproj'
dotnet restore $toolProject --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build $toolProject --no-restore -v:q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet (Join-Path $PSScriptRoot '../../Artifacts/FlowDocs/bin/net10.0/FlowDocs.dll') --self-test
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet (Join-Path $PSScriptRoot '../../Artifacts/FlowDocs/bin/net10.0/FlowDocs.dll') $ProjectRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python (Join-Path $PSScriptRoot 'validate.py') $ProjectRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python (Join-Path $PSScriptRoot '../generate_file_catalog.py') $ProjectRoot
exit $LASTEXITCODE
