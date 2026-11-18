param([string]$UnityManagedPath)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $taskRoot
try {
    $taskArtifacts=Join-Path $taskRoot 'Artifacts'
    New-Item -ItemType Directory -Force -Path $taskArtifacts | Out-Null
    $env:APPDATA=Join-Path $taskArtifacts 'Dotnet/AppData'
    $env:DOTNET_CLI_HOME=Join-Path $taskArtifacts 'Dotnet'
    $env:NUGET_PACKAGES=Join-Path $taskArtifacts 'Dotnet/packages'
    $taskConfig=Join-Path $PSScriptRoot 'ServerRegression/NuGet.Config'
    if (-not $UnityManagedPath) {
        [xml]$taskProject=Get-Content -LiteralPath 'DarwinPark.UI.csproj' -Raw
        $taskEngine=($taskProject.Project.ItemGroup.Reference | Where-Object Include -eq 'UnityEngine.CoreModule' | Select-Object -First 1).HintPath
        if (-not $taskEngine) { throw 'Generate Unity IDE project files or pass -UnityManagedPath.' }
        $UnityManagedPath=Split-Path (Split-Path $taskEngine -Parent) -Parent
    }
    dotnet restore Tools/ServerRegression/ServerRegression.csproj --configfile $taskConfig
    if ($LASTEXITCODE) { throw 'Network regression restore failed' }
    dotnet run --project Tools/ServerRegression/ServerRegression.csproj --no-restore
    if ($LASTEXITCODE) { throw 'Network regression failed' }
    foreach ($taskMode in @('client','server','editor')) {
        python Tools/verify_unity_compile.py $taskMode
        if ($LASTEXITCODE) { throw 'Compile project preparation failed' }
        $taskAssembly=if ($taskMode -eq 'editor') { 'DarwinPark.Editor' } else { 'Assembly-CSharp' }
        $taskCompileProject="Artifacts/ServerCompile/$taskAssembly/$taskAssembly.csproj"
        dotnet restore $taskCompileProject --configfile $taskConfig
        if ($LASTEXITCODE) { throw "$taskMode restore failed" }
        dotnet build $taskCompileProject --no-restore -v:q
        if ($LASTEXITCODE) { throw "$taskMode compile failed" }
    }
    dotnet restore Tools/ClimateRegression/ClimateRegression.csproj --configfile $taskConfig
    if ($LASTEXITCODE) { throw 'Climate regression restore failed' }
    dotnet run --project Tools/ClimateRegression/ClimateRegression.csproj --no-restore "-p:UnityManagedPath=$UnityManagedPath"
    if ($LASTEXITCODE) { throw 'Climate regression failed' }
    python Tools/TerrainRegression/prepare.py
    if ($LASTEXITCODE) { throw 'Terrain adapter preparation failed' }
    dotnet restore Tools/TerrainRegression/TerrainRegression.csproj --configfile $taskConfig
    if ($LASTEXITCODE) { throw 'Terrain regression restore failed' }
    dotnet run --project Tools/TerrainRegression/TerrainRegression.csproj --no-restore "-p:UnityManagedPath=$UnityManagedPath"
    if ($LASTEXITCODE) { throw 'Terrain regression failed' }
    python Tools/audit_code_structure.py
    if ($LASTEXITCODE) { throw 'Structure audit failed' }
    python Tools/validate_server_structure.py
    if ($LASTEXITCODE) { throw 'Server structure validation failed' }
    python -m unittest discover -s Tools/DarwinCoreBuilder -p test_builder.py -v
    if ($LASTEXITCODE) { throw 'Core builder/update regression failed' }
    python -m unittest discover -s Tools/DarwinCoreBuilder -p test_bootstrap.py -v
    if ($LASTEXITCODE) { throw 'Portable core bootstrap regression failed' }
} finally { Pop-Location }
