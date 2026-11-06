# ParasiteBehavior.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Ecology/ParasiteBehavior.cs`

[源文件](../../../Assets/DarwinPark/Entities/Ecology/ParasiteBehavior.cs) · 源码校验：`8402596d68452419fe6870c9cd232551ba645e80c2982c39f35c72eff09be571`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m854b55763184d078"></a>

## Host.get · L16

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

<a id="mb11b1f8f75e0ff5c"></a>

## Host.set · L16

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

<a id="mab6a402b1bce3096"></a>

## Offset.get · L17

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

<a id="mb64276e3de46de12"></a>

## DetectedByHost.get · L18

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

<a id="m88caae5cb66292ed"></a>

## DetectionRemaining.get · L19

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

<a id="me3b68e7667800a21"></a>

## VisibleTo · L20

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

- [ParasiteBehavior.Host.get · L16](ec21ecbd299caaed.md#m854b55763184d078)（访问器）

<a id="m40a5d06b41539454"></a>

## RestoreDetection · L21

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

<a id="m1da8e8fa9a54e121"></a>

## Active.get · L23

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

- [EcologyQueries.Active · L28](bdf7ccc2c9c0452f.md#m4fb10ad424167e3b)（方法）
- [NutritionEcologySettings.Current.get · L19](846a17351dbd6aa4.md#mb3eb930022e314f3)（访问器）

<a id="m65972a59ce611781"></a>

## Internal.get · L24

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

- [EcologyQueries.Active · L28](bdf7ccc2c9c0452f.md#m4fb10ad424167e3b)（方法）

<a id="m165db6c6d3d0933e"></a>

## OriginalKinematic.get · L25

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

- [ParasiteBehavior.Host.get · L16](ec21ecbd299caaed.md#m854b55763184d078)（访问器）

<a id="mecdc144500af3a24"></a>

## Awake · L26

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
- [EcologyIdentity.Ensure · L22](bdf7ccc2c9c0452f.md#m40609054a6d444d0)（方法）

<a id="m92afb1ae749564d1"></a>

## Update · L27

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

- [ParasiteBehavior.Advance · L29](ec21ecbd299caaed.md#m77af337c4ab30f62)（方法）
- [ServiceLocator.TryGet · L26](f2fdbe9f9ccd93cb.md#m143b24e6d58471b0)（方法）
- [SimulationClock.GameDeltaTime.get · L26](a8f444aec9614b0a.md#me7f11abae18a4f2d)（访问器）

<a id="m489548e8990ec4e2"></a>

## OnDisable · L28

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

- [ParasiteBehavior.Detach · L86](ec21ecbd299caaed.md#m858d10391da0e50c)（方法）

<a id="m77af337c4ab30f62"></a>

## Advance · L29

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
  b22["up=EcologyQueries.Up(transform.position)<br/>direction=Vector3.ProjectOnPlane(_candidate.transform.position-transform.position,up).normalized"]
  b23{"判断：ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)"}
  b24["clock.MotionTimeScale"]
  b25["1"]
  b26["scale=ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)?clock.MotionTimeScale:1"]
  b27["_body.linearVelocity=direction*_settings.parasiteSeekSpeed*scale+up*Mathf.Min(0,Vector3.Dot(_body.linearVelocity,up));"]
  b28(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 --> b28
  b3 -->|"是"| b28
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
  b14 --> b28
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 --> b17
  b17 -->|"否"| b28
  b17 -->|"是"| b18
  b18 -->|"是"| b19
  b18 -->|"否"| b20
  b19 --> b28
  b20 -->|"否"| b21
  b20 -->|"是"| b28
  b21 -->|"是"| b28
  b21 -->|"否"| b22
  b22 --> b23
  b23 -->|"是"| b24
  b23 -->|"否"| b25
  b24 --> b26
  b25 --> b26
  b26 --> b27
  b27 --> b28

```

项目内调用/回调引用：

- [ParasiteBehavior.Detach · L86](ec21ecbd299caaed.md#m858d10391da0e50c)（方法）
- [EcologyQueries.Alive · L38](bdf7ccc2c9c0452f.md#mb2877c487f449970)（方法）
- [ParasiteBehavior.Host.get · L16](ec21ecbd299caaed.md#m854b55763184d078)（访问器）
- [EntityGeneController.GetAttribute · L225](7494d53178552f0e.md#mc707943b1138a009)（方法）
- [EntityStats.TryGet · L90](75aa86a368efdf24.md#m24ee1433e4a70d94)（方法）
- [EntityStats.Modify · L104](75aa86a368efdf24.md#m858c2a75d852ecc1)（方法）
- [EntityStats.Feed · L95](75aa86a368efdf24.md#mc025822ff1eb86a0)（方法）
- [ParasiteBehavior.Follow · L85](ec21ecbd299caaed.md#m773aeb15453932c0)（方法）
- [ParasiteBehavior.FindHost · L53](ec21ecbd299caaed.md#ma2477ff63c367952)（方法）
- [ParasiteBehavior.CanHost · L60](ec21ecbd299caaed.md#mfe0f00ac7c08fc7a)（方法）
- [ParasiteBehavior.HostRadius · L69](ec21ecbd299caaed.md#m502aac50801d7dc6)（方法）
- [ParasiteBehavior.TryAttach · L70](ec21ecbd299caaed.md#mbdecb89dac39aa1f)（方法）
- [EcologyQueries.Up · L41](bdf7ccc2c9c0452f.md#maca7070196f125fd)（方法）
- [ServiceLocator.TryGet · L26](f2fdbe9f9ccd93cb.md#m143b24e6d58471b0)（方法）

<a id="ma2477ff63c367952"></a>

## FindHost · L53

`private DarwinEntity FindHost()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["best=null<br/>distance=_settings.hostSearchRadius*_settings.hostSearchRadius"]
  b2["EntityRegistry.All"]
  b3{"判断：EntityRegistry.All"}
  b4["var"]
  b5{"判断：CanHost(entity)"}
  b6{"d=(entity.transform.position-transform.position).sqrMagnitude<br/>判断：d&lt;distance"}
  b7["best=entity;<br/>distance=d;"]
  b8{"判断：EntityRegistry.All"}
  b9["EntityRegistry.All"]
  b10["连接 / 离开区域"]
  b11["结果：best"]
  b12(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否 · 经过finally 8"| b11
  b4 --> b5
  b5 -->|"否"| b3
  b5 -->|"是"| b6
  b6 -->|"是"| b7
  b6 -->|"否"| b3
  b7 --> b3
  b8 -->|"否"| b9
  b8 -->|"是"| b10
  b9 --> b10
  b11 -->|"返回"| b12
  b3 -. "退出保护区时 finally（示意）" .-> b8

```

项目内调用/回调引用：

- [ParasiteBehavior.CanHost · L60](ec21ecbd299caaed.md#mfe0f00ac7c08fc7a)（方法）

<a id="mfe0f00ac7c08fc7a"></a>

## CanHost · L60

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
  b12{"current.GetComponent&lt;ParasiteBehavior&gt;()<br/>判断：current.GetComponent&lt;ParasiteBehavior&gt;()"}
  b13[".Host"]
  b14["current.GetComponent&lt;ParasiteBehavior&gt;()"]
  b15["current=current.GetComponent&lt;ParasiteBehavior&gt;()?.Host;"]
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

- [EcologyQueries.Alive · L38](bdf7ccc2c9c0452f.md#mb2877c487f449970)（方法）
- [EntityStats.TryGet · L90](75aa86a368efdf24.md#m24ee1433e4a70d94)（方法）
- [ParasiteBehavior.Host.get · L16](ec21ecbd299caaed.md#m854b55763184d078)（访问器）

<a id="m502aac50801d7dc6"></a>

## HostRadius · L69

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

<a id="mbdecb89dac39aa1f"></a>

## TryAttach · L70

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

- [ParasiteBehavior.CanHost · L60](ec21ecbd299caaed.md#mfe0f00ac7c08fc7a)（方法）
- [ParasiteBehavior.HostRadius · L69](ec21ecbd299caaed.md#m502aac50801d7dc6)（方法）
- [ParasiteBehavior.Detach · L86](ec21ecbd299caaed.md#m858d10391da0e50c)（方法）
- [ParasiteBehavior.Host.set · L16](ec21ecbd299caaed.md#mb11b1f8f75e0ff5c)（访问器）
- [EntityGeneController.GetAttribute · L225](7494d53178552f0e.md#mc707943b1138a009)（方法）
- [EcologyQueries.Up · L41](bdf7ccc2c9c0452f.md#maca7070196f125fd)（方法）
- [ParasiteBehavior.Follow · L85](ec21ecbd299caaed.md#m773aeb15453932c0)（方法）

<a id="mc6d24cdaa44999b8"></a>

## LateUpdate · L83

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

- [ParasiteBehavior.Host.get · L16](ec21ecbd299caaed.md#m854b55763184d078)（访问器）
- [ParasiteBehavior.Follow · L85](ec21ecbd299caaed.md#m773aeb15453932c0)（方法）

<a id="m773aeb15453932c0"></a>

## Follow · L85

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

- [ParasiteBehavior.Host.get · L16](ec21ecbd299caaed.md#m854b55763184d078)（访问器）
- [EntityNeighborhood.ObservePosition · L63](e5e24c4aff0395b1.md#m994f4a7ab30bcd1d)（方法）

<a id="m858d10391da0e50c"></a>

## Detach · L86

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

- [ParasiteBehavior.Host.set · L16](ec21ecbd299caaed.md#mb11b1f8f75e0ff5c)（访问器）
