# ParasiteBehavior.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Ecology/ParasiteBehavior.cs`

[源文件](../../../Assets/DarwinPark/Entities/Ecology/ParasiteBehavior.cs) · 源码校验：`07c2f705e2729befdf2d9ad1f26ee28433d65a9d07193fc6ffc67d3fc086dd56`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m26d7e4af4cdffdbb"></a>

## Host.get · L18

`get`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="md057cd47821a7aa9"></a>

## Host.set · L18

`private set`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="me65a7509f034e027"></a>

## Offset.get · L19

`public Vector3 Offset`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_offset"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mc0b1fc7eaac7771a"></a>

## DetectedByHost.get · L20

`public bool DetectedByHost`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_detectedByHost"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="md2af822af86a84dd"></a>

## DetectionRemaining.get · L21

`public float DetectionRemaining`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_detectionRemaining"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="md0a6c5009a0a1a3e"></a>

## VisibleTo · L22

`public bool VisibleTo(DarwinEntity observer)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：observer!=Host"}
  b2["_detectedByHost"]
  b3["observer!=Host"]
  b4["结果：observer!=Host&#124;&#124;_detectedByHost"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

项目内调用/回调引用：

- [ParasiteBehavior.Host.get · L18](ec21ecbd299caaed.md#m26d7e4af4cdffdbb)（访问器）

<a id="m5047a40ae90aa091"></a>

## RestoreDetection · L23

`public void RestoreDetection(bool detected,float remaining)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_detectedByHost=detected;<br/>_detectionRemaining=remaining;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m45857745d20c309b"></a>

## Active.get · L25

`public bool Active`

★ 修复（防Bug回归）：热更新或旧组件残留时，新卵寄生逻辑不能继续触发旧的吸血/跟随行为。

### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["EcologyQueries.Active(_genes,_settings.parasiticGene)?.Definition.LogicScriptId==DarwinPark.Genetics.NutritionEcologySettings.Current.legacyEggLogic"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

项目内调用/回调引用：

