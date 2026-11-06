"""★ 新增：把人工职责说明与实际文件清单合成维护索引；新增C#必须有明确说明，不猜测业务用途。"""
from pathlib import Path
import hashlib,json,re,subprocess,sys
root=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parents[1];roles=json.loads((root/'Tools/file_catalog_roles.json').read_text('utf-8'))
tracked=set(subprocess.check_output(['git','ls-files','-z'],cwd=root).decode('utf-8').split('\0'))-{''}
cs={p.relative_to(root).as_posix() for p in (root/'Assets').rglob('*.cs')}
missing=cs-set(roles['scripts'])
if missing:raise SystemExit('请在Tools/file_catalog_roles.json补充脚本职责：\n'+'\n'.join(sorted(missing)))
flow=json.loads((root/'Docs/运行流程图/manifest.json').read_text('utf-8'));flow_by_path={s['path']:s for s in flow['scripts']}
if {p for p in flow_by_path if p.endswith('.cs')}!=cs:raise SystemExit('源码清单已变化，请先运行Tools/FlowDocs/Generate.ps1更新流程图。')
extra={'Tools/generate_file_catalog.py','Tools/file_catalog_roles.json','Docs/开发设计/项目文件作用索引.md','Docs/运行流程图/file-catalog.js','Tools/FlowDocs/FlowDocs.csproj','Docs/验证记录/文件作用索引验证-2026-11-06.md'}
files=(tracked|extra|set(roles['files']))
files={p for p in files if (root/p).is_file() or p=='Docs/开发设计/项目文件作用索引.md'}
meta=sum(p.endswith('.meta') for p in tracked)
files={p for p in files if not p.endswith('.meta') and not p.startswith('Docs/运行流程图/脚本/') and not p.startswith('Tools/FlowDocs/obj/')}
modules={'GameRoot':'启动与实体装配','Core':'公共基础服务','Genetics':'基因数据、表达与演化','Entities':'实体、生命与生态','AI':'AI决策模块','Environment':'环境参数与采样','Planet':'星球生成与视觉','UI':'界面、操控、世界与联机','Editor':'编辑器工具与回归','Tutorial':'Unity模板说明'}

def link(p,label=None):return f'[{label or p}](../../{p})'
def text(value):return str(value).replace('|','／').replace('\n',' ')
def describe(p):
 if p in roles['scripts']:return roles['scripts'][p]
 if p in roles['files']:return roles['files'][p]
 file=root/p;name=file.stem
 if p=='Docs/开发设计/项目文件作用索引.md':return '本文件：按目录、需求和文件名解释职责，并关联方法流程图。'
 if p=='Docs/运行流程图/file-catalog.js':return '供流程图页面显示每个脚本用途的离线说明数据，由文件索引工具生成。'
 if p.startswith('Assets/Resources/Genes/') and file.suffix=='.json':
  data=json.loads(file.read_text('utf-8-sig'));return '“'+data.get('displayName',name)+'”的状态、属性曲线和关系定义；逻辑标识：'+data.get('logicScriptId','由定义/父系提供')+'。'
 if file.suffix=='.asmdef':
  data=json.loads(file.read_text('utf-8-sig'));return '定义程序集 '+data.get('name',name)+' 的编译边界与引用'+('；限制在Editor平台。' if 'Editor' in data.get('includePlatforms',[]) else '。')
 if p.startswith('Assets/Resources/UI/Icons/'):return 'UI图标；资源键 UI/Icons/'+name+'，由图标注册表/主题引用。'
 if p.startswith('Assets/Resources/UI/Surfaces/'):return '九宫格主题表面/控件贴图；资源键 UI/Surfaces/'+name+'。'
 if p.startswith('Artifacts/'):return '历史提交的验证产物/截图：'+file.name+'；不属于主工程的运行输入。'
 if p.startswith('Docs/'):
  if file.suffix=='.md':
   title=next((line.lstrip('# ').strip() for line in file.read_text('utf-8-sig').splitlines() if line.startswith('# ')),name)
   group='历史验证范围、结果及限制' if '/验证记录/' in p else '使用步骤与操作说明' if '/操作指南/' in p else '设计、规则或维护说明'
   return group+'：'+title+'。'
  return '界面设计参考/提示资料：'+file.name+'；不是运行时游戏资源。'
 if file.suffix=='.csproj':return 'Unity/IDE生成的C#编译项目：'+name+'；保存源码列表、平台符号和程序集引用。'
 if file.suffix=='.slnx':return 'IDE与dotnet构建使用的解决方案入口，组织各C#项目。'
 raise ValueError('尚无文件用途说明：'+p)

