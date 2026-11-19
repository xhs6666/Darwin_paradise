# Unity 游戏项目开发通用规则

本文件是 Unity 游戏项目的强制开发规范。任何代码/数据改动都必须遵守以下规则，并在完成后执行「改动后回归检查」，防止同类 Bug 重复出现。
规则面向通用 Unity 项目，具体项目可在本文件基础上追加项目专属条款。

---

## 总则

1. **稳定核心优先**：渲染、存档、交互、世界/关卡生成等核心系统一旦稳定，不得在其上堆叠新功能。
2. **可回滚**：每次改动应能独立定位、独立回滚，避免大范围耦合修改。
3. **可验证**：任何改动都必须有明确的验证方式（编译通过、运行时日志、回归清单）。
4. **写清楚「为什么」**：注释与提交记录说明决策原因，而非复述代码行为。

## 千实体实时性能验收

- 以 Windows 非开发版测量约 1,000 个活跃实体、1 倍模拟速度的完整场景。前后对照使用同一台设备、同一存档副本、同一分辨率、图形 API、画质和镜头。关闭帧率上限和垂直同步，保留正常移动、物理、环境和生态结算。
- 预热后分别记录轨道暂停、轨道运行、密集近景、战斗、过河及自动存档。用实时帧间隔报告平均帧率、P95、P99、最大帧时间、活体数和每个现实秒推进的游戏秒数。记录存档尖峰和实际文件是否可重载。短样本用于定位，发布前使用长样本，逐场景检验平均至少 45 FPS、P99 不超过 22.22 ms，且维持 1 倍实际模拟速度。
- 游戏时钟与固定物理步分别记录。只有取得实际相机绘制的证据，帧时间才可用于完整画面验收；绘制计数为零或 GPU 数据缺失的样本记录其有效覆盖范围。跨分支对照固定补步上限，旧存档格式差异通过独立生成等价夹具处理。
- 性能预算为中枢/感知/任务 4 ms，重力/岸线/碰撞 6 ms，生命/环境/种群 3.5 ms，渲染/界面 5 ms，调度/存档及余量 3.72 ms。报告主线程、渲染线程和 GPU 时间时注明重叠或包含关系。
- 中枢应按基因表达失效重建静态能力和马达引用，普通任务每 0.25～0.5 游戏秒错峰刷新，显式命令、受击和失效目标立即处理。合并重复计算时保留角色、附着、水位和真实生命状态的变化。物理快速路径必须保守验证预计运动范围及身体两侧，近岸、碰撞、跌落和手控保持真实物理判断。代谢只在稳定区间合并，饥饿、修复、溺水和死亡仍按阈值结算。
- 自动存档由主线程采集一致快照，后台写盘必须维持原子替换和备份顺序，显式保存需等待落盘。未达标或未覆盖的场景记入验证文档，保留独立分支等待后续验证。

---

## 规则 1：数据驱动（配置与内容数据化）

- 游戏内容——物品、单位、关卡、地形、掉落、UI 动作、数值阈值等——必须通过配置文件（JSON / ScriptableObject / Inspector 公共字段）定义，不得在代码中硬编码。
- 代码中禁止硬编码内容 ID、属性、阈值（例如温度阈值、方块/物品 ID 列表、掉落表）。需要可调参数时，放入配置或 Inspector 字段。
- **ID 三方一致**：文件名、配置内 `id` 字段、注册表/索引条目必须完全一致；代码引用的 ID 必须能在注册表中查到对应定义。
- **依赖完整性**：新增/修改定义时，同时检查其依赖（贴图、预制体、材质、掉落物、音效、动画）是否齐全。
- **加载失败必须告警**：配置缺失时不得静默使用默认值，应输出明确 `LogWarning`/`LogError`，便于定位。
- **加载路径可预测**：配置必须能被约定的加载方式（如 `Resources.Load` 固定路径、Addressables 标签）找到；目录层级变化需同步更新加载逻辑。

---

## 规则 2：注释与编码规范

- 每次逻辑改动必须添加清晰的中文注释，标注：
  - `★ 修复（防Bug回归）：原因`
  - `★ 新增：用途`
  - `★ 适配：用途`（调用新脚本暴露的接口时）
- 注释要说明「为什么这样做」，而不是复述代码在做什么。
- 修复历史问题时，注释中写明之前错误的原因，方便后人避免重蹈覆辙。
- 所有脚本文件统一使用 **UTF-8 编码（无 BOM）**，避免中文注释乱码。
- 手写 `.meta` 文件必须使用 GUID 生成器，写入后立即校验 `^[0-9a-f]{32}$`（多一位/少一位都会导致脚本无法导入、编译报 CS0103）。

---

## 规则 3：改动后回归检查（强制自查清单）

每次修改代码或数据后，必须逐项自查以下清单，确认无回归后再交付：

### 1. ID / 资源一致性
- 本次改动引用的所有 ID 是否已在对应注册表注册？
- 对应配置文件是否存在，内部 `id` 与文件名/注册表是否一致？
- 配置引用的贴图、预制体、材质、音效是否真实存在且大小写一致？

### 2. 存档读写成对
- `SaveXxx` 是否真的把数据写入了磁盘/持久化存储？
- `LoadXxx` 读取的路径/键名是否与写入完全一致？
- 时间、进度、累计值等复合状态是否成对保存与恢复（不能只存一部分）？
- 读取失败是否有异常隔离与明确日志，而不是静默崩溃或返回默认值？

### 3. 渲染 / 几何 / 边界
- 涉及网格/剔除时检查：数组越界、坐标方向（+方向取 Min、-方向取 Max）、边界脏标记、共面 Z-fighting。
- 索引计算必须是「当前坐标 + 偏移」，不能漏掉基准坐标。
- 不透明材质必须保持 `_Surface=0`、`_ZWrite=1`、Opaque 队列；透明材质只能用于独立材质，避免全局材质被误改。
- 程序化天空盒/后处理必须有兜底，避免夜晚/暗场景被洗白。
- 合并网格无法逐对象设置材质属性时，使用顶点色或 `MaterialPropertyBlock`，禁止修改 `sharedMaterial` 造成串色。

### 4. 交互 / 放置链路
- 射线遮罩是否排除了辅助碰撞体、触发器、自身？
- 目标可替换判断是否使用属性（如 `isLiquid`）而非硬编码 ID？
- 预览状态丢失后能否恢复？成功后是否清空预览？
- 创造模式是否误消耗物品？空手是否误放空气？
- 多部件模型/合并模型：放置扣除与破坏掉落是否按单位数一致计算？

### 5. 场景 / 组件挂载
- 新增 MonoBehaviour 时，必须在场景中确认它出现在所属 GameObject 的 `m_Component` 列表中。
- 往 `m_Component` 添加引用前，必须确认场景中存在对应的 `--- !u!114 &ID` 组件块（否则出现 Broken PPtr）。
- 运行时创建的 UI 必须先创建 `Canvas + GraphicRaycaster`，否则不渲染/不可点击。
- 打开任何覆盖式 UI（暂停、控制台、管理面板）时，统一短路所有游戏输入脚本，并成对控制鼠标锁定。

### 6. 生命周期与初始化顺序
- 场景加载相关初始化不能只依赖 `RuntimeInitializeOnLoadMethod`；关键配置应在「使用前一刻」应用。
- 依赖其他组件 `Start` 副作用的实体，若被提前禁用，其他组件必须能自愈补建（如 `CharacterController`）。
- 诊断日志必须在数据就绪后输出，避免误导。

### 7. 编译与静态检查
- 交付前执行 `dotnet build`（或打开 Unity 检查 Console），确保 0 error、0 warning 新增。
- 场景加载/资源解析失败必须输出明确错误；场景名需在 Build Settings 中。

---

## 规则 4：Bug 修复记录（防复发台账）

新修复的 Bug 必须在本文件追加记录：日期、现象、根因、修复文件、防复发要点。

