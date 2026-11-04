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