# ★ 新增：每个表项来自明确说明或受限的资源类型规则；未知文件报错，防止静默漏掉维护对象。
for p in sorted(files):describe(p)
lines=['# 项目文件作用索引','','这份索引按“改什么 → 找哪个文件 → 查看方法流程”组织。C#脚本逐项说明；贴图、配置、文档也有文件条目。Unity自动维护的缓存和本机玩家数据按目录说明，不枚举每次运行都会变化的内部文件。','','## 先理解整体结构','',
'游戏启动由 `GameBootstrap` 建立实体生成入口；`WorldSessionManager` 组织世界加载和保存。`SimulationClock` 提供统一模拟时间，`EntityGeneController` 推进基因行为；实体生态组件处理生命、营养、攻击、繁殖、种群和任务。`Planet`/`Environment` 提供世界与采样，`UI` 把这些能力接到菜单、操控与管理界面。','','[打开全部运行流程图](../运行流程图/README.md) · [12条业务主流程](../运行流程图/主流程.md)','','## 根目录与本机目录','','|目录/文件|作用|','|---|---|',
'|`Assets/`|Unity实际游戏内容：源码、场景、预制体、材质、配置和媒体。|',
'|`Assets/DarwinPark/`|本项目主要C#模块；下面按每个脚本列出职责。|',
'|`Assets/Resources/`|随游戏打包、按Resources路径加载的内置数据和UI资源。不是所有配置都能在发布后热改。|',
'|`Assets/StreamingAssets/`|按文件路径读取的媒体，当前为菜单视频与封面。|',
'|`Packages/`|Unity包依赖声明与锁定版本。|',
'|`ProjectSettings/`|Unity工程、图形、质量、物理、时间、标签和平台设置。|',
'|`config/`|外部游戏数值入口，当前为带注释的gameplay.json。|',
'|`mods/Default/`|基因树工具写出的Genes/、tree.json及entity_states.json。已有世界通常保留自身配置快照。|',
'|`save/<世界编号>/world.json`|每个世界的地形配置、时间、实体、基因、种群历史等持久状态。|',
'|`templates/entities.json`|跨世界实体模板库，供模板投放/随机生成使用。|',
'|`templates/spawn-conditions/*.json`|独立命名的基因条件来源；与已启用的世界比例规则快照不同。|',
'|`Docs/`|设计、操作、验证记录、文件索引和生成的流程图手册。|',
'|`Tools/`|离线资源、流程图及文件说明生成工具，不随游戏每帧运行。|',
'|`Artifacts/`|隔离验证工程、构建产物、截图、工具二进制和验证报告。|',
'|`Library/`、`Temp/`|Unity导入、编译和运行期间生成的缓存/临时内容，通常不作为功能修改入口。|',
'|`Logs/`|Unity导入日志及按日志配置写入的游戏日志；Windows下logs/与Logs/是同一目录名。|',
'|`UserSettings/`、`.vs/`|本机编辑器布局/偏好与Visual Studio缓存。游戏改键另外保存在PlayerPrefs。|',
'|`.git/`|Git版本历史和索引。|',
'|`*.csproj`、`*.slnx`|IDE/编译入口；Unity生成的csproj应由工程同步更新。|','','## 按修改目标找文件','','|想修改的内容|先看这些文件|','|---|---|',
'|行动/基础饱食消耗|`config/gameplay.json` → `GameplayTuning.cs` → `EntityVitals.cs`。|',
'|默认实体基因|`Assets/Resources/Entities/entity_default.json`、`EntityFactory.cs`、`EntityGeneController.cs`。|',
'|基因定义与进化关系|`Assets/Resources/Genes/`、`Genetics/default_tree.json`、`GeneTreeStorage.cs`、`GeneLogicRegistry.cs`。|',
'|普通/孤雌繁殖|`ReproductionGeneLogic.cs`、`ParthenogenesisEvolutionUpgrade.cs`、`ecology_balance.json`及`parthenogenesis.json`。|',
'|王后、工匠和补给|`AntColonyMember.cs`、`AntColonyWorker.cs`、`AntColonyReproduction.cs`、`ColonyFeeding.cs`及`ant_colony.json`。|',
'|攻击、尸体与营养守恒|`EntityCombat.cs`、`EntityDeathSettlement.cs`、`NutritionTransfer.cs`、`ColonyResource.cs`。|',
'|营养寄生/卵寄生|`NutritionParasite.cs`、`EggHostPregnancy.cs`及`HeterotrophyGeneLogic.cs`；旧兼容入口不要与新逻辑混淆。|',
'|种群活动范围和外观|`ColonyActivityRange.cs`、`PopulationRangeLayer.cs`、`PopulationRangeOverlay.hlsl`、`PopulationArchive.cs`。|',
'|随机生成和比例维持|`RandomSpawnWorkbench.cs`、`RandomPlanetSpawner.cs`、`PopulationMaintenance.cs`、`RandomSpawnPresetStore.cs`。|',
'|菜单/底栏/控件风格|`GameMainMenu.cs`、`SimulationControlPanel.cs`、`UIFactory.cs`、`UIVisualTheme.cs`、`RuntimeMenuTheme.cs`及`UI/theme.json`。|',
'|改键与实体操控|`ControlBindings.cs`、`ControlSettingsPage.cs`、`EntityPossessionInput.cs`、`EntityManualControl.cs`。|',
'|世界存档|`WorldSessionManager.cs`、`WorldSaveStore.cs`、`EntitySaveState.cs`、`ColonyWorldState.cs`。|',
'|地形、气候和矿脉|`PlanetTerrainGenerator.cs`、`PlanetTerrainMeshBuilder.cs`、`PlanetClimateModel.cs`、`PlanetOreSystem.cs`。|',
'|局域网权限与同步|`LanSession.cs`、`LanEditActions.cs`、`LanProtocol.cs`、`LanWorldSnapshot.cs`。|',
'|日志和诊断|`RuntimeFileLog.cs`、`SessionLogWriter.cs`、`RuntimeCrashDiagnostics.cs`及runtime_logging/diagnostics配置。|','','## 各模块C#脚本','','“运行时”表示可被游戏使用，不证明每个声明都在当前场景主动执行；具体挂载、创建和调用看流程图。Editor目录是测试/工具入口，不是玩家每帧逻辑。']
for module,title in modules.items():
 entries=[s for s in flow['scripts'] if s['module']==module and s['path'].endswith('.cs')]
 if not entries:continue
 lines+=['',f'### {module}：{title}（{len(entries)}个）','','|文件|作用|流程/源码|','|---|---|---|']
 for s in entries:
  p=s['path'];lines.append(f"|{link(p)}|{text(roles['scripts'][p])}|[查看](../运行流程图/脚本/{s['id']}.md)|")