| 日期 | 现象 | 根因 | 修复文件 | 防复发要点 |
| --- | --- | --- | --- | --- |
| 2026-11-17 | 存档卡片右键无响应；摄像头拖到极点停滞 | 非主键控件事件被标记 Ignore 导致 MouseDown 判断漏掉，输入阶段布局矩形也不可靠；摄像头±85°限位及欧拉重建丢失过极点姿态 | GameMainMenu.cs、WorldSaveDetailsPage.cs、OrbitCamera.cs、PlanetGlobeMinimap.cs、MenuPoleRegression.cs、HudNavigationRegression.cs | 菜单最前读取未消费 rawType，Repaint 缓存屏幕卡片及滚动视口；完整四元数沿局部轴过极点；17 项真实 OnGUI/轨道与21项 UGUI/持久化回归，保留硬件鼠标验收边界 |
| 2026-11-17 | 切换底栏页面后放置数量框仍悬浮，模板无法删除；球形地图拖动可能与镜头缓存争抢 | 数量框挂根 Canvas 未绑定来源生命周期，缺少模板删除入口和轨道方向同步接口 | PlacementQuantityPopup.cs、EntitySpawnerUI.cs、EntitySpawnPanel.cs、EntityTemplateManager.cs、SimulationControlPanel.cs、PlanetGlobeMinimap.cs、OrbitCamera.cs | 切页/来源失效/脚本重载/权限撤回关闭弹窗；模板确认后先写盘再更新列表；地图拖动同步轨道缓存并在结束/切页/失焦释放锁；真实 UGUI 与文件持久化播放回归 |
| 2026-11-17 | 存档列表无法查看实体明细或删除；直接删除存在误删、旧副本复现风险 | 原菜单只有进入世界入口，缺少详情、确认与文件边界管理 | GameMainMenu.cs、WorldSaveDetailsPage.cs、WorldSaveManagement.cs、SaveDetailsRegression.cs | 右键先隔离普通按钮、屏外裁切区域不命中；删除二次确认，使用中拒绝，完整归档及失败回滚；19 项隔离文件/详情状态检查，不以程序外 Event 冒充窗口验收 |
| 2026-11-17 | 河流呈网格折线、统一粗细，盆地积水不明显 | 洪泛发现父节点直接作为坡降出流；河段独立矩形且宽度固定；缺少可蓄水的内陆闭合盆地雕刻 | PlanetDrainageNetwork.cs、PlanetHydrology.cs、PlanetFreshWaterMesh.cs、LandformSettings.cs、LandformSaveCompatibility.cs、WorldParameterUpdate.cs | 第三版最陡坡出流与平坦区破环、面积加权汇流、共享曲线与断面、先雕盆地再按溢出口蓄湖；31 项原生水文性质与 8 组旧版逐位基准；旧世界必须显式备份升级 |
| 2026-11-17 | 海中身体浸水但中心露出水面时不扣血、无状态；刚入水或离水时提示滞后 | 旧溺水只比较实体中心和水面；状态沿用上一伤害帧；静止共享与修复也需实时排除入水者 | EntityEnvironmentHazard.cs、EntityVitals.cs、EntityWorkScheduler.cs、MutationMovementRegression.cs | 按身体碰撞体与真实地表水深计算浸没；9 项原生运行检查覆盖深水、浅水、暂停、离水和实际详情文字；状态查询只读，不在暂停扣血 |
| 2026-11-17 | 两文件核心重复启动失败 | Windows PowerShell 将 File.Replace 的 null 备份参数转换为空路径；无 BOM 的非 ASCII 脚本被旧版 PowerShell 按系统编码读取 | Server/Bootstrap.ps1、Server/PortableStart.cmd、ServerCorePackaging.cs、test_bootstrap.py | 用明确备份路径原子切换；混合入口显式 UTF-8 读取，独立引导脚本保持 ASCII 兼容；10 项启动器测试及真实中文空格目录连续两轮开服/保存/恢复 |
| YYYY-MM-DD | 示例：玩家数据无法保存 | `SavePlayer` 只有注释，未写盘 | `SaveSystem.cs` | 回归检查 2：Save 必须写盘 |
| YYYY-MM-DD | 示例：UI 打开后鼠标仍被锁定 | 输入脚本每帧强制锁鼠标 | `PlayerController.cs` | 覆盖 UI 打开时统一屏蔽输入 |
| YYYY-MM-DD | 示例：组件报 Broken PPtr | 引用 ID 与组件块 ID 不一致 | `Scene.unity` | 加引用前确认组件块真实存在 |
| YYYY-MM-DD | 示例：新增脚本编译报 CS0103 | 手写 meta GUID 位数错误 | `Xxx.cs.meta` | 用 GUID 生成器并校验 32 位 |
| 2026-10-25 | 捕食收入低于行动代谢且饱食溢出丢失 | 伤害营养倍率过低、运动消耗过大，进食直接夹到饱食上限 | LocomotionSettings.cs、locomotion_settings.json、LocomotionGeneLogic.cs、EntityStats.cs、EntityVitals.cs | 实际伤害结算、溢出储存；检查跑动代谢后的净收益与过量伤害 |
| 2026-10-25 | 实体标题冗长、参数面板留白过多 | 只读标题及旧版固定布局 | GeneInspectorUI.cs、EntityFavoritesManager.cs、PlanetParameterEditor.cs | 紧凑名称输入、空值回退、收藏同步；检查输入与滑条不重叠 |
| 2026-10-25 | 新增手动操控可能与 AI/轨道相机争抢输入 | 原行为与镜头没有控制权切换接口 | EntityViewController.cs、EntityManualControl.cs、OrbitCamera.cs、SimulationPauseHotkey.cs | 进入/退出成对恢复；失效基因、暂停、死亡和 UI 焦点回归 |
| 2026-10-24 | 大陆沿海生成大面积沙漠 | 气候图只用高度近似湿度，纬度用像素 y 线性近似，且未计算海岸距离 | `PlanetClimateMap.cs`、`PlanetClimateSystem.cs`、`PlanetClimateConfig.cs` | 湿度 = 纬度带 + 海岸距离 + 内陆干燥度；沿海设置湿度下限 |
| 2026-10-24 | 气候图层全白 | 星球未挂 `PlanetClimateMap`，Shader 采样默认灰图；温度编码范围两端不一致 | `SampleScene.unity`、`PlanetClimateMap.cs`、`PlanetURP.shader` | 场景挂载气候图组件；`_ClimateReady` 未就绪时程序化兜底；编码范围由配置下发 |
| 2026-10-24 | 地形几乎看不到山脉、盆地 | 脊状噪声 `1-|2m-1|` 对负值平方放大；盆地只有窄带；侵蚀配置未接入 | `PlanetTerrainGenerator.cs`、`PlanetTerrainMeshBuilder.cs` | 分层雕刻 + 浸蚀 + 6 面接缝顶点平均；所有雕刻幅度配置化 |
| 2026-10-24 | 空格键无法暂停 | 没有全局暂停热键；且输入框焦点时空格应属于输入框 | `SimulationPauseHotkey.cs`、`SampleScene.unity` | 独立热键组件；输入框聚焦时短路 |
| 2026-10-24 | 收藏栏右键定位不准 | `OrbitCamera.FocusOn` 把相机放到目标反方向，固定距离 180 小于星球半径 300 | `OrbitCamera.cs`、`EntityFavoritesManager.cs` | 沿实体球面法线外侧放置相机，距离 = 地表半径 + focusAltitude |
| 2026-10-24 | 收藏后实体不高亮 | URP 附加点光源受限，点光几乎不可见 | `EntityFavoriteHighlight.cs`、`EntityFavoritesManager.cs` | 用无光照 LineRenderer 环形描边 + 脉动 |
| 2026-10-24 | 实体没有温度/溺水伤害，基因无适应方向 | 环境伤害分散且水位使用 `seaLevelRatio` 估算 | `EntityEnvironmentHazard.cs`、`EntityVitals.cs`、`PlanetGravity.cs`、`gene_environment.json`、`environment_default.json` | 环境伤害独立组件；真实水位 `Data.SeaLevel`；阈值/伤害/状态 id 全配置化 |
| 2026-10-24 | 有性繁殖永远找不到配偶 | `FindMate` 查找生长基因的 `"adult"` 状态，实际状态 id 是 fast/slow_growth | `ReproductionGeneLogic.cs` | 成年判断改用 `CurrentAge == AgeState.Adult` |
| 2026-10-24 | 气候图层出现色块断层 | 湿度按纬度带插值，Shader 用 if/else 硬分类；纹理按线性数据采样 | `PlanetClimateConfig.cs`、`PlanetClimateMap.cs`、`PlanetURP.shader` | 去除纬度湿度带，改为海岸/风/地形/噪声条件；CPU 平滑混色；颜色贴图用 sRGB |
| 2026-10-24 | 山脉看起来没有生成 | 水力侵蚀 `sqrt(speed² + deltaH)` 对下坡负值开方产生 NaN，污染高度图；气候图层又无光照 | `HydraulicErosion.cs`、`PlanetTerrainGenerator.cs`、`PlanetURP.shader` | 侵蚀速度改用 `sqrt(max(0, ...))` 并过滤 NaN；数据图层叠加山体阴影；日志输出高度范围 |
| 2026-10-24 | 有行动基因的实体攻击时飞上天 | 追击力直接朝目标施加，带向上分量；重力随高度衰减后逃逸 | `LocomotionGeneLogic.cs` | 只用球面切向追击力，并清除向外速度 |
| 2026-10-24 | 无法适应环境时缺少选择压力 | 环境伤害与基因系统没有联动 | `EntityEnvironmentHazard.cs`、`EntityGeneController.cs`、`GeneValueResolver.cs`、`GeneMutator.cs`、`environment_default.json` | 压力超过阈值时统一抑制所有基因，并优先突变环境适应基因 |
| 2026-10-24 | 底部控制栏偶发消失/移出屏幕 | Canvas/布局在特定 Game View 尺寸下未刷新，且可能被其他 Canvas 覆盖 | `SimulationControlPanel.cs`、`UIRoot.cs` | 主 Canvas 固定排序 100；底部栏幂等校正锚点/尺寸/屏幕位置并输出构建日志 |
| 2026-10-24 | 山脉只有 0.72 高度、雪地占陆地 30% | 侵蚀仍过强；极地温度曲线和 tundra 阈值过宽 | `PlanetTerrainConfig.cs`、`PlanetClimateConfig.cs`、`SampleScene.unity` | 水力侵蚀降到 40 次/0.05；山高 0.38；纬度指数 0.8、极地 -22°C、tundra -8°C |
| 2026-10-24 | 气候图层仍有一条笔直断层 | Shader 在顶点里算等距柱状 UV，跨反经线（x≈0,z<0）三角形把 u 从 1 插到 0，整张气候图被压缩进一条窄带 | `PlanetURP.shader` | UV 必须在片元里按物体空间方向逐像素计算；纹理 U 用 Repeat |
| 2026-10-24 | 气候图层还有扇形/等距色块 | 湿度用“最近海洋方向”判断迎风，最近海洋来源在等距边界跳变；海岸湿度下限用硬 if 阈值 | `PlanetClimateModel.cs`（新增）、`PlanetClimateMap.cs`、`PlanetClimateConfig.cs` | 改用上风方海洋距离（风程）+ 迎风抬升 + 背风雨影 + 连续权重；禁止硬阈值改整片湿度 |
| 2026-10-24 | 山脉依旧几乎看不见 | 单位球方向噪声频率仅 1.25，山脊 `1-|FBM|` 均值低；高度上限写死 0.9、heightScale 45，峰值离海面仅约 10 米 | `PlanetTerrainGenerator.cs`、`PlanetTerrainMeshBuilder.cs`、`PlanetTerrainConfig.cs`、`PlanetURP.shader`、`SampleScene.unity` | 山脊零等值线窄带 + 频率 4.2 + heightScale 90 + 统一 MaxNormalizedHeight=1.0 |
| 2026-10-24 | 沿海大片“沙漠” | 默认地形 Shader 把海平面 +0.02 内整条带子硬刷沙色 | `PlanetURP.shader` | 沙滩改为 0.006 内的平滑窄带；气候图沙漠权重与 Classify 阈值对齐 |
| 2026-10-24 | 有行动基因的实体攻击时卡在天上 | 追击函数先判断目标是否在正上方再清向外速度，目标在头顶时提前 return 跳过清理；引力随高度平方衰减 | `LocomotionGeneLogic.cs`、`PlanetGravity.cs`、`EntitySurfaceAnchor.cs`（新增）、`EntityFactory.cs` | 清向外速度放到所有提前返回之前；引力设下限；新增地表硬约束/高空救援 |
| 2026-10-24 | 环境压力把适应基因也抑制，越冷越弱 | `GeneValueResolver` 对所有基因统一扣抑制值，包括环境适应基因；压力来自溺水时也触发 | `GeneValueResolver.cs`、`EntityGeneController.cs`、`EntityEnvironmentHazard.cs`、`EnvironmentConfig.cs`、`environment_default.json` | 适应基因豁免；只按“适应缺口”缩放；溺水不触发温度适应压力；突变总概率随压力提高 |
| 2026-10-24 | 底部控制栏消失 | Start 直接取 UIRoot，UIRoot 未就绪就永久 return；无图标时按钮是隐形深色块 | `SimulationControlPanel.cs`、`UIRoot.cs`、`SampleScene.unity` | 协程等待 UIRoot；主 Canvas 排序 200；每帧幂等校正；销毁自动重建；无图标时文字兜底 |

| 2026-10-24 | UI 看似消失、球面六面边界断层、海陆层次和大气不符合参考图 | Game 视图 2× 裁切了底栏；各面独立侵蚀造成边缘坡度差，重复顶点拓扑；海床被当成海面渲染；旧大气壳过厚且没有云层 | `PlanetMeshTopology.cs`、`PlanetMountainField.cs`、`PlanetTerrainGenerator.cs`、`PlanetTerrainConfig.cs`、`PlanetTerrainMeshBuilder.cs`、`PlanetWeatherVisuals.cs`、`PlanetOcean.shader`、`PlanetCloudVolume.shader`、`PlanetScattering.shader`、`PlanetURP.shader`、`PlanetSurfaceDetail.hlsl`、`PlanetVisuals.cs`、`UIRoot.cs`、`PlanetVisualRegression.cs`、`SampleScene.unity` | Game 视图恢复 1×；侵蚀边界缓冲与共享顶点焊接；球面每条边恰好关联两个三角形；独立海面、体积云和指数密度散射层；三维细节无需 UV；真实播放检查 HUD、缺失脚本与 Shader |

| 2026-10-24 | 旧方块矿石仍生成、默认生成单击无效、收藏列表空白及基因面板交互无效 | 新 PlanetOreSystem 未挂场景而旧 OreGenerator 仍启用；投放只实现拖拽；首次收藏未刷新；基因面板挂错误 Canvas、标题行无射线命中面、右键穿透 UI、底栏每帧抢层级 | `SampleScene.unity`、`PlanetOreSystem.cs`、`EntityPlacementTool.cs`、`EntityPlacementUtility.cs`、`EntitySpawnerUI.cs`、`EntitySpawnPanel.cs`、`EntityFavoritesManager.cs`、`GeneInspectorUI.cs`、`SimulationControlPanel.cs`、`OrbitCamera.cs`、`EntityUIRegression.cs` | 场景停用旧生成器并显式挂载矿脉系统；单击/拖拽共用真实地形拾取；预览仅复制渲染外观；确认时复制模板基因；首次刷新收藏空状态；屏幕面板统一 MainCanvas，UI 输入和射线命中区域成对验证 |

| 2026-10-25 | 种子菜单只有 0.0、镜头无法近看、云气壳出现直线缺块 | 种子无处理分支；最近距离固定 430；用变形角点决定整面绕序，天气壳没有真实地表深度截断 | `PlanetParameterEditor.cs`、`SimulationControlPanel.cs`、`OrbitCamera.cs`、`PlanetTerrainGenerator.cs`、`PlanetWeatherVisuals.cs`、天气与地表 Shader、`SampleScene.unity` | 参数草稿用 Shift 提交；种子整数校验；按球面确定绕序并逐三角校验；深度积分及近看自动关云 |
| 2026-10-25 | 捕食者不追猎、倍率只加速日期 | 捕猎排除不动活物且无猎物直接停机；实体 Update 与突变协程使用真实秒数 | `LocomotionGeneLogic.cs`、`LocomotionSettings.cs`、`EntityGeneController.cs`、`SimulationClock.cs`、`EntityVitals.cs`、`EntityEnvironmentHazard.cs`、`NutritionGeneLogic.cs`、`GrowthGeneLogic.cs` | 最近活物目标、碰撞体接触距离、无猎物搜索；统一模拟步长，按模拟时间突变，阶段变化重算属性；1×/16×等模拟时长回归 |
| 2026-10-25 | 近景地表模糊、实体视角键鼠不符合操控需求 | 几何与气候采样不足，只有低频颜色；旧控制未锁鼠标且空格攻击 | PlanetTerrainMeshBuilder.cs、PlanetClimateMap.cs、PlanetURP.shader、PlanetNearSurfaceDetail.hlsl、EntityPossessionInput.cs、EntityJumpMotor.cs、EntityViewController.cs、EntityManualControl.cs、SampleScene.unity | 三维近景细节与导数抗闪烁、焊接球面回归；成对恢复鼠标；着地跳跃保留合法上升、防连跳；捕食先补饱食后储存 |
| 2026-10-25 | 进入控制时镜头跳转、曲面接触可能穿透 | 鼠标居中位移被读取为转向，切向速度重写可能打断物理接触修正 | EntityPossessionInput.cs、EntityJumpMotor.cs | 捕获前两帧不读鼠标位移；真实 MeshCollider 脚底接触兜底，正常跳跃仍保留 |
| 2026-10-25 | 提高细分后部分区域无法投放、最近镜头穿入星球，六面交界仍有直线折痕 | 微小叉积 normalized 归零导致三个面绕序反转；相机只限海床且聚焦走直线；侵蚀按面独立处理坡度、拖拽读旧 UI 缓存 | PlanetTerrainGenerator.cs、PlanetSeamContinuity.cs、PlanetTerrainConfig.cs、OrbitCamera.cs、PlanetWeatherVisuals.cs、EntityPlacementUtility.cs、EntityPlacementTool.cs、EntitySpawnerUI.cs、SampleScene.unity | 用原始叉积判绕序并逐三角验证；96 方向近景与正面拾取；真实海面边界及球外圆弧；球面跨面坡度处理；指针 -1 过期 UI 缓存与点击投放回归 |
| 2026-10-25 | Esc 后镜头留在旧位置找不到实体；无游泳能力也能在水中移动且不掉血 | 退出恢复进入前镜头且轨道缓存未同步；通用行动在水中推进、重力无条件乘 0.1，浸没阈值偏深且自然修复抵消伤害 | OrbitCamera.cs、EntityViewController.cs、EntityManualControl.cs、EntityJumpMotor.cs、LocomotionGeneLogic.cs、PlanetGravity.cs、EntityEnvironmentHazard.cs、EntityVitals.cs、EnvironmentConfig.cs、environment_default.json | 退出对准当前位置并推进后续轨道帧验证；AI/手动共同限制水中行动；浅深水扣血、暂停/离水及自然修复检查；阈值和最低严重度配置化 |
| 2026-10-25 | 滑条松手关窗导致 Shift 无法应用；放置一次就退出；信息长期不变、添加窗残留；攻击被自身遮挡并锁定旧目标 | 覆盖层 Button 收到冒泡点击；投放成功即取消；只在打开时构建文本且关闭路径分散；手动复用 AI 目标缓存 | SimulationControlPanel.cs、PlanetParameterEditor.cs、OrbitCamera.cs、EntityPlacementTool.cs、EntitySpawnerUI.cs、EntityPlacementUtility.cs、PlacementQuantityPopup.cs、GeneInspectorUI.cs、EntityViewController.cs、EntityPossessionInput.cs、EntityManualControl.cs、EntityAttackAim.cs、LocomotionGeneLogic.cs | 草稿拖动与应用/取消回归；连续批次及独立基因副本；实时刷新不丢焦点与滚动位置；EventTrigger 转交滚轮；准星 A/B 切换、排除自身、落空和遮挡检查 |
> 台账是团队资产：修复前先检索是否有同类历史记录，修复后必须补登一行。

---