- [EcologyQueries.Active · L29](bdf7ccc2c9c0452f.md#m200839bb9db5ec55)（方法）
- [NutritionEcologySettings.Current.get · L19](846a17351dbd6aa4.md#mb3eb930022e314f3)（访问器）

<a id="mb50e6bdc00e42f73"></a>

## Internal.get · L26

`public bool Internal`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["EcologyQueries.Active(_genes,_settings.parasiticGene)?.State?.StateId==_settings.internalState"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

项目内调用/回调引用：

- [EcologyQueries.Active · L29](bdf7ccc2c9c0452f.md#m200839bb9db5ec55)（方法）

<a id="md273b2395ba33966"></a>

## OriginalKinematic.get · L27

`public bool OriginalKinematic`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Host!=null?_kinematic:_body!=null&amp;&amp;_body.isKinematic"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

项目内调用/回调引用：

- [ParasiteBehavior.Host.get · L18](ec21ecbd299caaed.md#m26d7e4af4cdffdbb)（访问器）

<a id="m583105d4b5e5f7bb"></a>

## Awake · L28

`private void Awake()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_genes=GetComponent&lt;EntityGeneController&gt;();<br/>_entity=GetComponent&lt;DarwinEntity&gt;();<br/>_settings=EcologySettings.Load();<br/>EcologyIdentity.Ensure(gameObject);"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [EcologySettings.Load · L18](90428a275d065ef6.md#mb0575490373edaf2)（方法）
- [EcologyIdentity.Ensure · L23](bdf7ccc2c9c0452f.md#m8adfd15f583f225a)（方法）

<a id="md862501ee37e8380"></a>

## Update · L29

`private void Update()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"Advance<br/>判断：ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)"}
  b2["clock.GameDeltaTime"]
  b3["Time.deltaTime"]
  b4["Advance(ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)?clock.GameDeltaTime:Time.deltaTime);"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 --> b5

```

项目内调用/回调引用：

- [ParasiteBehavior.Advance · L31](ec21ecbd299caaed.md#m52dd6e8d78f1a34c)（方法）
- [ServiceLocator.TryGet · L26](f2fdbe9f9ccd93cb.md#m143b24e6d58471b0)（方法）
- [SimulationClock.GameDeltaTime.get · L26](a8f444aec9614b0a.md#me7f11abae18a4f2d)（访问器）

<a id="mdf6475e5137b4c4a"></a>

## OnDisable · L30

`private void OnDisable()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Detach();"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [ParasiteBehavior.Detach · L94](ec21ecbd299caaed.md#m85ba1f015e87ec39)（方法）

<a id="m52dd6e8d78f1a34c"></a>

## Advance · L31

`public void Advance(float dt)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Active"}
  b2["Detach();"]
  b3{"判断：dt&lt;=0"}
  b4{"判断：_body==null"}
  b5["_body=GetComponent&lt;Rigidbody&gt;();"]
  b6{"判断：_attached"}
  b7{"判断：EcologyQueries.Alive(Host)"}
  b8{"判断：_attachedInternal!=Internal"}
  b9["Detach();"]
  b10{"判断：Host!=null"}
  b11["_detectionRemaining-=dt;"]
  b12{"判断：_detectionRemaining&lt;=0"}
  b13["_detectionRemaining+=Mathf.Max(.01f,_settings.hostSearchInterval);<br/>_detectedByHost=Random.value&gt;=Mathf.Clamp01(_genes.GetAttribute(&quot;hide_from_host&quot;));"]
  b14["stats=Host.GetComponent&lt;EntityStats&gt;()<br/>requested=Mathf.Max(0,_genes.GetAttribute(&quot;host_drain_rate&quot;))*dt<br/>stats.TryGet(&quot;nutrient&quot;,out var food);<br/>drained=Mathf.Min(food,requested)<br/>stats.Modify(&quot;nutrient&quot;,-drained);<br/>stats.TryGet(&quot;storedNutri…"]
  b15{"_search-=dt;<br/>判断：_search&lt;=0"}
  b16["_search=_settings.hostSearchInterval;<br/>_candidate=FindHost();"]
  b17{"判断：CanHost(_candidate)"}
  b18{"判断：Vector3.Distance(transform.position,_candidate.transform.position)&lt;=_settings.attachReach+HostRadius(_candidate)"}
  b19["TryAttach(_candidate);"]
  b20{"判断：_body==null"}
  b21{"判断：_body.isKinematic"}
  b22{"planet=EcologyQueries.Planet<br/>destination=_candidate.transform.position<br/>判断：_genes.GetAttribute(&quot;swimSpeed&quot;)&lt;=0"}
  b23{"判断：_route.Resolve(planet,transform.position,destination,out destination)"}
  b24["_body.linearVelocity=Vector3.zero;"]
  b25["up=EcologyQueries.Up(transform.position)<br/>direction=Vector3.ProjectOnPlane(destination-transform.position,up).normalized"]
  b26{"判断：ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)"}
  b27["clock.MotionTimeScale"]
  b28["1"]
  b29["scale=ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)?clock.MotionTimeScale:1"]
  b30{"tangent=direction*_settings.parasiteSeekSpeed*scale<br/>判断：_genes.GetAttribute(&quot;swimSpeed&quot;)&lt;=0"}
  b31["tangent=DarwinPark.Planet.LandRoutePlan.SafeVelocity(planet,transform.position,tangent);"]
  b32["_body.linearVelocity=tangent+up*Mathf.Min(0,Vector3.Dot(_body.linearVelocity,up));"]
  b33(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 --> b33
  b3 -->|"是"| b33
  b3 -->|"否"| b4
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b10
  b7 -->|"是"| b8
  b7 -->|"否"| b9
  b8 -->|"是"| b9
  b8 -->|"否"| b10
  b9 --> b10
  b10 -->|"是"| b11
  b10 -->|"否"| b15
  b11 --> b12
  b12 -->|"是"| b13
  b12 -->|"否"| b14
  b13 --> b12
  b14 --> b33
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 --> b17
  b17 -->|"否"| b33
  b17 -->|"是"| b18
  b18 -->|"是"| b19
  b18 -->|"否"| b20
  b19 --> b33
  b20 -->|"否"| b21
  b20 -->|"是"| b33
  b21 -->|"是"| b33
  b21 -->|"否"| b22
  b22 -->|"是"| b23
  b22 -->|"否"| b25
  b23 -->|"否"| b24
  b23 -->|"是"| b25
  b24 --> b33
  b25 --> b26
  b26 -->|"是"| b27
  b26 -->|"否"| b28
  b27 --> b29
  b28 --> b29
  b29 --> b30
  b30 -->|"是"| b31
  b30 -->|"否"| b32
  b31 --> b32
  b32 --> b33

```

项目内调用/回调引用：

- [ParasiteBehavior.Detach · L94](ec21ecbd299caaed.md#m85ba1f015e87ec39)（方法）
- [EcologyQueries.Alive · L39](bdf7ccc2c9c0452f.md#m8352bfacbe2996a8)（方法）
- [ParasiteBehavior.Host.get · L18](ec21ecbd299caaed.md#m26d7e4af4cdffdbb)（访问器）
- [EntityGeneController.GetAttribute · L288](7494d53178552f0e.md#m45127bcdd833aaf5)（方法）
- [EntityStats.TryGet · L93](75aa86a368efdf24.md#m03cef847022eee28)（方法）
- [EntityStats.Modify · L107](75aa86a368efdf24.md#mf17c464aaa04f6f2)（方法）
- [EntityStats.Feed · L98](75aa86a368efdf24.md#me8ee3fa22434267a)（方法）
- [ParasiteBehavior.Follow · L93](ec21ecbd299caaed.md#mc051bf3a9b9733ce)（方法）
- [ParasiteBehavior.FindHost · L59](ec21ecbd299caaed.md#m5b3425b94bdcab23)（方法）
- [ParasiteBehavior.CanHost · L68](ec21ecbd299caaed.md#m976734fde9bb01e4)（方法）
- [ParasiteBehavior.HostRadius · L77](ec21ecbd299caaed.md#ma9c640d736992744)（方法）
- [ParasiteBehavior.TryAttach · L78](ec21ecbd299caaed.md#m8473a35a233ea83d)（方法）
- [LandRoutePlan.Resolve · L42](d357e13967229560.md#md0b0a64ddf86d978)（方法）
- [EcologyQueries.Up · L42](bdf7ccc2c9c0452f.md#ma73e6bbbc726282f)（方法）
- [ServiceLocator.TryGet · L26](f2fdbe9f9ccd93cb.md#m143b24e6d58471b0)（方法）
- [LandRoutePlan.SafeVelocity · L54](d357e13967229560.md#mdb046d6a448848cc)（方法）

<a id="m5b3425b94bdcab23"></a>

## FindHost · L59

`private DarwinEntity FindHost()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["best=null<br/>distance=_settings.hostSearchRadius*_settings.hostSearchRadius<br/>cost=PerformanceTimings.Measure(PerformanceTimings.Area.ParasiteHostSearch)"]
  b2["EntityCoordinateGrid.Query(transform.position,_settings.hostSearchRadius,_coordinates);"]
  b3["_coordinates"]
  b4{"判断：_coordinates"}
  b5["var"]
  b6{"entity=coordinate.entity<br/>判断：CanHost(entity)"}
  b7{"判断：LandReachability.CanReach(_genes,entity)"}
  b8{"d=(coordinate.position-transform.position).sqrMagnitude<br/>判断：d&lt;distance"}
  b9["best=entity;<br/>distance=d;"]
  b10["_coordinates"]
  b11["结果：best"]
  b12["cost=PerformanceTimings.Measure(PerformanceTimings.Area.ParasiteHostSearch)"]
  b13(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 --> b4
  b4 -->|"是"| b5
  b4 -->|"否 · 经过finally 10"| b11
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b4
  b7 -->|"否"| b4
  b7 -->|"是"| b8
  b8 -->|"是"| b9
  b8 -->|"否"| b4
  b9 --> b4
  b11 -->|"返回 · 经过finally 12"| b13
  b4 -. "退出保护区时 finally（示意）" .-> b10
  b2 -. "退出保护区时 finally（示意）" .-> b12

```

项目内调用/回调引用：

- [PerformanceTimings.Measure · L25](a8403a4c7503726d.md#m36fde3c234084192)（方法）
- [EntityCoordinateGrid.Query · L26](f6d0404a662a5d5c.md#mb223b609771bd310)（方法）
- [ParasiteBehavior.CanHost · L68](ec21ecbd299caaed.md#m976734fde9bb01e4)（方法）
- [LandReachability.CanReach · L15](816416ac988cda79.md#m421ee61c476fabe8)（方法）

<a id="m976734fde9bb01e4"></a>

## CanHost · L68

`private bool CanHost(DarwinEntity entity)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：EcologyQueries.Alive(entity)"}
  b2{"判断：entity==_entity"}
  b3["结果：false"]
  b4{"_entity.GetComponent&lt;EntityStats&gt;().TryGet(&quot;size&quot;,out var own);<br/>entity.GetComponent&lt;EntityStats&gt;().TryGet(&quot;size&quot;,out var size);<br/>判断：size&lt;Mathf.Max(.01f,own)*_settings.minimumHostSizeRatio"}
  b5["结果：false"]
  b6["seen=new HashSet&lt;DarwinEntity&gt;()<br/>current=entity"]
  b7{"判断：current!=null"}
  b8{"判断：current==_entity"}
  b9{"判断：seen.Add(current)"}
  b10["结果：false"]
  b11["current"]
  b12{"DarwinPark.Core.OptionalComponent.Read&lt;ParasiteBehavior&gt;(current)<br/>判断：DarwinPark.Core.OptionalComponent.Read&lt;ParasiteBehavior&gt;(current)"}
  b13[".Host"]
  b14["DarwinPark.Core.OptionalComponent.Read&lt;ParasiteBehavior&gt;(current)"]
  b15["current=DarwinPark.Core.OptionalComponent.Read&lt;ParasiteBehavior&gt;(current)?.Host;"]
  b16["结果：true"]
  b17(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 -->|"返回"| b17
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 -->|"返回"| b17
  b6 --> b7
  b7 -->|"是"| b8
  b7 -->|"否"| b16
  b8 -->|"否"| b9
  b8 -->|"是"| b10
  b9 -->|"否"| b10
  b9 -->|"是"| b11
  b10 -->|"返回"| b17
  b11 --> b12
  b12 -->|"否"| b13
  b12 -->|"是"| b14
  b13 --> b15
  b14 --> b15
  b15 --> b7
  b16 -->|"返回"| b17

```

项目内调用/回调引用：

- [EcologyQueries.Alive · L39](bdf7ccc2c9c0452f.md#m8352bfacbe2996a8)（方法）
- [EntityStats.TryGet · L93](75aa86a368efdf24.md#m03cef847022eee28)（方法）
- [OptionalComponent.Read · L7](806c46b42ced4d36.md#m69622a4dee165c5a)（方法）
- [ParasiteBehavior.Host.get · L18](ec21ecbd299caaed.md#m26d7e4af4cdffdbb)（访问器）

<a id="ma9c640d736992744"></a>

## HostRadius · L77

`private static float HostRadius(DarwinEntity entity)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"entity.GetComponent&lt;Collider&gt;()<br/>判断：entity.GetComponent&lt;Collider&gt;()"}
  b2[".bounds.extents.magnitude"]
  b3["entity.GetComponent&lt;Collider&gt;()"]
  b4{"判断：entity.GetComponent&lt;Collider&gt;()?.bounds.extents.magnitude"}
  b5["entity.GetComponent&lt;Collider&gt;()?.bounds.extents.magnitude"]
  b6["0"]
  b7["结果：entity.GetComponent&lt;Collider&gt;()?.bounds.extents.magnitude??0"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"否"| b5
  b4 -->|"是"| b6
  b5 --> b7
  b6 --> b7
  b7 -->|"返回"| b8

```

<a id="m8473a35a233ea83d"></a>

## TryAttach · L78

`public bool TryAttach(DarwinEntity host,bool restoring=false,Vector3 savedOffset=default,bool originalKinematic=false)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Active"}
  b2{"判断：CanHost(host)"}
  b3["结果：false"]
  b4{"判断：restoring"}
  b5{"判断：Vector3.Distance(transform.position,host.transform.position)&gt;_settings.attachReach+HostRadius(host)"}
  b6["结果：false"]
  b7["Detach();<br/>Host=host;<br/>_attached=true;<br/>_attachedInternal=Internal;<br/>_body=GetComponent&lt;Rigidbody&gt;();"]
  b8{"_detectedByHost<br/>判断：restoring"}
  b9["Random.value&gt;=Mathf.Clamp01(_genes.GetAttribute(&quot;hide_from_host&quot;))"]
  b10["!restoring"]
  b11["_detectedByHost=!restoring&amp;&amp;Random.value&gt;=Mathf.Clamp01(_genes.GetAttribute(&quot;hide_from_host&quot;));"]
  b12{"_detectionRemaining=_settings.hostSearchInterval;<br/>判断：_body!=null"}
  b13{"_kinematic<br/>判断：restoring"}
  b14["originalKinematic"]
  b15["_body.isKinematic"]
  b16["_kinematic=restoring?originalKinematic:_body.isKinematic;"]
  b17{"判断：_body.isKinematic"}
  b18["_body.linearVelocity=Vector3.zero;<br/>_body.angularVelocity=Vector3.zero;"]
  b19["_body.isKinematic=true;"]
  b20{"_offset<br/>判断：restoring"}
  b21["savedOffset"]
  b22{"host.transform<br/>判断：Internal"}
  b23["Vector3.zero"]
  b24["EcologyQueries.Up(host.transform.position)*(HostRadius(host)+.1f)"]
  b25["host.transform.InverseTransformVector(Internal?Vector3.zero:EcologyQueries.Up(host.transform.position)*(HostRadius(host)+.1f))"]
  b26["_offset=restoring?savedOffset:host.transform.InverseTransformVector(Internal?Vector3.zero:EcologyQueries.Up(host.transform.position)*(HostRadius(host)+.1f));"]
  b27["GetComponentsInChildren&lt;Collider&gt;()"]
  b28{"判断：GetComponentsInChildren&lt;Collider&gt;()"}
  b29["var"]
  b30["host.GetComponentsInChildren&lt;Collider&gt;()"]
  b31{"判断：host.GetComponentsInChildren&lt;Collider&gt;()"}
  b32{"var<br/>判断：parasite==collider"}
  b33["Physics.IgnoreCollision(parasite,collider,true);<br/>_ignored.Add((parasite,collider));"]
  b34{"host.GetComponentsInChildren&lt;Collider&gt;()<br/>判断：host.GetComponentsInChildren&lt;Collider&gt;()"}
  b35["host.GetComponentsInChildren&lt;Collider&gt;()"]
  b36["连接 / 离开区域"]
  b37{"GetComponentsInChildren&lt;Collider&gt;()<br/>判断：GetComponentsInChildren&lt;Collider&gt;()"}
  b38["GetComponentsInChildren&lt;Collider&gt;()"]
  b39["连接 / 离开区域"]
  b40{"判断：Internal"}
  b41["GetComponentsInChildren&lt;Renderer&gt;()"]
  b42{"判断：GetComponentsInChildren&lt;Renderer&gt;()"}
  b43["var<br/>_renderers[renderer]=renderer.enabled;<br/>renderer.enabled=false;"]
  b44{"GetComponentsInChildren&lt;Renderer&gt;()<br/>判断：GetComponentsInChildren&lt;Renderer&gt;()"}
  b45["GetComponentsInChildren&lt;Renderer&gt;()"]
  b46["连接 / 离开区域"]
  b47["Follow();<br/>结果：true"]
  b48(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"否"| b3
  b2 -->|"是"| b4
  b3 -->|"返回"| b48
  b4 -->|"否"| b5
  b4 -->|"是"| b7
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 -->|"返回"| b48
  b7 --> b8
  b8 -->|"否"| b9
  b8 -->|"是"| b10
  b9 --> b11
  b10 --> b11
  b11 --> b12
  b12 -->|"是"| b13
  b12 -->|"否"| b20
  b13 -->|"是"| b14
  b13 -->|"否"| b15
  b14 --> b16
  b15 --> b16
  b16 --> b17
  b17 -->|"否"| b18
  b17 -->|"是"| b19
  b18 --> b19
  b19 --> b20
  b20 -->|"是"| b21
  b20 -->|"否"| b22
  b21 --> b26
  b22 -->|"是"| b23
  b22 -->|"否"| b24
  b23 --> b25
  b24 --> b25
  b25 --> b26
  b26 --> b27
  b27 --> b28
  b28 -->|"是"| b29
  b28 -->|"否 · 经过finally 37"| b40
  b29 --> b30
  b30 --> b31
  b31 -->|"是"| b32
  b31 -->|"否 · 经过finally 34"| b28
  b32 -->|"是"| b31
  b32 -->|"否"| b33
  b33 --> b31
  b34 -->|"否"| b35
  b34 -->|"是"| b36
  b35 --> b36
  b37 -->|"否"| b38
  b37 -->|"是"| b39
  b38 --> b39
  b40 -->|"是"| b41
  b40 -->|"否"| b47
  b41 --> b42
  b42 -->|"是"| b43
  b42 -->|"否 · 经过finally 44"| b47
  b43 --> b42
  b44 -->|"否"| b45
  b44 -->|"是"| b46
  b45 --> b46
  b47 -->|"返回"| b48
  b31 -. "退出保护区时 finally（示意）" .-> b34
  b28 -. "退出保护区时 finally（示意）" .-> b37
  b42 -. "退出保护区时 finally（示意）" .-> b44

```

项目内调用/回调引用：

- [ParasiteBehavior.CanHost · L68](ec21ecbd299caaed.md#m976734fde9bb01e4)（方法）
- [ParasiteBehavior.HostRadius · L77](ec21ecbd299caaed.md#ma9c640d736992744)（方法）
- [ParasiteBehavior.Detach · L94](ec21ecbd299caaed.md#m85ba1f015e87ec39)（方法）
- [ParasiteBehavior.Host.set · L18](ec21ecbd299caaed.md#md057cd47821a7aa9)（访问器）
- [EntityGeneController.GetAttribute · L288](7494d53178552f0e.md#m45127bcdd833aaf5)（方法）
- [EcologyQueries.Up · L42](bdf7ccc2c9c0452f.md#ma73e6bbbc726282f)（方法）
- [ParasiteBehavior.Follow · L93](ec21ecbd299caaed.md#mc051bf3a9b9733ce)（方法）

<a id="mdb66a478067005fe"></a>

## LateUpdate · L91

`private void LateUpdate()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Host!=null"}
  b2["Follow();"]
  b3(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3

```

项目内调用/回调引用：

- [ParasiteBehavior.Host.get · L18](ec21ecbd299caaed.md#m26d7e4af4cdffdbb)（访问器）
- [ParasiteBehavior.Follow · L93](ec21ecbd299caaed.md#mc051bf3a9b9733ce)（方法）

<a id="mc051bf3a9b9733ce"></a>

## Follow · L93

`private void Follow()`

★ 适配：宿主跟随可能发生在基因模拟之后，及时同步空间索引。

### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Host!=null"}
  b2["transform.position=Host.transform.position+Host.transform.TransformVector(_offset);<br/>EntityNeighborhood.ObservePosition(GetComponent&lt;EntityGeneController&gt;());"]
  b3(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3

```

项目内调用/回调引用：

- [ParasiteBehavior.Host.get · L18](ec21ecbd299caaed.md#m26d7e4af4cdffdbb)（访问器）
- [EntityNeighborhood.ObservePosition · L81](e5e24c4aff0395b1.md#m232339f32a8c5875)（方法）

<a id="m85ba1f015e87ec39"></a>

## Detach · L94

`public void Detach()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_ignored"]
  b2{"判断：_ignored"}
  b3{"var<br/>判断：pair.parasite!=null"}
  b4{"判断：pair.host!=null"}
  b5["Physics.IgnoreCollision(pair.parasite,pair.host,false);"]
  b6["_ignored"]
  b7["_ignored.Clear();"]
  b8["_renderers"]
  b9{"判断：_renderers"}
  b10{"var<br/>判断：pair.Key!=null"}
  b11["pair.Key.enabled=pair.Value;"]
  b12["_renderers"]
  b13{"_renderers.Clear();<br/>判断：_body!=null"}
  b14{"判断：_attached"}
  b15{"_body.isKinematic=_kinematic;<br/>判断：_body.isKinematic"}
  b16["_body.linearVelocity=Vector3.zero;"]
  b17["_attached=false;<br/>Host=null;<br/>_candidate=null;<br/>_search=0;"]
  b18(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"是"| b3
  b2 -->|"否 · 经过finally 6"| b7
  b3 -->|"是"| b4
  b3 -->|"否"| b2
  b4 -->|"是"| b5
  b4 -->|"否"| b2
  b5 --> b2
  b7 --> b8
  b8 --> b9
  b9 -->|"是"| b10
  b9 -->|"否 · 经过finally 12"| b13
  b10 -->|"是"| b11
  b10 -->|"否"| b9
  b11 --> b9
  b13 -->|"是"| b14
  b13 -->|"否"| b17
  b14 -->|"是"| b15
  b14 -->|"否"| b17
  b15 -->|"否"| b16
  b15 -->|"是"| b17
  b16 --> b17
  b17 --> b18
  b2 -. "退出保护区时 finally（示意）" .-> b6
  b9 -. "退出保护区时 finally（示意）" .-> b12

```

项目内调用/回调引用：

- [ParasiteBehavior.Host.set · L18](ec21ecbd299caaed.md#md057cd47821a7aa9)（访问器）
