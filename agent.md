# Unity 游戏项目开发通用规则

本文件是 Unity 游戏项目的强制开发规范。任何代码/数据改动都必须遵守以下规则，并在完成后执行「改动后回归检查」，防止同类 Bug 重复出现。
规则面向通用 Unity 项目，具体项目可在本文件基础上追加项目专属条款。

---

## 总则

1. **稳定核心优先**：渲染、存档、交互、世界/关卡生成等核心系统一旦稳定，不得在其上堆叠新功能。
2. **可回滚**：每次改动应能独立定位、独立回滚，避免大范围耦合修改。
3. **可验证**：任何改动都必须有明确的验证方式（编译通过、运行时日志、回归清单）。
4. **写清楚「为什么」**：注释与提交记录说明决策原因，而非复述代码行为。

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
| YYYY-MM-DD | 示例：玩家数据无法保存 | `SavePlayer` 只有注释，未写盘 | `SaveSystem.cs` | 回归检查 2：Save 必须写盘 |
| YYYY-MM-DD | 示例：UI 打开后鼠标仍被锁定 | 输入脚本每帧强制锁鼠标 | `PlayerController.cs` | 覆盖 UI 打开时统一屏蔽输入 |
| YYYY-MM-DD | 示例：组件报 Broken PPtr | 引用 ID 与组件块 ID 不一致 | `Scene.unity` | 加引用前确认组件块真实存在 |
| YYYY-MM-DD | 示例：新增脚本编译报 CS0103 | 手写 meta GUID 位数错误 | `Xxx.cs.meta` | 用 GUID 生成器并校验 32 位 |
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