## 规则 5：新增代码必须新建独立脚本（禁止在旧脚本上堆叠新功能）

- 新增功能 / 新系统 / 新逻辑（新的交互、渲染辅助、管理类、服务类等）**必须新建独立 `.cs` 文件**，禁止叠加进旧脚本。
- 旧脚本只允许做两类改动：
  1. **修复 Bug**：按规则 2 标注 `★ 修复（防Bug回归）：原因`；
  2. **最小接口适配**：调用新脚本暴露的静态方法/事件等，必须加 `★ 适配：用途` 注释。
- 判断标准：如果改动是「给旧脚本新增功能分支 / 新方法 / 新状态」，应拆到新脚本；只有「改正旧脚本本身的错误逻辑」才允许直接改旧脚本。
- 新脚本要求：职责单一、命名清晰、注释齐全（规则 2）、数据驱动（规则 1）。
- 若新脚本是 MonoBehaviour 且需要挂到场景，必须按回归检查 5 加入对应 GameObject 的 `m_Component` 列表，否则组件不会执行。
- **为什么**：旧脚本是稳定核心（渲染/存档/交互/世界生成），反复叠加改动容易引入回归；独立脚本可单独挂载、单独回滚，也便于与旧版本对照排查差异。

---

## 附录：Unity 通用高频陷阱速查

以下为从历史 Bug 中提炼的通用教训，编码时优先规避：

| 领域 | 陷阱 | 规避方式 |
| --- | --- | --- |
| 资源导入 | 贴图尺寸与图集打包尺寸不一致，`SetPixels` 抛异常被空 catch 吞掉 | 图集统一缩放；禁止空 catch 吞错误 |
| 资源命名 | 文件名大小写不一致导致加载失败 | JSON/配置引用的资源名必须与实际文件逐字符一致 |
| 材质 | 共享材质被误改为透明，导致渲染排序错乱 | 不透明材质保持 Opaque；逐对象差异用 MPB/顶点色 |
| 光照 | 运行时改 `RenderSettings` 后未调用 `DynamicGI.UpdateEnvironment()` | 光照变更后刷新 GI；天空盒必须有纯色兜底 |
| 输入 | 多个脚本独立读取 `Input`，UI 打开时互相冲突 | 统一输入门控（如全局 `IsUIBlocking`） |
| UI 布局 | `VerticalLayoutGroup` 子项未声明 `LayoutElement`，高度被压成 0 | 布局组子物体必须声明首选高度；列表用 `RectMask2D` 裁剪 |
| UI 射线 | 提示/悬浮面板参与射线导致闪烁 | 提示面板与文字关闭 `raycastTarget` |
| 槽位索引 | 外部槽位用 -1 初始化时访问 `slots[-1]` | 访问前先判断索引非负 |
| 销毁时机 | `Destroy` 延迟导致旧对象参与本帧布局/刷新 | 先隐藏/移出父节点，再销毁；下一帧强制刷新 |
| 实体生成 | 区块/地形未就绪时实体化，导致坠入虚空 | 生成前确认区块已加载且脚下有实心地面 |
| 场景切换 | 切换场景不触发 `OnApplicationQuit`，静态状态残留 | 切场景前显式保存并清理静态状态 |
| 静态缓存 | 路径/配置静态缓存后设置变更不生效 | 设置保存后刷新缓存；非法路径回退默认值 |

---

## 交付前最终确认

- [ ] 遵守规则 1：无硬编码内容，ID/资源三方一致，加载失败有告警
- [ ] 遵守规则 2：改动有 ★ 注释，说明「为什么」，文件 UTF-8 无 BOM
- [ ] 遵守规则 3：七项回归检查逐条自查通过
- [ ] 遵守规则 4：新修复的 Bug 已登记台账
- [ ] 遵守规则 5：新功能为独立脚本，旧脚本仅修复或最小适配
- [ ] 编译 0 error、0 warning 新增，场景组件挂载确认无误
| 2026-10-24 | 多轮修复后气候/地形/控制栏/攻击悬空仍有回归 | 山脉按理论噪声范围归一化；大种子逐层倍频丢精度；像元中心被强行复制；HUD 超时放弃且根节点重启未注册；移动削弱下落速度，追击力按渲染帧累积；适应基因被错误豁免 | `PlanetTerrainGenerator.cs`、`FBMNoise.cs`、`PlanetClimateModel.cs`、`PlanetClimateConfig.cs`、`PlanetClimateSystem.cs`、`PlanetURP.shader`、`SimulationControlPanel.cs`、`UIRoot.cs`、`LocomotionGeneLogic.cs`、`EntitySurfaceAnchor.cs`、`EntityGeneController.cs`、`EntityEnvironmentHazard.cs`、`GeneValueResolver.cs`、`EnvironmentConfig.cs`、`SampleScene.unity` | 实测噪声范围；所有基因环境抑制含面板显示；温度由多条件叠加；真实气候决定雪地；HUD 持续重试；保留下落速度并限制追击速度；按用户本次要求取消适应基因豁免 |

| 2026-10-26 | 控制实体短点击攻击丢失、老年不死、阶段外变异及幼体重叠 | 攻击仅查持续按住且冷却消费请求；寿命依赖可失效的生长逻辑；定时突变与三维出生偏移 | EntityPossessionInput.cs、LocomotionGeneLogic.cs、EntityLifeCycle.cs、EntityGeneController.cs、GeneMutator.cs、OffspringPlacement.cs、ReproductionGeneLogic.cs、GeneInspectorUI.cs | 短点击锁存至冷却结束，失焦/退出清除；独立寿终；阶段去重抽签；90/5/5 配置；真实地表随机散布；7～30 游戏日；添加窗再次点击/Esc 关闭 |

| 2026-10-26 | 偏肩俯视时准星偏离近身目标；新增覆盖菜单存在游戏输入穿透风险 | 旧镜头以实体前方为注视点且仍允许滚轮拉远，菜单没有统一接管游戏输入 | EntityFirstPersonCamera.cs、EntityViewController.cs、EntityPossessionInput.cs、GameInputGate.cs、GameMainMenu.cs | 锁定眼位、隐藏自身且退出恢复、最终视线与攻击射线同源；覆盖菜单暂停模拟并关闭 HUD 射线；实际近身攻击回归通过 |
| 2026-10-26 | 基因树编辑与运行配置可能脱节、切换节点丢失数值 | 原系统仅读 Resources，没有布局/关系持久化与表单提交路径 | GeneTreeStorage.cs、GeneTreeWorkbench.cs、GeneValueForm.cs、GeneDatabase.cs、GeneMutator.cs | JSON 与布局关系同目录保存；保存/切换时提交数值；重复 id、悬空关系、进化环与非法数值拦截；四类关系运行映射和文件往返验收 |

| 2026-10-26 | 主菜单直接进入临时世界、F10/操控退出与暂停冲突、加载耗时计入日期 | 主菜单没有世界会话入口，启动同步生成且没有存档读写闭环；Esc 被多处消费 | GameMainMenu.cs、WorldSessionManager.cs、WorldSaveStore.cs、MainMenuBackground.cs、GamePaths.cs、SimulationClock.cs、EntityViewController.cs、SampleScene.unity | 菜单提前门控生成；数字/文字种子与默认现实时间命名；save 原子写入/备份及完整读档；mods/Default 默认工具目录；Esc 暂停三按钮、F10 无动作、Tab 退出操控；后台生成/阶段进度、加载后下一帧开始计时；实机存档往返、坏档保护与预录背景播放检查 |

| 2026-10-27 | 基因出错换类型后仍执行旧逻辑；社会行为与跨实体关系缺少持久化约束 | 行为表只按实例引用判断，复制行为重复写入；宿主/群体使用临时对象无法跨读档关联 | EntityGeneController.cs、GeneBehaviorAuthority.cs、GeneEvolutionRules.cs、Entities/Ecology、WorldSessionManager.cs、WorldSaveStore.cs | 换定义时重建匹配逻辑；每类行为单写入者；领地球面本地坐标；真实受击与环境伤害分离；群体稳定 ID/加入顺序/权限/决斗冷却；宿主独立跟随及碰撞恢复；两遍读档解析引用；矿量/营养与孵化计时往返回归 |

| 2026-10-27 | Esc 暂停页没有返回主界面 | 只有退出程序分支，没有会话卸载链路 | GameMainMenu.cs、WorldSessionManager.cs、EntityFavoritesManager.cs | 保存成功后才清场；失败保留世界；清理操控、收藏、实体和基因快照，重复进出回归 |
| 2026-10-27 | 基因树无法缩小，大树被裁切 | 固定 1:1 画布坐标及滚动范围 | GeneGraphViewport.cs、GeneTreeWorkbench.cs | 缩放不写入节点；绘制与命中坐标一致；鼠标锚点、适应全图及拖动实机验证 |
| 2026-10-27 | 所有世界散放在 save 根目录 | 存档路径直接为标识.json | WorldSaveStore.cs | 每世界独立目录；兼容旧档和列表去重；新文件成功落盘后再收纳旧档及备份 |
| 2026-10-27 | 旧世界添加基因页只有七项 | 存档的基因库快照屏蔽新增定义 | GeneCatalogExpansion.cs、WorldSessionManager.cs、GeneInspectorUI.cs | 仅补缺失定义及相关关系；保留同名旧配置；新增条目可滚动、可点击并随世界保存 |
| 2026-10-27 | 领地实体持续巡逻，饥饿和发现捕猎者仍不能切换 | 领地意图无条件返回并截断行动器，基因倾向与实体活动混用 | EntityStateRegistry.cs、GeneEntityStateTags.cs、EntityStateController.cs、EntityStateMotion.cs、TerritoryBehavior.cs、EcologyMotion.cs、GeneValueForm.cs、EntityStateSaveValidation.cs | 标签授予独立状态；条件、阈值、迁移全配置化；严格 >5 边界；真实捕猎攻击、失效/死亡/范围外过滤；改 JSON 为 9 验证无写死；注册表与实体状态写读成对 |
| 2026-10-28 | 生态无法多代循环，小体型仍可捕食大实体 | 孕育绕过有效基因表达、冷却过长；营养生产重复且溢出丢弃；开销与收益失衡；目标和最终咬合缺少体型限制；旧长孕育在恢复生长后先老死 | EcologyBalanceSettings.cs、EcologyEnergyBudget.cs、EcologyReproductionTiming.cs、EcologyGeneDefaults.cs、PredationSizePolicy.cs、EcologyLegacySaveUpgrade.cs、EcologyBalanceRegression.cs、相关基因 JSON | 生产单入口和溢出入库、资源竞争、有效孵化速度、基因相互促进抑制、真实模拟速度代谢、储备感知觅食；各捕食入口严格比较实际 size；配置快照成对保存恢复、旧计时一次性迁移、多代隔离实验；邻域先筛距离再查基因避免长时间运行开销过大 |

| 2026-10-28 | UI 与样图外观不一致；高 DPI 滑条混入旧皮肤；补图标后图层可能换位 | IMGUI/uGUI 分别维护外观，继承系统 scaledBackgrounds，图层顺序依赖资源扫描 | UIVisualTheme.cs、RuntimeMenuTheme.cs、UIFactory.cs、EntitySummaryView.cs、相关 UI 脚本、UI/theme.json | 统一主题与独立透明图标；清理高 DPI 背景；保留面板锚点及外层矩形；显式图层顺序；真实数值/上限刷新、射线与存档回归 |
| 2026-10-29 | 普通巡逻持续主动攻击、蚁群卵孵化时退群、士兵不到补给点、种群色环全白 | 巡逻回退普通捕食；将孵化行动限制混同血缘身份；缺少共享补给导航；URP Unlit 不读取线条顶点色 | PatrolRhythm.cs、EcologyMotion.cs、PopulationBehavior.cs、AntColonyMember.cs、ColonyAppearance.cs、AntColonyRegression.cs | 巡逻窗口与休息、真实受击反击、孵化期间保留同群免伤、零击杀取食奖励、运输中断释放、独立材质属性块、独立世界运输/孕育/逆序读档回归 |
| 2026-10-29 | 士兵不主动追击王后范围内的猎物 | 捕猎复用了未初始化或以出生位置为中心的个人领地边界 | EcologyMotion.cs、AntColonyRegression.cs | 自主扫描与追击使用王后种群中心和范围；保留感知、体型与同群免伤检查 |
| 2026-10-30 | 实体页基因图标挤压、失效基因显示其他基因属性、角色状态冗长、王后可被底层决斗入口挑战、行动低速不移动、体型无法约束实际跳跃 | 行高度不足且按钮区挤占；提示框读取实体总属性；行动谱系重复累加曲线；仅界面禁用挑战；高摩擦抵消小步速度；跳跃固定冲量绕过基因 | GeneInspectorUI.cs、EntitySummaryView.cs、ColonyStatusPresentation.cs、PopulationBehavior.cs、GeneAttributeInheritance.cs、GeneValueResolver.cs、EntityAttributeProcessor.cs、PlanetGravity.cs、EntityMovementLimits.cs、EntityJumpMotor.cs、EntityPanelMotionRegression.cs | 独立图标和数值行、只列本基因贡献、共有行动能力继承一次、挑战入口禁用蚁群、真实物理步验证移动、AI/手动与实际起跳共用体型上限；验证使用隔离项目不打断正在使用的编辑器 |
| 2026-10-30 | 少量种群即卡顿，多群后无法推进 | 属性热路径反复排序种群表达、全局搜索王后和运载物；蚁群重复执行被分工覆盖的普通状态感知；每帧重建色环和模拟列表 | PopulationGroupIndex.cs、PopulationBehavior.cs、AntColonyMember.cs、AntColonyWorker.cs、EcologyIdentity.cs、EntityStateController.cs、EntityGeneController.cs、GeneBehaviorAuthority.cs、ColonyMemberMarker.cs、PopulationPerformanceRegression.cs | 关系与 ID 索引；出生/死亡/角色/同帧表达/读档失效；搬运释放和途中销毁清理；距离先筛；10/100 成员与多群对比、10 成员 16× 实际时钟、100 狼群、完整玩法回归 |