sections=[('内置与外部配置',lambda p:p.startswith(('Assets/Resources/','config/')) and Path(p).suffix=='.json'),('场景、预制体和程序集定义',lambda p:Path(p).suffix in ('.unity','.prefab','.asmdef')),('Shader与HLSL',lambda p:Path(p).suffix in ('.shader','.hlsl')),('材质、渲染与媒体资源',lambda p:p.startswith('Assets/') and Path(p).suffix not in ('.cs','.json','.unity','.prefab','.asmdef','.shader','.hlsl') and not p.startswith('Assets/Resources/UI/')),('UI贴图与图标',lambda p:p.startswith('Assets/Resources/UI/') and Path(p).suffix!='.json'),('工程、依赖与仓库入口',lambda p:p.startswith(('Packages/','ProjectSettings/')) or '/' not in p),('历史验证产物',lambda p:p.startswith('Artifacts/')),('维护工具',lambda p:p.startswith('Tools/')),('文档与流程图阅读器',lambda p:p.startswith('Docs/'))]
represented=set(cs)
for title,predicate in sections:
 selected=[p for p in sorted(files) if p not in represented and predicate(p)]
 if not selected:continue
 lines+=['',f'## {title}','','|文件|作用|','|---|---|']
 for p in selected:lines.append('|'+link(p)+'|'+text(describe(p))+'|');represented.add(p)
check_remaining=files-represented
if check_remaining:raise SystemExit('未编入目录：'+str(sorted(check_remaining)))
lines+=['','## 配套文件与容易混淆的地方','',
f'- **`.meta`**：当前版本控制中有{meta}个。它们保存Unity资源GUID和导入设置，作用属于对应资源/目录，不是额外游戏功能；本索引不为每个副本重复一行。',
'- **配置文件与存档快照不同**：Resources为内置默认，config/gameplay.json为外部调参，mods为编辑后的基因内容，world.json为某个世界已经保存的状态。不要假定只改一处就能改变所有旧档。',
'- **同类文件不一定是当前主实现**：例如OreGenerator/OreNode属于旧矿石对象路径，当前主矿脉看PlanetOreSystem；旧ParasiticGeneLogic/ParasiteBehavior不要当作新的NutritionParasite。Shader实际选用由材质和天气组件决定。',
'- **一个文件可能有多个类**：HeterotrophyGeneLogic.cs还包含食腐/卵寄生实现，GeneLogicRegistry.cs也定义IGeneLogic接口；logicScriptId不保证与文件名相同。',
'- **界面与逻辑分开**：按钮界面负责编辑/提交请求，具体生成、繁殖、伤害与权限检查应追到Entities和LanSession等实际执行入口。',
'- **流程图分图**：`Docs/运行流程图/脚本/*.md`每个对应一个源码文件，统一作用是保存该文件的方法图与调用链接，已由上面的“查看”列逐个关联。',
'- **更新方式**：新增或改动文件职责时修改 `Tools/file_catalog_roles.json`，再运行 `python Tools/generate_file_catalog.py`。流程图的一键更新也会同步刷新文件索引。','']
target=root/'Docs/开发设计/项目文件作用索引.md';target.write_text('\n'.join(lines).rstrip()+'\n',encoding='utf-8')
explanations={**roles['files'],**roles['scripts']};(root/'Docs/运行流程图/file-catalog.js').write_text('window.FLOW_FILE_ROLES='+json.dumps(explanations,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
# ★ 新增：核对每个本地文件链接和C#覆盖，避免索引留有不存在的目标。
for p in files:assert (root/p).is_file(),p
assert all(roles['scripts'][p].strip() for p in cs)
report={'result':'PASS','csharpScripts':len(cs),'catalogFiles':len(files),'metaCompanions':meta,'sourceRoles':len(roles['scripts']),'unknownFiles':0}
output=root/'Artifacts/FileCatalog';output.mkdir(parents=True,exist_ok=True);(output/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(report,ensure_ascii=True))
