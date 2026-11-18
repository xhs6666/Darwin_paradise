param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path, [switch]$NoRestore)
$ErrorActionPreference='Stop'
# ★ 新增：离线使用本机SDK内置Roslyn；显式执行DLL，避免Windows首次运行apphost的启动阻塞。
$toolProject=Join-Path $PSScriptRoot 'FlowDocs.csproj'
# ★ 适配：已还原的工作区可跳过恢复，避免受本机不可读的NuGet用户配置阻塞文档更新。
if (-not $NoRestore) {
    dotnet restore $toolProject --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
dotnet build $toolProject --no-restore -v:q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet (Join-Path $PSScriptRoot '../../Artifacts/FlowDocs/bin/net10.0/FlowDocs.dll') --self-test
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet (Join-Path $PSScriptRoot '../../Artifacts/FlowDocs/bin/net10.0/FlowDocs.dll') $ProjectRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python (Join-Path $PSScriptRoot 'validate.py') $ProjectRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python (Join-Path $PSScriptRoot '../generate_file_catalog.py') $ProjectRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
# ★ 适配：重新生成索引后检查主题文档与模块章节的链接，避免下次维护重新引入断链。
python (Join-Path $PSScriptRoot '../validate_docs.py')
exit $LASTEXITCODE