| 2026-10-31 | 蚁群附近有能量块仍饿死；保存实体模板重进游戏消失 | 分工寻路与进食条件分离，工匠运输和士兵追猎长期抢占补给，王后/幼体只追随中心；模板只有静态内存列表 | ColonyFeeding.cs、ColonyResource.cs、AntColonyMember.cs、AntColonyWorker.cs、EntityTemplateStore.cs、EntityTemplateManager.cs、ColonyFeedingTemplateRegression.cs、AntColonyRegression.cs | 进食与寻路共用资格；带货补给后继续任务且不复制；受抑营养基因连续120秒代谢/补给；真正重启第二进程恢复模板；损坏主文件保留有效备份；全套运输/繁殖/逆序读档与规模性能回归 |

| 2026-10-31 | 王后和幼体跟随却不吸收现有黄色块；操控时无法启停或添加补给 | 问题存档9个育幼块属于已灭绝旧群，新群被严格归属拒绝；先到幼体会吃光减速王后的最后口粮；空格冲突导致操控时禁用全部暂停，锁鼠标也阻断按钮 | ColonyResourceOwnership.cs、PopulationGroupIndex.cs、ColonyFeeding.cs、ColonyResource.cs、SimulationPauseHotkey.cs、EntityPossessionInput.cs、EntityViewActions.cs、ColonyResourceControls.cs、EntityViewController.cs、GeneInspectorUI.cs、SimulationControlPanel.cs | 用真实问题存档完整地形/实际物理步验收移动及能量块扣减；废弃回收不抢存活外群；只在实际进食领取并保存归属；幼体留最低王后口粮；P启停/Alt鼠标/E补给与按钮、输入门控、读档回归；百成员性能保持通过 |

| 2026-10-31 | 操控补给UI不符合要求，E可无成本生成能量，Alt松开后仍释放鼠标 | 旧补给工具直接创建资源且使用切换式Alt，能量块能力依附蚁群而非独立基因 | EntityViewController.cs、EntityPossessionInput.cs、EnergyBlockInput.cs、EnergyBlockMetabolism.cs、EnergyBlockGeneLogic.cs、NutrientUtilizationSettings.cs、GeneLogicRegistry.cs、ColonyResource.cs、ColonyFeeding.cs、ColonyResourceOwnership.cs、EcologyMotion.cs、gene_nutrient_utilization.json、nutrient_utilization.json、default_tree.json | 删除新增UI与E入口；Alt按住/松开语义；Q必须有效基因与足够营养且严格扣账；禁用/抑制/暂停/输入门控；新基因普通实体真实寻路和自动吸收守恒；旧存档增量补库与新资源读档；蚁群不重复吸收、原专用补给约束及百成员回归 |

| 2026-10-31 | 实体面板偏窄偏长；营养利用来源有误；缺少全星球快速投放入口 | 固定420×700尺寸会每帧覆盖；上一版错误连到营养方式正常演化；原生成仅支持选定落点 | GeneInspectorUI.cs、gene_nutrient_utilization.json、default_tree.json、expansion_rules.json、NutrientUtilizationEvolutionUpgrade.cs、nutrient_utilization_upgrade.json、GeneTreeStorage.cs、WorldSaveStore.cs、RandomSpawnPanel.cs、RandomPlanetSpawner.cs、EntitySpawnPanel.cs、LayoutRandomSpawnRegression.cs | 520×560及边界/滚动；限定储存出错且真实变异；旧入口迁移保留自定义数据并成对保存父定义/连线；真实UI默认与模板投放、球面分布/地表高度、独立基因、取消/菜单/页面生命周期和有界失败；分帧及百成员性能通过 |
| 2026-10-31 | 随机生成栏和主菜单横向过长；多人入口不可用且本地编辑没有权限边界 | 原界面固定宽度；没有唯一服务端、实体占用和成员指令验证；仅隐藏UI不能保护世界 | WorldAuthority.cs、LanSession.cs、LanTransport.cs、LanReplicaActor.cs、LanEditActions.cs、LanLobbyUI.cs、WorldSessionManager.cs、相关编辑入口、RandomSpawnPanel.cs、GameMainMenu.cs、RuntimeMenuTheme.cs、LanMultiplayerRegression.cs | 随机栏240、主菜单320/按钮58；同一程序房主/成员、唯一管理员、作弊撤销；输入身份来自连接而非实体ID；普通成员不能修改/放置；客户端不保存副本；真实双进程恶意报文、移动、基因增改、随机生成、启停和读写验证 |
| 2026-10-31 | 新增RTS可能越权指挥外群或点击命令时清空选区；联机关闭、连接取消和省略矿图可能遗留世界或创建0尺寸纹理 | 缺少角色/基因/占用检查，IMGUI不参与EventSystem命中；异步连接和世界加载未配对取消，JsonUtility把省略数组恢复为空数组 | RtsOrderController.cs、RtsSelectionInput.cs、EcologyMotion.cs、EcologySaveState.cs、LanSession.cs、LayoutRandomSpawnRegression.cs、LanMultiplayerRegression.cs | 指令服务端按王后所属群及占用二次校验；工匠采集复用独占运输；RTS状态保存恢复；命令栏独立命中；取消连接代次隔离，加载中断清场且不写盘；矿图分辨率及数组长度共同验证；断线确认后才重连避免测试竞态 |
| 2026-10-31 | 编辑器编译通过但Windows发布构建CS1061失败 | RuntimeMenuTheme直接引用仅编辑器可用的GUIStyleState.scaledBackgrounds；先前只验证编辑器程序集，未实际构建Player | RuntimeMenuTheme.cs、PlayerBuildLogRegression.cs | 专用API放在UNITY_EDITOR条件内，保留编辑器高DPI修复；回归必须真实构建非开发版Windows Player并启动，不以dotnet编辑器编译代替 |
| 2026-10-31 | 发布版测试缺少可定位、可保存的输出；高频日志同步写盘可能卡顿 | 只有Debug和分类日志，没有统一跨线程持久化、会话及文件容量管理 | RuntimeFileLog.cs、RuntimeLogLifecycle.cs、SessionLogWriter.cs、RuntimeLogSettings.cs、runtime_logging.json、WorldSessionManager.cs、LanSession.cs、RandomPlanetSpawner.cs、GameMainMenu.cs、PlayerBuildLogRegression.cs | 场景Awake前订阅线程日志、独立后台有界队列、定时与错误刷新；退出成对注销排空、轮转和保留上限、只读目录回退；中文/线程/堆栈/实际文件/关闭解锁/重复初始化回归；实时读取端必须共享写，避免Mono默认ReadAllText误报文件锁 |
| 2026-10-31 | 构建成功后太阳/星空启动Material(null)异常；地表Shader有负底数pow告警 | Shader.Find字符串引用不阻止发布裁剪；浮点归一化纬度可能微小越界 | Resources/RuntimeMaterials/runtime_unlit.mat、runtime_particles_unlit.mat、runtime_energy.mat、PlanetURP.shader、PlayerBuildLogRegression.cs | 通过Resources材质显式保留URP着色器及发光变体；pow底数saturate；实际WindowsPlayer启动日志无错误，不以构建Succeeded代替运行验收 |
| 2026-11-01 | RTS压住权能栏，缺少全地图数量/基因筛选和实际FPS；新增FPS可能遮挡实体面板 | RTS用固定屏幕像素推算Canvas高度；没有独立只读地图统计；模拟时间不能作为渲染帧率口径 | RtsSelectionInput.cs、SimulationControlPanel.cs、MapEntityStatisticsUI.cs、FrameRateDisplay.cs、GeneInspectorUI.cs、GameMainMenu.cs、GameplayHudRegression.cs | 真实底栏边界与命中矩形成对；0.7/1/1.4缩放不重叠；注册表统计存活个体、复制基因去重、携带/生效分开、成员可筛选；定时刷新与稳定布局不标脏；unscaled时间采样、暂停/16倍验证、顶部避让；出生/死亡/禁用表达和退出归零真实回归 |
| 2026-11-01 | 地图统计框横向过长 | 以左右双锚点填满右侧全部剩余宽度 | MapEntityStatisticsUI.cs、GameplayHudRegression.cs | 单侧锚定、preferredWidth上限340并适配可用空间；三档缩放检查实际屏幕宽度、边界和交互，统计及FPS联合33项通过 |

| 2026-11-01 | 400～800无行动实体卡顿，长帧补算后再次阻塞 | 每生产者每子步全表竞争、资格/主行为重复查询，幼体逐个生成繁殖日志；重力持续唤醒并射线检查；0.5减0.1产生第六个残差子步 | EntityNeighborhood.cs、GeneAuthorityCache.cs、EcologyDiagnostics.cs、StationaryBodyRest.cs、SimulationStepBudget.cs及最小适配 | 同帧出生/死亡/表达/搬运/恢复失效；地形/传送/行动变化唤醒；积压不计入已完成日期；400/800/1600对照，生态与运动回归 |
| 2026-11-01 | 统计栏仍偏大 | 340宽度与纵向拉伸占用过多底栏空间 | MapEntityStatisticsUI.cs、GameplayHudRegression.cs | 限制260×136并按缩放检查宽高、下拉与文字 |
| 2026-11-01 | 生态回归启动后未执行 | Unity搜索索引异常打断delayCall | EcologyBalanceRegression.cs | 改为幂等update等待，必须读取实际PASS结果 |

| 2026-11-01 | 统计同时显示两页、缺少存档内数量控制及清理入口 | 统计挂在页面公共根，生成/繁殖没有统一配额 | MapEntityActions.cs、WorldGameSettingsPanel.cs、WorldGameSettings.cs、WorldEntityActions.cs及生成/保存/联机适配 | 统计只在第二页；成员只读但隐藏创造/设置/清除；服务端拒绝伪造管理请求；超额存档完整恢复、真实出生等待空位、清理保存不复活 |
| 2026-11-01 | 修改参数时同步卡住、地形误改湿度、种子精度丢失 | UI直接Build及MarkDirty触发主线程全量气候生成；地形落入湿度分支；整数先转float | WorldParameterUpdate.cs、WorldLoadingView.cs、PlanetParameterEditor.cs、LanEditActions.cs | 复用加载动画、后台纯数据计算、主线程提交；温湿度不重建地形；实体/镜头/时间/暂停保留，精确32位整数文本，失败/重复请求有界且恢复输入 |

| 2026-11-01 | 出现深色球体且右键无法查看，统计需移回第一页 | 士兵尸体沿用球形深色外观且删除碰撞体；右键只查Physics和DarwinEntity，无法识别世界资源 | ColonyResourcePresentation.cs、WorldInspectablePicker.cs、WorldResourceInspector.cs、ColonyResource.cs、GeneInspectorUI.cs、SimulationControlPanel.cs | 尸体共享叉号标记，几何拾取不增加物理开销且尊重遮挡；活体/尸体面板互斥；搬运/移除/读档/成员只读及来源字段校验；存活统计排除尸体，第一页保留管理按钮 |

| 2026-11-01 | 闪退/卡死后只有输出，难以保存或定位最后错误；Windows Mono内存值为0 | 普通日志后台队列无法证明正常退出，缺少独立错误刷盘、运行状态心跳、原生日志归档和导出；Mono进程工作集API在本机恒为0 | RuntimeDiagnosticSession.cs、RuntimeCrashDiagnostics.cs、DiagnosticFiles.cs、DiagnosticMemory.cs、RuntimeDiagnosticSettings.cs、RuntimeDiagnosticWorldState.cs、RuntimeDiagnosticsUI.cs、RuntimeFileLog.cs、RuntimeLogLifecycle.cs、GameMainMenu.cs、RuntimeDiagnosticsRegression.cs | 错误独立刷盘且有界；异常退出与根因区分；持有锁的并发实例不误判；心跳后台更新但Unity只主线程采样；终止异常不写正常标记；实际Windows进程强制结束/重启/导出；原生报告受预算限制；编辑器日志不自动采集；写盘故障可见，不修改异常传播策略 |

| 2026-11-01 | 统计仅能查看单基因携带数，不能用包含/不包含组合条件列出和定位实体；空标题复用行缺少Text | 缺少独立条件模型与结果列表；UIFactory对空标题不创建文字组件 | EntityGeneFilter.cs、MapEntityGeneFilterUI.cs、EntityFilterResultsUI.cs、MapEntityStatisticsUI.cs、EntityGeneFilterRegression.cs | 包含全部/排除任一且同基因互斥；携带不等于生效；复制基因去重；实际下拉切换和清空；列表固定行分页与少量结果缩短；失效引用重验；普通成员只读、窄屏可展开、页面及世界退出关闭；不改全局清除范围；列表预建文字再复用 |

| 2026-11-01 | FPS背景遮挡画面、下拉出现白绿块，工匠漏收自然尸体，营养与寄生机制混用 | 默认下拉子控件无精灵；死亡入口分散且搬运仅认本群；攻击伤害凭空产粮；旧繁殖寄生承担营养吸取；库已含新定义时旧树漏迁移 | FrameRateDisplay.cs、RuntimeDropdownSkin.cs、EntityDeathSettlement.cs、NutritionTransfer.cs、NutritionParasite.cs、EggHostPregnancy.cs、CorpseFeeding.cs、ColonyCorpseAccess.cs、NutritionEvolutionUpgrade.cs及配置与适配 | 致死消化/尸体单次结算且严格守恒；中立尸体真实行走运输、独占和交付；寄生成年前后、禁用、宿主死亡及碰撞恢复；外来卵独立DNA、计时和逆序读档；迁移后保留用户树编辑；副本只展示；实际展开截图、编辑器编译和Windows构建 |

| 2026-11-02 | 超过2,097,152三角形地形碰撞警告和潜在漏检；菜单视频WindowsVideoMedia Flush错误；底栏缺少设置快捷入口 | 超大网格仍使用PhysX快速结构且重建前未解绑旧碰撞；原H.264 High/B帧走Windows原生解码；设置只在Esc内 | PlanetTerrainMeshBuilder.cs、MainMenuBackground.cs、MainMenuBackgroundRecorder.cs、SimulationControlPanel.cs、GameMainMenu.cs、SampleScene.unity、earth-orbit.webm、TerrainVideoSettingsRegression.cs | 先解绑再改网格，按引擎上限选择烘焙结构且保留精度和其他选项；真实211.68万三角形多方向射线；VP8实际循环/暂停/释放；两页设置、输入门控、原运行状态和三档缩放；发布版构建运行验证 |
| 2026-11-04 | 设置需求被误解为底栏按钮；操控近身攻击漏判；补给点外圈不计入种群范围；缺少易修改的游戏数值文件 | 按键分散在输入脚本，球形扫掠与地形竞争，范围只有王后单圆，数值只存在内部配置 | ControlBindings.cs、ControlSettingsPage.cs、GameplayTuning.cs、EntityAttackAim.cs、ColonyActivityRange.cs及输入/行动/图层适配 | 移除底栏入口，设置页左侧分页改键、冲突检测与本机持久化；仅扩攻击选择框，前方与真实遮挡验证；王后和补给点圆并集共用于巡猎/采集/图层与领地；外部JSON校验、回退、运行代谢倍率；30项专项、59项蚁群及实际Windows构建回归 |
| 2026-11-04 | 设置变宽变扁、返回点击不稳定；种群空补给圈和内部交叉线；取消图层后仍绿色；缺少种群列表和图层热键 | 设置尺寸扩大且返回混用滚动布局；出生预建3个空点并逐圆画线；SpriteSwap焦点覆盖实际取消状态；配置不支持注释和实时载入 | GameMainMenu.cs、CommentedJson.cs、GameplayTuning.cs、ColonyActivityRange.cs、PopulationRangeLayer.cs、PopulationRangeOverlay.hlsl、PlanetURP.shader、PlanetOcean.shader、MapPopulationListUI.cs、SimulationControlPanel.cs及按键配置 | 480×560固定返回区且原生鼠标点击验收；仅实际剩余能量扩圈，圆并集统一填色/外沿不留内部边；唯一图层状态及输入门控；中文注释及外部文件自动载入；三卡统一尺寸、窄屏入口；30新专项+30操控+59蚁群+实际Windows构建通过 |
| 2026-11-04 | 种群入口只提供单群下拉，缺少完整列表、显示配置、分布图表及长期发展谱系 | 临时统计依赖存活实体，没有独立档案和来源事件；入口误放第一页 | PopulationArchive.cs、PopulationWorkbench.cs、PopulationCharts.cs、MapPopulationListUI.cs、ColonyWorldState.cs、建群/出生/颜色与联机适配 | 入口第二页且不依赖创造权限；生效/携带去重、互斥年龄和多基因构成口径分开；灭绝历史与真实来源持久化，旧档不伪造；颜色/隐藏成对写读、周期采样有界压缩；窗口暂停及实际射线防穿透；36项专项、59蚁群、30图层及实际Windows构建通过 |

| 2026-11-05 | 种群档案显示累计小数天数，难以对照游戏日历 | 档案独立用模拟秒除以10显示天数，历史图表另有格式 | PopulationWorkbench.cs、PopulationCharts.cs、PopulationArchiveRegression.cs | 概览/事件/图表/采样共用年月日，沿用底栏日历；历史标签加宽；跨月跨年与681.8天回归 |

| 2026-11-05 | RGB滑条都显示青绿色，种群颜色无法用于实体身体 | 主题滑条固定青绿填充，实体受击颜色与种群色没有统一显示入口 | RuntimeMenuTheme.cs、PopulationWorkbench.cs、PopulationArchive.cs、EntityStats.cs | 通道独立纯色；每群同步开关默认关闭并随存档/快照保存；材质槽属性块不污染共享资源，受击优先且结束恢复；原生属性块在Awake创建 |

| 2026-11-05 | 范围不透明度只能全局修改；有性实体长期无配偶无法延续种群 | 档案没有单群透明度，繁殖没有无配偶等待及单亲回退分支 | PopulationArchive.cs、PopulationWorkbench.cs、PopulationRangeLayer.cs、PopulationRangeOverlay.hlsl、ReproductionGeneLogic.cs、AntColonyReproduction.cs、ParthenogenesisEvolutionUpgrade.cs及存档/配置 | 旧档默认值与单群0～1不透明度分开；并集不重复混合；新出错分支迁移一次；成年连续等待、营养和数量上限、冷却及分工边界；等待/单亲类型成对写读，空配偶Genome归一化，普通缺粮繁殖不新增无效物理扫描 |

| 2026-11-05 | 筛选下拉展开后无法收起，残留Option A模板行 | 原生Dropdown创建当帧Show早于Start，淡入控制器为空，展开流程在隐藏模板/创建遮罩前中断 | RuntimeDropdownDismissal.cs、RuntimeDropdownSkin.cs、GameMainMenu.cs、EntityGeneFilterRegression.cs | 初始化期间禁止展开但保留射线；不覆盖业务权限；列表和模板分别清理；Esc优先关闭，鼠标外部/按钮/选择/切页/HUD隐藏回归；40项专项及Windows构建通过 |

| 2026-11-05 | 同群成员挤到王后头上，随机生成缺少条件与长期比例维持 | 空闲/跟随/取食共用中心点，地面射线命中实体；生成来源只有固定模板，缺少世界级比例规则与繁殖调节 | ColonyFormation.cs、PlanetGravity.cs、AntColonyMember.cs、ColonyFeeding.cs、RandomSpawnWorkbench.cs、RandomPlanetSpawner.cs、PopulationMaintenance.cs及存档/联机适配 | 分散站位与切向避让不传送、真实地表/出生空位；两种来源互斥且出生不重抽；补充计入分母，超量只延迟新孕育；完整互斥比例防反馈增殖，拒绝无解目标；规则/等待成对保存、上限/暂停/权限/切世/清除回归；44专项、59蚁群、48生成界面、91档案及Windows构建通过 |

| 2026-11-05 | 生成条件选择框沿用系统外观、搜索输入不可靠，维持页只列已建规则且条件无法独立保存 | 来源窗口混用IMGUI和Canvas，缺少原生持久输入控件、来源库和自动规则行 | RandomSpawnWorkbench.cs、RandomSpawnPresetStore.cs、PopulationMaintenanceRegression.cs | 原生主题有无按钮互斥；筛选只重建结果保留输入焦点；条件JSON独立原子写读、外部校验；自动列出来源但显式启用，移除规则不删除来源；58专项、48界面兼容与Windows构建通过 |


## 2026-11-06 精简优化补充台账

|日期|现象|根因|修复文件|防复发要点|
|---|---|---|---|---|
|2026-11-06|基因表达事件异常后实体后续无法重算|重算锁只在正常结束路径释放|EntityGeneController.cs|finally释放锁；订阅者抛错后再重算应增加逻辑版本|
|2026-11-06|同帧替换个体后维持比例计数沿用旧结果|缓存仅比较总数，等量替换不失效|EntityRegistry.cs、PopulationMaintenance.cs|成员版本在成功注册/注销时递增；同帧等量替换立即重统计|
|2026-11-06|扩展AI在Tick内改变表达可能枚举失败，销毁时未释放模块|直接遍历可变字典，缺销毁清理与副本门控|EntityAIController.cs、GeneAIModuleRegistry.cs|统一工厂；快照核对身份；销毁成对退订释放；暂停/副本不推进|
|2026-11-06|旧实体状态回归误把无消化能力的领地个体当成食物猎手|营养规则迁移后旧测试未更新前置条件|EntityStateRegression.cs|先验收无消化能力不主动取食，再按配置授予消化能力验收真实追击与攻击|
|2026-11-07|旧社会生态回归取得空寄生组件后中断|gene_parasitic已是卵寄生，旧吸血测试仍使用旧定义前提|SocialEcologyRegression.cs|验收新卵寄生不安装/不激活旧组件；营养寄生生命周期由NutritionEcologyRegression专项覆盖|
|2026-11-07|精简后文件索引的meta数量仍包含删除项|只读取git缓存名单，未过滤删除项或纳入待提交新文件|Tools/generate_file_catalog.py|生成时列出未忽略的新文件并核对文件实际存在|


## 2026-11-07 生态默认与建群保障台账

|日期|现象|根因|修复文件|防复发要点|
|---|---|---|---|---|
|2026-11-07|新王后缺粮、缺工匠，无法建立补给链|繁殖依赖储备，起步没有育幼食物；随机分工与全局繁殖等待又会推迟首批工匠|ColonyFoundingSupport.cs、AntColonyMember.cs、AntColonyReproduction.cs|一次性有限创业物资入存档；优先工匠，王后临时采集，完成货物交接后退出；副本不执行|
|2026-11-07|默认70/30若只匹配精确模板，变异及其他种群会让满分区反复补充|模板身份不等于实际生产/移动能力|EcologyMaintenancePolicy.cs、PopulationMaintenance.cs|默认分区按实际能力和共同有效分母统计；普通来源规则保留原语义；验证分区外个体不会无限增生|
|2026-11-07|外部生态配置无效时，旧档可能清场后才发现|旧档缺配置，加载时才读取当前外部默认|EcologyDefaults.cs、WorldSaveStore.cs|先固定并校验配置快照与基因引用，再开始清场；旧档规则列表保持原样|
|2026-11-07|营养回归在窄Game窗口下无法展开下拉框|夹具只改minimumWidth，实际卡片仍处于折叠状态|NutritionEcologyRegression.cs|先切回第一页并点击真实筛选入口，确认卡片可见再测试下拉菜单|
|2026-11-07|自动生态补充会退出实体视角或取消放置|后台维持复用手动随机生成入口，无条件切换玩家模式|RandomPlanetSpawner.cs|只有手动生成退出/取消模式；真实自动补充前后验证操控与放置仍有效|


## 2026-11-08 实体计划与近邻合并计算台账

|日期|现象|根因|修复文件|防复发要点|
|---|---|---|---|---|
|2026-11-08|成员在运输/巡逻与补给之间反复折返|开始与结束补给共用同一饥饿阈值，寻路每次重新选最近资源|EntityTaskPlan.cs、ColonyFeeding.cs、AntColonyMember.cs|保存计划和目标；恢复线高于开始线；目标失效才重选；保留货物和原任务；吃饱后恢复运输|
|2026-11-08|静止同基因近邻逐体重复计算，合并时可能重复扣粮或丢失时间|没有全局调度归属、欠账状态及个体副作用边界|EntityWorkScheduler.cs、StationaryCohortKernel.cs、EntitySaveState.cs、Vitals适配|代表变化量只应用于兼容近邻；原Update当帧跳过；个体寿命与孕育独立；欠算时间成对保存；受伤/移动/状态/基因变化拆组；启停组件不混组|
|2026-11-08|高优先级杀死后续快照成员后，死者仍可能推进孕育|调度快照完成后没有再次检查对象是否仍启用|EntityWorkScheduler.cs、EntityPlanningMergeRegression.cs|执行每个活跃条目前复核生命周期；原生回归验证先死亡后不能产生后代|
|2026-11-08|副本可能在房主快照之间额外扣粮和受到环境伤害|基因有权威保护，Vitals/环境Update缺少同样保护|EntityVitals.cs、EntityEnvironmentHazard.cs|副本仅展示；原Update和共享入口各验权，原生反射调用Update验证不耗粮|
|2026-11-08|多胎存档读回被判损坏或重复交付|Unity把null内联对象恢复为全零；缺少总数和待出生数|MultipleBirthPolicy.cs、ReproductionGeneLogic.cs、AntColonyReproduction.cs、WorldSaveStore.cs|全零按旧单胎兼容；每实际出生才扣剩余数；上限/空间不足保留；坏胎数清场前拒绝；实际磁盘往返|
|2026-11-08|新增繁殖分支未孕育时也压制成长|旧求值器只对基础繁殖ID按孕育开关抑制|GeneDefinitionJson.cs、GeneDatabase.cs、GeneValueResolver.cs、gene_multiple_birth.json|用配置声明suppressionOnlyWhileGestating，未成年也能正常成长；升级和用户定义保持兼容|
|2026-11-08|长期模拟到上限后王后完成孕育却无法生产，小群没有后继|自动维持/普通繁殖占满名额，繁殖分工只在较大群体或随机抽中时出现|ColonyContinuityPriority.cs、AntColonyMember.cs、EcologyReproduction.cs、RandomPlanetSpawner.cs|开启建群保障时有限保留真实空位、首批工匠后优先培养繁殖个体；不突破全局上限、不删除实体；长期物理模拟验收|
|2026-11-08|修复重复扣粮，且溺水时可能被第二套修复抵消|修复基因和Vitals均计算修复，前者未遵守环境限制|RepairGeneLogic.cs、EntityVitals.cs|修复统一在Vitals，基因仅提供属性和提示；共享核和活跃路径共用结算|


## 2026-11-08 读档后的界面交互恢复台账

|日期|现象|根因/覆盖的恢复缺口|修复文件|防复发要点|
|---|---|---|---|---|
|2026-11-08|读档后底栏、筛选和种群入口可见但无响应|旧_uiBuilt只证明节点存在，不能证明运行时UnityEvent还有效；菜单仅Awake订阅Loaded；缓存的关闭状态和缺失输入组件缺少恢复路径|WorldHudInputLifecycle.cs、SimulationControlPanel.cs、WorldSessionManager.cs、GameMainMenu.cs、HudInputRegression.cs|加载遮罩结束前重建回调并保留分页；菜单成对重绑；补齐或启用唯一事件系统、射线和模块；不覆盖合法弹窗、Esc防穿透与只读权限；真实存档副本和完整指针状态机回归；未将冷启动正常直接当成现场根因已唯一确定|


## 2026-11-08 合并计算性能退化台账

|日期|现象|根因|修复文件|防复发要点|
|---|---|---|---|---|
|2026-11-08|本地延迟，合并计算后帧耗时反而上升|活跃优先级排序重复查组件；到期分组拼接长键、创建临时列表；共享小步重复验证与查询；单成员无共享收益却走完整代表路径|EntityWorkScheduler.cs、StationaryCohortKernel.cs、SchedulerPerformanceRegression.cs、EntityPlanningMergeRegression.cs|线性优先级、值类型预散列、组与桶复用、单成员逐体补算；保留死亡/权限/欠账/变异边界；签名只保留活跃引用；公平计入两条路径的生命与代谢，并采样真实存档副本，不用代表次数或恒零的Mono分配计数替代性能验证|


## 2026-11-08 完整现场卡顿与统一实体检查台账

|日期|现象|根因|修复文件|防复发要点|
|---|---|---|---|---|
|2026-11-08|同数量下合并优化后仍有本地延迟|早期208实体副本不是最新385实体现场；工匠/临时王后的资源筛选对每具尸体重新枚举所有资源生成活动圆，成为平方级搜索|AntColonyWorker.cs、ColonyActivityRange.cs、RuntimePerformance.cs、FullScenePerformanceRegression.cs|使用最新存档、完整地形与固定数量做分项计时；每次同步搜索复用圆列表；保留球面资格与原最近目标排序；1500远处尸体和高度差用例防回归；不以实体数或调度次数替代实际帧耗时|
|2026-11-08|需要锁帧与可复用性能证据|缺少目标帧率设置及游戏分项报告|FramePacingSettings.cs、RuntimePerformanceCapture.cs、GameMainMenu.cs|画质后应用、与VSync协调、保存恢复；空闲不采样、结束释放；不可用指标明示，不相加父子耗时|
|2026-11-08|失效原因不明、尸体详情布局不同、生命阶段不可编辑|检查界面只显示布尔状态；资源没有生前档案；缺少权威阶段入口|GeneFailureReasons.cs、GeneValueResolver.cs、CorpseEntityRecord.cs、GeneInspectorUI.Corpse.cs、EntityEditPopup.cs、EntityStateEditor.cs|不改运行求值；禁用基因补只读条目；复用原布局，无伪活体；旧档全零对象兼容与损坏档案区分；阶段/数值成对保存；幼年先释放运输；遵守服务端权限和合法输入门控|

| 2026-11-08 | 数百非移动实体零尸体仍周期卡顿，降频被误认为真实共享 | 兼容实体多数为孤立组且同步到期；累计浮点尾数额外触发整轮行为 | EntityWorkScheduler、CohortDeadline、SimulationSubsteps、EcologyEnergyBudget；MergeAlgorithmAuditRegression | 同一完整存档开关合并/资源绘制/零资源对照；同时记录真实共享次数、帧P95和寿命加欠账守恒；完整混合世界仍有活体与物理瓶颈，不宣称全面提帧 |


## 2026-11-08 种群有粮不吃与细分性能台账

| 日期 | 现象 | 根因 | 修复文件 | 防复发要点 |
| --- | --- | --- | --- | --- |
| 2026-11-08 | 有剩余能量但成员不能使用，迁居与旧计划抢占进食 | 育幼/成年是硬类别；远锁定源不在半径时直接返回；迁居分支先于补给 | ColonyFeeding、ColonyFoodSources、AntColonyMember、EntityTaskPlan、GameplayTuning | 专用点优先及备用可调；近处实际进食；保留原迁居/货物；外群保护和王后最低口粮；四类成员120模拟秒持续吃单类补给；真实读档后进食 |
| 2026-11-08 | 大量尸体被每次补给搜索遍历 | 可吃能量块和尸体共用全部资源列表 | ColonyFoodSources、ColonyResource | Create/启停/移除/清场/读档同步索引；300尸体不增加食物候选；容量直接查询不分配快照 |
| 2026-11-08 | 只看基因总计无法确定下一步优化重点 | 行动、邻域重建和队形未单独计时 | RuntimePerformance、LocomotionGeneLogic、EntityNeighborhood、ColonyFeeding、ColonyFormation、ColonyFeedingAuditRegression | 报告区分包含关系；完整最新场景测量，附数量、地形、API与模拟倍率；不把补给修复称为整体提帧 |


## 2026-11-08 全局轮次变异、观察与第一页规则卡台账

| 日期 | 现象/变更 | 根因与实现 | 文件 | 防复发要点 |
| --- | --- | --- | --- | --- |
| 2026-11-08 | 全局每轮只随机抽活体一定比例，默认10% | 原阶段逐实体抽签与近邻序号预算不符合总数比例；改统一模拟时间轮次、部分Fisher–Yates与剩余队列 | MutationRoundController、GlobalMutationManager、EntityGeneController、WorldSessionManager、WorldSaveStore、GameplayTuning | 100活体选10身份；0%、锁定角色、卵、暂停、副本；预算不丢名单；计时和队列实际读档；阶段属性仍重算 |
| 2026-11-08 | 行动搜索应接收视野内坐标，观察基因区分范围/注意力 | 原行动逐子步全表搜；复用共享坐标并按视野粗格投递，观察收件箱复用与目标实时/采样定位 | EntityCoordinateGrid、EntityPerceptionHub、PerceivedTargets、ObservationSettings、ObservationGeneLogic、ObservationGeneUpgrade及行动/巡猎/状态/反击/警戒适配 | 隐藏/死亡/出视野资格保留；同帧新注册可见；300远处对象不进粗格距离判断；静止粗格跨帧复用；旧无观察基因范围兼容 |
| 2026-11-08 | 第一页增加同尺寸规则控制卡 | 独立260×164卡，窄屏展开，规则按原字符位置保留注释并原子保存 | SimulationRulesPanel、MutationRuleEdits、SimulationControlPanel | 空标题按钮不创建Text须避免；实际Card尺寸和图像检查；只读/副本入口拒绝、无效输入不写盘；原统计/筛选与第二页不移位 |
| 2026-11-08 | 热重载后不能重复安装变异计时器 | 静态引用清掉但场景组件可能仍存在，Ensure先认领并在读档确保安装 | MutationRoundController、GameBootstrap、WorldSessionManager | 静态引用模拟重置后组件仍恰好一个，暂停与读档不重复推进 |


## 2026-11-08 顺序任务与坐标计算优化台账

| 日期 | 现象/变更 | 根因与实现 | 文件 | 防复发要点 |
| --- | --- | --- | --- | --- |
| 2026-11-08 | 工匠需要完成当前任务再执行下一项，饥饿插队 | 原计划只有feeding布尔值；顺序队列存目标，实际交付后出队，补给保留原任务和独占货物 | WorkerTaskQueue、MemberTaskSequence、EntityTaskPlan、AntColonyWorker | 真正采集、交付、下一目标、目标失效、逆序读档；未来目标不预占；明确新命令与取消；角色生命周期释放 |
| 2026-11-08 | 旧档/恢复后的活体运输目标可能无效 | Unity把null字符串恢复为空，??会错误选择空resourceTarget；改非空目标选择并同步明确任务插队 | WorkerTaskQueue | 旧空串资源目标加非空livingTarget应迁入正确活体任务；已有货物不能重新创建 |
| 2026-11-08 | 数千尸体放大采集/食腐查询 | 各入口遍历资源All；改统一资源分类坐标格、球面投影格与同群能量索引 | ResourceCoordinates、ResourceGroups、ColonyFoodSources、CorpseFeeding、EnergyBlockMetabolism | 创建、停用、搬运、移除、星球变化与清场同步；高度差不漏球面候选；300尸体不增加食物候选 |
| 2026-11-08 | 同物理位置重复决策与转向 | 小步把完整意图/避让多次执行；分开决策周期与累计移动，任务/饥饿/受击等打断 | ActionDecisionCadence、ActionMotionBatch、LocomotionGeneLogic | 冷却和实际伤害仍逐步；嵌套推进恢复父批次；异常/死亡finally释放；手动攻击不缓存丢失 |
| 2026-11-08 | 孤立核逐小步交错营养和代谢产生数值差异 | 原路径先整批生产、后结算环境/代谢，交错改变容量溢出；核保留原批次顺序，仅跳空被动调用 | SingletonSettlement、EntityWorkScheduler、EntityGeneController | 与原路径相同累计寿命、营养、上限和欠账；阶段切换/受伤/孕育拆分；身份不共享 |
| 2026-11-08 | 重复纯求值与全域竞争缓存失效 | 表达使用有界纯结果缓存；空间桶跨帧保留，局部生产者变化只失效邻近格 | GeneExpressionCache、ProducerClassification、EntityNeighborhood | 个体GeneInstance/属性/行为不串用；环境、角色、缺失状态入键；重载清空；出生死亡和跨格即时正确 |
| 2026-11-08 | 接地和外观高频重复工作 | 接地缓存与摩擦按变化写入；资源外观统一调度、远处灯关闭、共享材质实例化 | GravityProbeCache、ResourceVisualScheduler、EnergyBlockGradeVisual、ColonyResource | 地形/星球/位移失效与起跳下落保留；真实Shader构建、颜色、拾取与营养不受灯光降频影响 |

| 2026-11-08 | 长程灭群不能直接归为补给失败 | 含捕猎压力测试存在王后营养充足仍被连续击杀；测试专用记录伤害并显式隔离战斗验证补给，不修改正式伤害 | ColonyTaskStressAudit、ColonyContinuityRegression | 保留自然压力失败记录；归因实际HP/营养和攻击者；700秒补给隔离通过只说明补给链，不冒充战斗永续 |

| 2026-11-08 | 普通实体也应有顺序补给/食腐计划 | 仅工匠排队仍不能覆盖普通营养实体；按能力维护资源目标序列、扫描计时和存档，完成/失效打断行动缓存 | EntityResourcePlan、EnergyBlockMetabolism、CorpseFeeding、EcologySaveState、ActionDecisionCadence | 两块能量/两具尸体按序完成；近食插队、实时资格、实际耗尽、磁盘恢复、旧空快照清理、清场释放引用 |

| 2026-11-09 | 拉远渲染大量冗余几何；规则不能切换阶段变异或周期清尸 | 云/大气复制百万级地形代理；旧规则仅支持定时抽样 | WeatherShellGeometry.cs、PlanetWeatherVisuals.cs、StageMutationPolicy.cs、CorpseCleanupController.cs、RuleCardExtensions.cs及存档适配 | 解析球外接低模、海岸精度保留；固定远近CPU/GPU对照；模式互斥阶段去重；清尸分帧且删除前复核货物保护、进度真实写盘和恢复 |

| 2026-11-09 | 重力/安全网重复探地与写入，斜坡静止体难以休眠 | 重复PhysX和高度采样，径向落点不等于真实斜坡接触，重复解析移动资格 | RadialTerrainSurface.cs、EntityGroundProbe.cs、PlanetGroundPhysics.cs、SurfaceAnchorPhysics.cs、ManualGroundPhysics.cs、VerifiedGroundRest.cs及原入口适配 | 真实三角形与PhysX逐点对比；非生成网格/缩放回退；表达/变换/碰撞失效；接触验证后才休眠；移动/跳跃/搬运/水下/救援回归及同场景前后计时 |


## 2026-11-09 活跃计算与规则列表台账

|日期|现象|根因/改动|文件|防复发要点|
|---|---|---|---|---|
|2026-11-09|规则标题按钮不便继续加规则，紧凑下拉可能没有文字|独立稳定ID注册列表；默认30高下拉的上下边距会裁空21高文字|SimulationRuleList、RuleCardExtensions|主题/初始化关闭保护共用；260×164不变，标题文字空间、切页草稿、部分保存和权限回归|
|2026-11-09|失效原因重复占用编辑入口，只有基因标题触发悬浮|原因统一整张基因卡悬浮，原入口仅编辑|GeneHoverSurface、GeneInspectorUI、EntityEditPopup|输入和双击/滚轮保留、提示不拦截射线、离开/隐藏清理；尸体使用生前档案且不可编辑|
|2026-11-09|控制模式高地天空看似没有大气|海面指数密度在高地视角很弱，气壳可能低于最高地形|AtmosphereViewPolicy、PlanetWeatherVisuals、PlanetScattering|真实像素对照；只增强完整穿气壳的天空，不误把海面截断当地面天空；昼夜、近景地形和退出恢复验证，不加光线步数|
|2026-11-09|活跃实体逐小步重复空行为和组件查询|按表达版本建执行计划，同步批内组件复用；生产资格只缓存静态表达|ActiveSimulationPlan、ActiveNutritionContext及原入口适配|繁殖副本仍有等待/缓存副作用，不能过滤；重算恢复剩余快照、未知扩展回退、嵌套/新增/销毁失效；真实收入/阶段/孕育时间等价和405实体四轮开关对照|

本轮验收：516项原生断言、四轮405实体性能对照、第一人称昼夜/深度像素检查、最终Windows非开发构建及12秒启动通过。维护流程图376脚本/3903方法/3906唯一图，19项浏览检查通过。详见`Docs/验证记录/性能验证汇编.md#doc-b018cd62557c`。


## 2026-11-10 正常生态性能反馈复查

|日期|现象|根因/修复|文件|防复发要点|
|---|---|---|---|---|
|2026-11-10|用户反馈优化后帧率/延迟更差，但上次隔离跑分无法代表现场|旧夹具关闭生命/环境并限制出生，遗漏正常生态热点；新增完整负载、用户DX12/视口及真实旧适配源码对照|LiveSimulationAudit、RuntimePerformance及细分计时入口|不得用隔离收益承诺普通世界；记录实际交付模拟时间、起止数量、API、视口和实验限制；本次尚未稳定复现用户现场骤降|
|2026-11-10|关闭活跃优化仍维护执行计划，批外组件查询也收检查成本|Controller和公共Run双重直接回退；批外先原生查询，不计数/查配置|EntityGeneController、ActiveSimulationPlan|验证关闭不增加Builds、批外不增加ComponentReads；保留原顺序和真实时间，不以冻结换帧率|
|2026-11-10|完整生态中维持比例约6.5ms/帧，重复解析生产和零移动资格|规则改复用已有ProducerClassification，基础零的移动不走乘法修饰链|EcologyEnergyBudget、EcologyMobility、EcologyMaintenancePolicy、ProducerClassification、EntityNeighborhood|评估引用/版本/配置失效，动态孵化与正基础移动保持实时；未进邻域的注销同样清缓存；完整资格、重算回调及规则回归|
|2026-11-10|同帧死亡后剩余扣血在停用对象上启动闪烁协程|闪烁入口检查组件/对象活跃状态|EntityStats|保留真实伤害，只跳过无效渲染；实际日志只有少量错误，不能误称这是主要卡顿原因|

本轮425项原生断言及Windows非开发构建通过；完整生态对照和仍未复现的范围见`Docs/验证记录/性能验证汇编.md#doc-0807d3bbc9cc`。

性能验收补充：隔离微测必须与正常生态分开报告；串行不等于机器独占，应记录同时运行的Unity/后台渲染，不能边争用资源边保证用户帧率。未经授权不得关闭用户实例；只清理本轮自己启动且身份明确的测试实例。


|2026-11-10|用户明确近景/控制掉帧，而原采样缺GPU与镜头信息|新增按需GPU帧计时与镜头/倍率/积压上下文，单独计准星和相机探地；真实控制输入夹具判定仍在控制，目标死亡样本必须作废|GpuPerformanceSample、RuntimePerformance、NearViewPerformanceAudit、EntityAttackAim、OrbitCamera、ProjectSettings|GPU无数据不能报为零成本；异步GPU最大值不能强配最慢镜头；1倍与16倍分开，补算预算降幅必须说明吞吐损失，不暗改默认玩法；109项原生检查通过|

用户补充确认该近景/控制问题发生在1倍速。16倍复现不适用于此现场；新增先计时后切镜头的首切测量，最大约77ms，仍未复现250ms，待现场报告。禁止以高倍样本替代1倍用户反馈。


## 2026-11-10 现场报告与交互复查

|日期|现象|根因/修复|文件|防复发要点|
|---|---|---|---|---|
|2026-11-10|1倍近景实际2～3FPS，先前413实体样本没有覆盖|用户19帧现场平均426ms；新495实体世界复现。自动补生产者逐王后/候选重复解析范围，编辑器对正常空GetComponent生成百万次临时诊断字符串；改每王后范围快照与实时TryGet|ProducerPlacementSearch、OptionalComponent、EcologyMaintenancePolicy、ActiveSimulationPlan、EntityWorkScheduler及分类/种群入口|保留70/30目标和原球面资格；固定随机种子对旧算法；普通CPU层级与现场视口对照，Editor收益不能冒充Player收益，不拿高倍率解释用户1倍|
|2026-11-10|读档显示正在运行却不推进|暂停存档先RestoreState(false)，HUD之后重建固定画暂停图标，未收到一次初始化事件；构建完成立即同步真实时钟|SimulationControlPanel|暂停存档立即显示播放；实际输入模块点击后日期推进；运行存档自动恢复真实GameDelta，尊重存档暂停选择|
|2026-11-10|统计“筛选”在宽屏看似无响应|按钮仅改窄屏浮卡标记，宽屏始终强制显示；宽屏单独切换真实可见性|MapEntityGeneFilterUI|真实输入模块分别检查停靠/浮卡开关；窄屏下拉须先通过入口展开，不能对隐藏卡直接Show来误报失败|
|2026-11-10|性能采样完成没有画面提示|只有已离开的设置页Status和日志输出；新增游戏内12秒完成/失败通知|PerformanceCaptureNotice、RuntimePerformanceCapture|成功必须在写盘后显示真实路径；通知纯绘制，不占鼠标、不改旧UI矩形；重复请求先清旧路径|

本次现场修复最终387项原生断言、Windows非开发构建及12秒启动通过；同镜头复测约249→48ms/帧，仍有尖峰，不承诺60FPS。流程图385脚本/3967方法/3970图、187245结构检查及19项浏览检查通过；详情见`Docs/验证记录/性能验证汇编.md#doc-ba70b6906100`。


## 2026-11-10 500实体与繁殖反馈复查

|日期|现象|根因/修复|文件|防复发要点|
|---|---|---|---|---|
|2026-11-10|500以上仍卡，用户怀疑大量繁殖|626实体现场普通繁殖4.7ms、自动投放103ms；42王后×全体×每次失败尝试重复统计。新增同步片段坐标/生产者/缺粮王后快照、纯数据球面判定、2ms投放分帧与8次局部失败后转全局|ProducerPlacementSnapshot、ProducerPlacementSearch、EcologyMaintenancePolicy、RandomPlanetSpawner、SimulationOptimization及资源默认配置|正常预热可能漏掉已完成的生成；冷启动与拒绝落点压力分开记录。对照自然繁殖开启/抑制并验证完成真实数量，不以锁450、停止生成或改普通孕育换FPS；快照仅在同步片段复用，跨帧/出生注销更新。125项原生断言通过，625基础约48ms仍未消失|

本轮最终125项原生断言、Windows非开发构建及12秒启动通过；387份源码及默认优化JSON逐份匹配，流程图387脚本/3988方法/3991图、188122结构检查和19项浏览检查通过。拒绝落点压力625实体101→48ms；普通625仍约56ms，不承诺500以上全部流畅。


## 2026-11-10 比例调节与时间单位

|日期|现象/需求|处理|文件|防复发要点|
|---|---|---|---|---|
|2026-11-10|比例失衡直接生成导致人口不按自然增长，规则无法独立关闭|移除自动补员轮询，旧自动请求拒绝；比例调节总规则默认关闭，开启仅延缓超额类别新孕育，支持100%目标|GameplayTuning、PopulationRatioRuleEdits、PopulationRatioRulePage、RuleCardExtensions、PopulationMaintenance、RandomPlanetSpawner、RandomSpawnWorkbench|开关两种状态都不自动生成；只对新孕育施加等待，已孕育继续，手动生成和来源/规则存档保持有效；旧字段不触发补员|
|2026-11-10|规则“模拟秒”描述不清|周期界面使用游戏天输入，悬浮给换算，JSON仍保留模拟秒|SimulationTimeHelp、RuleCardExtensions、SimulationRulesPanel|原配置不重置；变异/清尸分页分别换算保存，错误提示使用游戏天；默认1天10模拟秒，负载和暂停影响现实耗时|

本次216项原生断言、Windows非开发构建及12秒启动通过；离线编译0错误/0警告。Windows构建1条原有云端符号上传警告。维护图391脚本/4007方法/4010图，188768项结构及19项浏览检查通过。详见 `Docs/验证记录/功能回归汇编.md#doc-7181e96a6917`。

此前未验证的自然节奏及矿脉上传草案暂存在 `Artifacts/PendingWorldEvolution`（补丁、原GUID及恢复说明）；地形/气候/陆地寻路仍待继续，不得把本次规则交付描述为这些功能已完成。

| 2026-11-11 | 近千实体吸收矿物导致营养结算占用约253ms | 每次吸收整张上传矿物图；补给循环重复查询与空组件诊断分配 | OreTextureUploadBatch、PlanetOreSystem、ColonyFoodQueryCache、ColonyFoodSources、EntityTaskPlan | CPU营养即时结算，帧末一次上传；恢复取消旧队列，同帧缓存必须检查版本与可消费能量 |
| 2026-11-11 | AI向隔海猎物/尸体/食物直走并落水 | 只按距离选目标和直线移动，未建立陆地连通区 | LandNavigationGraph、LandPathScheduler、LandReachability、LocomotionGeneLogic | 水域/坡度限制、全局共享预算、候选可达性；验证绕湖与隔海拒绝，不用逐体大数组 |
| 2026-11-11 | 新地貌可能静默改变旧档岸线或埋入资源 | 新增字段默认开启、重建只迁移实体 | LandformSaveCompatibility、WorldParameterUpdate、TerrainResourceRelocation | 缺字段旧档默认关闭；显式升级保留资源身份/能量并重新贴地，非法设置在清场前拒绝 |
| 2026-11-11 | 自然人口增长过快，合并结算与个体年限可能分歧 | 旧秒级成长/冷却与多年目标不一致 | NaturalPopulationPace、EcologyReproductionTiming、StationaryCohortKernel、NaturalWorldSeeder | 统一推进函数，新孕育15～360游戏天，旧计时不重置；种源仅新世界一次，约50年仅理想标定 |


## 2026-11-11 完整大存档热点修复

| 日期 | 现象 | 根因/处理 | 文件 | 防复发要点 |
| --- | --- | --- | --- | --- |
| 2026-11-11 | 近千活体及六千多尸体仍严重卡顿 | 每胎整世界平方身份查找、大型地形射线、仅同物理步接地缓存、重复补给和可选组件诊断分配 | EntitySpawnCapture、OffspringPlacement、GroundSurfacePatch、GroundMotionSnapshot、ColonyFoodCandidates、ColonyFeedDecision、OptionalComponent | 完整同存档、同镜头8秒实测，不清尸/锁450/停繁殖换FPS；事件finally退订，属性变化和实际扣粮立即失效 |
| 2026-11-11 | 资源索引遇到刚性姿态变化会全量重建 | 世界矩阵任意变化就清空资源桶；本现场场景没有挂载自转脚本，不能把自转宣称为现场主因 | ResourceCoordinates、ResourceGroups、ColonyFoodQueryCache | 刚性姿态和结构版本分离；纯自转不重建，缩放/形变重建；球面/三维局部和无穷范围逐对象对照，活动圆读最新世界坐标 |
| 2026-11-11 | 尸体过多使绘制提交上万 | 身体和叉号逐Renderer、逐对象属性块阻止实际自动实例化 | CorpseInstanceRenderer、ColonyResource、simulation_optimization及RuntimeMaterials | 真实RenderMeshInstanced、独立色板、局部矩阵和空间格；移动单个对象只写自身/交换槽，不能重建全世界；对象/能量/点选/运输保留，热关闭恢复；实际像素对照及发布材质检查 |
| 2026-11-11 | 报告普通代谢几乎0ms但CPU层级明显耗时 | Vitals仅共享入口被计时，普通Update漏报 | EntityVitals、RuntimePerformance、RuntimePerformanceCapture、NearViewPerformanceAudit | 计时放实际代谢入口；包含项不可相加；报告缓存命中、真实采样、索引重建和批次数，改善不等同稳定60FPS |

本轮2053项原生断言通过；最终Windows非开发构建0错误/1条符号上传授权告警；可见发布版实际绘制100个尸体批次并加载同一近千活体世界采样8秒。编辑器同存档约349→105ms/帧，发布版仍约74ms，未彻底解决千实体卡顿，不能承诺60FPS；隐藏窗口不绘制的样本已排除。414脚本/4219方法/4222图、200252项结构与19项浏览检查通过。详见 `Docs/验证记录/性能验证汇编.md#doc-ce4e418702a1`。


## 2026-11-12 筛选布局、地貌升级与性能归属

| 日期 | 现象 | 根因/处理 | 文件 | 防复发要点 |
| --- | --- | --- | --- | --- |
| 2026-11-12 | 筛选首击迟钝、卡片横占位，规则栏留空 | 筛选另一个Start才建入口且宽窄两套显隐状态；改Awake同步、单状态上浮，规则跟随统计右边 | MapEntityGeneFilterUI、SimulationRulesPanel | 加载第一就绪帧通过StandaloneInputModule实际点击，不用onClick.Invoke代替；宽窄布局及260×164卡片间距 |
| 2026-11-12 | 新地貌不能静默应用旧世界，湖面呈方格 | 地貌版本、备份升级与岸线显示不完整；v1原算法保留，v2宽盆地及湖岸裁切，显式升级先完整备份 | LandformSettings、LandformSaveCompatibility、PlanetHydrology、PlanetFreshWaterMesh、LandformUpgradeControl、WorldParameterUpdate | 普通读档不升级；旧字段缺省仍兼容；备份成功后才重建、资源身份和日期读写验证；实际河湖近景检查 |
| 2026-11-12 | AI沿旧路跨湖或旧寄生直接入海 | 双线性岸线与真实网格不一致、位移后路径未验证、旧寄生绕过导航 | RadialTerrainSurface、LandNavigationGraph、LandPathScheduler、ParasiteBehavior、NutritionParasite、LocomotionGeneLogic | 最终网格后建图；全球坐标候选和陆地连通；路点/惯性/身体侧面检查；绕湖/隔海及320细分PhysX对照 |
| 2026-11-12 | 采样不能指出脚本，父子计时重复，批次误报0 | Area只有包含范围；采样时机在渲染前清零后 | RuntimeCostBreakdown、RuntimePerformance、RuntimePerformanceDetails、RuntimePerformanceCapture、CorpseInstanceRenderer | 只主动采样建栈、扣子计自身；旧metrics保留；JSON/Markdown/设置排行来自同模型；批次用上一完成帧；未覆盖代码和不可用引擎计数明确说明 |

高空救援回归定位到实际已回到1米且速度已清零，只有径向投影的浮点尾差令严格<=0断言失败；验证采用与已有跳跃检查相同的0.0001速度容限，不改游戏救援行为。地形升级测试新增备份前保存，暴露了原测试能量块缺少合法group标识，已修正测试夹具并要求成功写盘状态，不能略过验证以制造通过。

详见 `Docs/验证记录/性能验证汇编.md#doc-1f84b5fa96c9`。本轮是界面、地形/导航补齐与可归属性能采样，不得宣称已经彻底解决近千活体卡顿；真实采样只覆盖已安装计时范围，不冒充全函数Deep Profile。

本轮最终1954项原生断言通过；真实游戏入口在可见Windows非开发版记录34脚本/40方法范围，998活体、100尸体批次，约71.9ms/帧仍卡顿。406份源码与测试副本一致；416脚本/4262方法/4265图、202868项结构和19项浏览检查通过。


## 2026-11-12 全局坐标、感知快照与增量寻路

| 日期 | 现象 | 根因/处理 | 文件 | 验证与边界 |
| --- | --- | --- | --- | --- |
| 2026-11-12 | 实体移动引发全量视野分桶；地理、溺水和寻路判定分散 | 邻居与视野共用增量空间索引；补采未完成初始化的实体；视野使用有预算的快照与有限 LRU 记忆；统一局部地理数据与实际水深；复用已验证路段 | EntityCoordinateGrid、EntityNeighborhood、EntityGeography、PlanetCoordinate、EntityPerceptionHub、LandRoutePlan、EntityEnvironmentHazard 等 | 托管宿主直接链接生产源码，109 项算法断言通过；1000 同格移动 0 重建/0 桶调整，局部查询 1 候选；真实 PhysX 扣血与整场景 FPS 未完成原生验证，详见 Docs/开发设计/性能与坐标.md#doc-e1c6ddca4210 |


## 2026-11-16 专服发布与更新回归

| 问题 | 原因与修复 | 验证 |
| --- | --- | --- |
| 更新无法判断新服是否真正就绪、旧服是否安全保存 | 独立健康凭证绑定 runId/PID/版本；保存完成回执结合数据租约释放；更新器外置并备份后启动候选，失败恢复 | DarwinCoreBuilder 真实 ZIP 和 Windows 模拟子进程回归；不替代真实 Unity 开服 |
| 框架拆分后索引和流程引用仍指向旧文件/方法 | 补齐 meta 与文件职责，流程引用改为服务端和客户端实现，重新生成并校验文档 | 三种源码编译、结构/GUID 和文档检查 |


| 2026-11-16 | Unity 构建在编译前 0x80000003 崩溃 | Curl 缓存目录权限错误 5，LOCALAPPDATA 重定向无效；构建前验证目录与已有数据库访问，失败不启动编辑器 | Tools/DarwinCoreBuilder/diagnostics.py、build.py、test_builder.py | 权限拒绝和版本拒绝回归，真实核心仍需正常桌面构建 |


| 2026-11-16 | 真实专服构建场景裁剪出现 MissingReferenceException | 删除相机/Canvas 根对象后继续遍历同一对象，嵌套对象也可能随父对象失效；改为先完整收集表现对象、删除音视频组件，再去重并检查存活后销毁对象 | Assets/DarwinPark/Editor/DedicatedServerBuild.cs | 必须运行真实 BuildPipeline，源码编译不能覆盖场景处理回调；保留原开发场景 |

| 2026-11-16 | 新建专服就绪后时间始终为零 | 世界加载暂停期首次 SaveNow 把 Current.running 覆盖为 false；首次落盘显式保存 data.running，再由开服完成回调恢复模拟 | Assets/DarwinPark/UI/WorldSessionManager.cs | 用真实 Player 快照验证 running 和时间递增，不能只检查端口/READY |
| 2026-11-16 | 场景修复后专服包仍缺少开服入口；Burst 临时文件失败 | 失败的场景处理缓存被复用；构建改为 CleanBuildCache，并为 Unity/Burst 子进程设置本次构建独立 TEMP/TMP/TMPDIR | DedicatedServerBuild.cs、Tools/DarwinCoreBuilder/build.py | 真实日志记录场景 bootstrap，实际开服校验；不修改系统环境和用户 ACL |


## 2026-11-16 玩法边界与同类脚本合并

| 问题 | 原因与处理 | 防复发要点 |
| --- | --- | --- |
| 服务端依赖 UI 程序集、参数更新在无图形设备下等待渲染 | 世界/存档/编辑/生成/会话迁入 Gameplay，客户端表现通过接口注册；参数更新只按普通帧推进 | Gameplay 和 ServerUnity 均不引用 UI；实际世界和专服包回归 |
| 读档/副本恢复重复复制全体实体再查差集 | 统一 EntitySpawnCapture，生成注册异常也 finally 退订 | 原生超上限读档与快照恢复，保留原实体标识和基因数据 |
| 无收藏界面的专服保存会丢弃原档收藏标记 | 玩法按稳定实体 ID 记录收藏，UI 查询可选覆盖 | 不安装表现适配器仍保留存档 favorite；不改存档字段 |
| 旧世界回归卡在帧末截图，读档后持有已销毁控件 | 批处理截图不等待帧末，读档后重新取实际 HUD 控件；精确数量测试隔离自然种源 | 修正夹具，不为旧断言改变正式玩法 |


## 2026-11-16 基础基因缺失与行动停滞回归修复

| 现象 | 根因与修复 | 防复发要点 |
| --- | --- | --- |
| 唯一基础繁殖基因在变异后缺失 | 删除分支有保护，出错分支直接替换 DefinitionId 会绕过保护；两条路径共用 mutation_settings.ProtectedGeneIds，最后必要副本不允许删除或换型，仍允许数值变异；默认基因组改读 entity_default，补上漏掉的繁殖项 | 唯一副本/多副本、正常行动到领地演化、3000次混合变异、默认配置均真实回归；不要用 isBaseGene 树根分类误禁行动演化 |
| 正常行动实体有速度且主行为有效，却没有位移 | 微小物理步的 acos(dot) 夹角被舍入为零，使真实坡度被误报为陡坡，SafeVelocity 清掉启动速度；改用半角弦长 atan2 计算距离 | 同存档旧算法对照失败、修复后原生物理/自主行动通过；115项坐标导航检查继续拒绝跨海并覆盖短上坡启动 |
| 历史存档已缺失的基因需要补回 | 经用户选择仅生成独立修复副本；默认值补必要基因，并同步锁定分工和孕育配偶基因组，普通读档不自动改遗传数据 | 原存档逐字节保留、完整目录备份、修复副本真实读档；既有基因/实体身份/进度不变 |

## 2026-11-17 模板隔离、完整备份与控制视角天空

| 现象 | 根因与修复 | 防复发要点 |
| --- | --- | --- |
| 多个存档出现同一模板 | 全局模板文件和静态缓存没有世界归属；切换世界时重新绑定标识目录，旧文件只显式导入 | 23项存储及3项独立进程检查；退出清空、不覆盖同名、备份及恢复携带模板 |
| 数量和删除确认窗口留白较多 | 确认沿用数量编辑布局；分开高度和固定标题/说明区域 | 实际UI高度与既有21项分页、关闭、删除交互检查 |
| 备份不包含完整世界附件的风险 | 统一复制世界目录及旧单文件旁路模板，逐文件SHA256校验后发布，恢复使用新标识 | 中途失败不发布、损坏拒绝、不覆盖原档；归档旧单文件只移动无world.json的模板附件目录，不能移走另一个完整世界 |
| 河面出现斜三角和交叠色块 | 独立河带交叉且岸外采样降到海平面；统一共享顶点水面按实际地形裁切，岸外保留河水位载体 | 实际网格无重复面/非流形重叠边，近景渲染；相邻边ulong编号使用混合哈希，防止默认异或哈希退化为平方查找 |
| 控制模式无法抬头 | 场景仍序列化旧的正数俯仰范围5～70°；场景改为-80～80°，进入控制时兼容迁移旧范围 | 实际相机向上/下与极点平行初始朝向；暂停仍允许观察但不行动 |
| 近地天空不能分辨昼夜或不可配置 | 散射缺少行星遮光和晨昏色，加入真实太阳高度驱动；独立颜色文件自动热更新 | 17项网格/视角/像素/时钟/颜色检查，错误颜色保留有效配置，颜色不重建世界 |
| 观察视角夜面仍亮且晨昏出现整圈红晕 | 地表/海洋固定底亮度、无光照河湖掩盖夜面；气壳外误用摄像机统一判色；按球面位置衰减普通画面，按视线实际气体路径定位晨昏，地图亮度保留 | 8项观察对照与17项既有控制回归；复用太阳和网格，云/气仍各8步，1相机1灯光无新增对象；1080p同步离屏对照未见额外开销，不能据此声称游戏FPS提升 |


## 2026-11-17 多基因部件组合回归

|现象|根因与修复|防复发要点|
|---|---|---|
|移动光合生物仍使用展开叶片，移除行动后可能保留组合形态|部件参数只按数值求值，缺少有效基因条件；PartModifier 增加 requireGene / forbidGene，并在表达时使用有效基因集合|真实实体添加/移除行动后检查叶片尺寸、四肢和受光部件；数值变化不改变离散模式|
|配置组合条件拼错基因 ID 时静默不生效|目录校验只检查 requiresGenes，遗漏绑定及参数条件引用|GeneTreeStorage 对全部组合引用做注册校验；无效条件回归必须明确拒绝|


## 2026-11-18 部件版本性能与实体编辑

|现象|根因与修复|防复发要点|
|---|---|---|
|植物规模不大也出现开销回归，静止批处理失效|营养拆分后白名单和两个生产入口仍只认识原基础营养逻辑|光合允许共享；矿物及个体计划排除；代表和孤立路径均验证真实营养收入；没有行动的空中枢视作无副作用|
|导航图周期性CPU尖峰|每次刷新逐像素投影并上传约2万像素|地表底图按数据变化上传一次，GPU投影，矩阵有变才更新；连续刷新上传次数不增长|
|部件材质报缺少_Color，远景状态可能被旧颜色遮盖|旧生命组件假设每个材质都有_Color，并创建优先级更高的材质槽属性块|先查真实属性，单材质共用同一块，保留部件颜色和实例参数|
|游泳方向误禁陆地移动与跳跃|把基因演化方向作为实际能力开关|方向只决定形态；陆地/游泳/跳跃分别读取属性，水下呼吸独立|
|小数数值填写恰好上限被偶发拒绝|以当前值加剩余容量反推上限产生浮点尾差|直接读取真实上限；原编辑命令回归|

部位缩放/偏移/旋转及顺序任务使用同一权威校验，写盘、模板和网络状态完整传递；植物模板无中枢。Alt右键观察不移动原轨道位置，拉远自动归正，门控及右键拾取互斥。详情及证据见 Docs/验证记录/功能回归汇编.md 的本轮章节。

## 2026-11-18 海洋深度与水中生物

|现象|根因与修复|防复发要点|
|---|---|---|
|海水像不透明的蓝壳，遮住浅水海床和生物|海面由海床深度确定浅滩颜色，再用场景深度计算水中视线长度和遮蔽。普通视图透明混合，数据图层与河湖保留原绘制方式|同一镜头对照开启海面、隐藏生物和隐藏海面，检查画面变化；斜视、日照和深浅交界实际渲染核对|
|手动游泳速度和实体游泳能力不符|水中手动行动沿用了陆地速度。现在根据是否浸没读取游泳或陆地速度|用游泳和鳃呼吸模板验证实际入水推进、呼吸及移除鳃后的溺水伤害|


## 2026-11-18 行动状态、河床与重复计算

|现象|根因与修复|防复发要点|
|---|---|---|
|行动基因出现下拉控件，数值变化不切换状态|行动分类误用独立StateId；恢复按数值解析，统一运行与检查入口，旧世界内嵌标记也不再改变行动规则|25游泳、61两栖、缺失状态、禁用检查及真实UGUI无下拉回归|
|河流浅，河湖水面缺少波纹|默认下切1.5过浅，淡水绕过海水波纹分支；第四版三倍河床深度，共享三维波纹动画，顶点色传真实水深|原版本逐位基准不变；旧世界显式备份升级；100点平均深度1.53→4.50；GPU两时刻检查通过|
|卡片事件及刷新重复查询|每卡另挂EventTrigger，禁用行刷新重建整份检查，水位查询多次转换球面坐标|合并到GeneHoverSurface、绑定时缓存滚动容器、单基因检查、复用坐标；未声称实测帧率提升|

19项隔离原生回归、8组旧版逐位基准和源码编译通过。详情见Docs/验证记录/行动基因与河流水纹-2026-11-18.md。


## 2026-11-18 脚本合并与计算审查

|现象|根因与修复|防复发要点|
|---|---|---|
|多胎实体储粮未达到整胎扣款时停止额外觅食|觅食使用单胎营养预算，实际扣款乘以胎数。两个入口统一调用 MultipleBirthPolicy.RequiredReserve|隔离世界验证储备超过单胎但低于整胎时继续觅食，达到整胎预算时停止额外觅食|
|基因组在抑制、前置校验、缓存写入和检查列表中重复扫描|按定义归集抑制和活跃副本；按原始索引绑定缓存；按实例建立诊断索引|检查同定义副本、禁用条目、缓存绑定和级联失效|
|同类脚本与界面构建重复|接触辅助统一到 PlanetGroundPhysics，两条繁殖迁移共用实现，MutationType 与结果合并，两处界面分隔线共用 UIFactory|保留场景组件和原资源配置入口，更新流程图和职责索引|


## 2026-11-18 千实体与水体地理

|现象|根因与修复|防复发要点|
|---|---|---|
|1000 活动实体严重卡顿|慢帧连续补跑多轮重力和岸线检查，实体模拟还同时占用主线程。限制单帧物理补步、去掉重复坐标和高度转换|同一 1000 活动实体样本记录暂停与运行，270→146 ms/帧；仍是个位数|
|没有游泳基因也进入海洋|普通行动默认给游泳速度；缓存目标粒度粗于岸线|独立游泳基因，真实水面校验，陆行浅水和绕水原生回归|
|水体和气候缺少深度与环流|浅海床、浅自然湖盆，使用噪声当洋流|第五版海湖真水深与透明水面，旧地形显式升级；预计算季风和洋流，跨经线与季节反转回归|


## 2026-11-18 历史生态模板再次生成缺基础基因实体

旧世界内嵌的移动/蚁群/食腐模板与默认移动维持来源没有营养方式基因。只修活体会再次缺失。WorldBaseGeneRepair 在独立副本同时补内置生成来源、主基因组、锁定分工和孕育引用。原存档既有基因和模板附件要保留。现场卡顿诊断及验证见 Docs/验证记录/1024实体现场卡顿与基础基因-2026-11-18.md。


## 2026-11-18 三项现场性能优化

普通决策与周期性种群扫描错峰，保留即时任务和逐小步结算。地面/水位查询使用精确坐标缓存，河段候选通过保守索引排除。远景活体按投影大小合批、保留选中和首人称表现，帧内重复写 Renderer 已改为只在分组变化时写。相同 1014 实体现场复测 175.3→156.7 ms/帧，P95 192.9→170.8 ms，每秒仍推进约 1 游戏秒，仍有个位数帧率。详见 Docs/验证记录/三项性能优化实施-2026-11-18.md。

## 2026-11-18 千实体 Windows 独立版复测

缓存中枢的表达和马达，按累计时间更新普通意图，命令变化立即处理；河水索引跳过空格，保持岸线和坡度判定；稳定代谢合并扣粮，自动存档后台写盘；种群统一重建成员索引。约千活体的 Windows 独立版轨道运行 234.37→186.94 ms/帧，近景运行 215.69→184.90 ms/帧。两组仍只有约 5.4 FPS，重力、岸线和中枢推进继续占用主要预算，45 FPS 验收未通过。详见 [千实体优化分支实测](Docs/验证记录/性能验证汇编.md#thousand-entities-45fps-2026-11-18)。
