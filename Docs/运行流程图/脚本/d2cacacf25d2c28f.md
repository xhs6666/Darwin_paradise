# CorpseCleanupController.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Simulation/CorpseCleanupController.cs`

[源文件](../../../Assets/DarwinPark/Entities/Simulation/CorpseCleanupController.cs) · 源码校验：`b44292e211ae48203250764dd7d231359de622f398ff1651339bc0d6241764d9`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m61919ebdc55e98a7"></a>

## Removed.get · L15

`public static long Removed`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_state.removed"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m3f64ddbfb337da8d"></a>

## Queued.get · L15

`public static int Queued`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Pending.Count"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m8bc7b9171b588afb"></a>

## Elapsed.get · L15

`public static double Elapsed`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_state.elapsed"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mdc28e56427bcd8e8"></a>

## Ensure · L16

`public static void Ensure(GameObject owner)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_instance!=null"}
  b2{"_instance=FindFirstObjectByType&lt;CorpseCleanupController&gt;();<br/>判断：_instance==null"}
  b3["owner.AddComponent&lt;CorpseCleanupController&gt;();"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b4
  b1 -->|"否"| b2
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b4

```

<a id="mb303445d036224f3"></a>

## Awake · L17

`private void Awake()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_instance!=null"}
  b2{"判断：_instance!=this"}
  b3["Destroy(this);"]
  b4["_instance=this;"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b4
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5

```

<a id="mc72c8c5cb39aa2a1"></a>

## OnDestroy · L18

`private void OnDestroy()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_instance==this"}
  b2["_instance=null;"]
  b3(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3

```

<a id="mc95eb27ae586db07"></a>

## Update · L19

`private void Update()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)"}
  b2["clock.GameDeltaTime"]
  b3["Time.deltaTime"]
  b4["Advance(ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)?clock.GameDeltaTime:Time.deltaTime)"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 --> b5

```

项目内调用/回调引用：

- [CorpseCleanupController.Advance · L20](d2cacacf25d2c28f.md#m7469a7ea8c42a6cc)（方法）
- [ServiceLocator.TryGet · L26](f2fdbe9f9ccd93cb.md#m143b24e6d58471b0)（方法）
- [SimulationClock.GameDeltaTime.get · L26](a8f444aec9614b0a.md#me7f11abae18a4f2d)（访问器）

<a id="m7469a7ea8c42a6cc"></a>

## Advance · L20

`public static void Advance(float dt)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"tuning=GameplayTuning.Current<br/>判断：dt&lt;=0"}
  b2{"判断：tuning.corpseCleanupEnabled"}
  b3{"判断：WorldAuthority.IsReplica"}
  b4{"判断：WorldRuntimeGate.Managed"}
  b5{"判断：WorldRuntimeGate.Ready"}
  b6{"判断：WorldRuntimeGate.Loading"}
  b7["performance=PerformanceTimings.Measure(PerformanceTimings.Area.CorpseCleanup)"]
  b8{"_state.elapsed+=dt;<br/>判断：Pending.Count==0"}
  b9{"判断：_state.elapsed&gt;=tuning.corpseCleanupSeconds"}
  b10["_state.elapsed-=tuning.corpseCleanupSeconds;<br/>_state.cycles++;"]
  b11["ColonyResource.All"]
  b12{"判断：ColonyResource.All"}
  b13{"var<br/>判断：resource.State.kind==ColonyResourceKind.Corpse"}
  b14{"判断：resource.HasLiveClaim"}
  b15["Pending.Enqueue(resource.State.id);"]
  b16{"判断：ColonyResource.All"}
  b17["ColonyResource.All"]
  b18["连接 / 离开区域"]
  b19["budget=tuning.corpseCleanupPerFrame"]
  b20{"判断：budget--&gt;0"}
  b21{"判断：Pending.Count&gt;0"}
  b22{"resource=ColonyResource.Find(Pending.Dequeue())<br/>判断：resource==null"}
  b23{"判断：resource.State.kind!=ColonyResourceKind.Corpse"}
  b24{"判断：resource.HasLiveClaim"}
  b25["resource.Remove();<br/>_state.removed++;"]
  b26["performance=PerformanceTimings.Measure(PerformanceTimings.Area.CorpseCleanup)"]
  b27(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b27
  b2 -->|"是"| b3
  b2 -->|"否"| b27
  b3 -->|"否"| b4
  b3 -->|"是"| b27
  b4 -->|"是"| b5
  b4 -->|"否"| b7
  b5 -->|"是"| b6
  b5 -->|"否"| b27
  b6 -->|"是"| b27
  b6 -->|"否"| b7
  b7 --> b8
  b8 -->|"是"| b9
  b8 -->|"否"| b19
  b9 -->|"是"| b10
  b9 -->|"否"| b19
  b10 --> b11
  b11 --> b12
  b12 -->|"是"| b13
  b12 -->|"否 · 经过finally 16"| b19
  b13 -->|"是"| b14
  b13 -->|"否"| b12
  b14 -->|"否"| b15
  b14 -->|"是"| b12
  b15 --> b12
  b16 -->|"否"| b17
  b16 -->|"是"| b18
  b17 --> b18
  b19 --> b20
  b20 -->|"是"| b21
  b20 -->|"否 · 经过finally 26"| b27
  b21 -->|"是"| b22
  b21 -->|"否 · 经过finally 26"| b27
  b22 -->|"否"| b23
  b22 -->|"是"| b20
  b23 -->|"否"| b24
  b23 -->|"是"| b20
  b24 -->|"是"| b20
  b24 -->|"否"| b25
  b25 --> b20
  b12 -. "退出保护区时 finally（示意）" .-> b16
  b8 -. "退出保护区时 finally（示意）" .-> b26

```

项目内调用/回调引用：

- [GameplayTuning.Current.get · L46](db546e5713b32d53.md#m4fa691bd8a3b9618)（访问器）
- [PerformanceTimings.Measure · L25](a8403a4c7503726d.md#m36fde3c234084192)（方法）
- [ColonyResource.State.get · L24](293dbaecfff4b585.md#m41824f1a0e160aa5)（访问器）
- [ColonyResource.Find · L28](293dbaecfff4b585.md#m1324285ab40ac6f4)（方法）
- [ColonyResource.Remove · L108](293dbaecfff4b585.md#m42fd78441247c433)（方法）

<a id="m62a8c47f3240774f"></a>

## Capture · L37

`public static CorpseCleanupSave Capture()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["copy=JsonUtility.FromJson&lt;CorpseCleanupSave&gt;(JsonUtility.ToJson(_state))<br/>copy.pending=new List&lt;string&gt;(Pending);<br/>结果：copy"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m88264fca2e08063a"></a>

## Restore · L38

`public static void Restore(CorpseCleanupSave state)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Validate(state);<br/>Pending.Clear();"]
  b2{"_state<br/>判断：state==null"}
  b3{"判断：state.version==0"}
  b4["new()"]
  b5["JsonUtility.FromJson&lt;CorpseCleanupSave&gt;(JsonUtility.ToJson(state))"]
  b6["_state=state==null&#124;&#124;state.version==0?new():JsonUtility.FromJson&lt;CorpseCleanupSave&gt;(JsonUtility.ToJson(state));"]
  b7["_state.pending"]
  b8{"判断：_state.pending"}
  b9["var<br/>Pending.Enqueue(id);"]
  b10["_state.pending"]
  b11["_state.pending.Clear();"]
  b12(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"否"| b3
  b2 -->|"是"| b4
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b6
  b5 --> b6
  b6 --> b7
  b7 --> b8
  b8 -->|"是"| b9
  b8 -->|"否 · 经过finally 10"| b11
  b9 --> b8
  b11 --> b12
  b8 -. "退出保护区时 finally（示意）" .-> b10

```

项目内调用/回调引用：

- [CorpseCleanupController.Validate · L40](d2cacacf25d2c28f.md#m881c458a99fc0e08)（方法）

<a id="m881c458a99fc0e08"></a>

## Validate · L40

`public static void Validate(CorpseCleanupSave state)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：state==null"}
  b2{"判断：state.version==0"}
  b3{"判断：state.elapsed==0"}
  b4{"判断：state.cycles==0"}
  b5{"判断：state.removed==0"}
  b6{"判断：state.pending==null"}
  b7{"判断：state.pending.Count==0"}
  b8{"判断：state.version!=1"}
  b9{"判断：double.IsNaN(state.elapsed)"}
  b10{"判断：double.IsInfinity(state.elapsed)"}
  b11{"判断：state.elapsed&lt;0"}
  b12{"判断：state.cycles&lt;0"}
  b13{"判断：state.removed&lt;0"}
  b14{"判断：state.pending==null"}
  b15["结果：new InvalidDataException(&quot;尸体清理规则进度无效&quot;)"]
  x15(["抛出异常 / 离开方法"])
  b16["ids=new HashSet&lt;string&gt;()"]
  b17["state.pending"]
  b18{"判断：state.pending"}
  b19{"var<br/>判断：Guid.TryParseExact(id,&quot;N&quot;,out _)"}
  b20{"判断：ids.Add(id)"}
  b21["结果：new InvalidDataException(&quot;尸体清理名单无效或重复&quot;)"]
  x21(["抛出异常 / 离开方法"])
  b22["state.pending"]
  b23(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b23
  b1 -->|"否"| b2
  b2 -->|"是"| b3
  b2 -->|"否"| b8
  b3 -->|"是"| b4
  b3 -->|"否"| b8
  b4 -->|"是"| b5
  b4 -->|"否"| b8
  b5 -->|"是"| b6
  b5 -->|"否"| b8
  b6 -->|"否"| b7
  b6 -->|"是"| b23
  b7 -->|"是"| b23
  b7 -->|"否"| b8
  b8 -->|"否"| b9
  b8 -->|"是"| b15
  b9 -->|"否"| b10
  b9 -->|"是"| b15
  b10 -->|"否"| b11
  b10 -->|"是"| b15
  b11 -->|"否"| b12
  b11 -->|"是"| b15
  b12 -->|"否"| b13
  b12 -->|"是"| b15
  b13 -->|"否"| b14
  b13 -->|"是"| b15
  b14 -->|"是"| b15
  b14 -->|"否"| b16
  b15 -->|"Throw"| x15
  b16 --> b17
  b17 --> b18
  b18 -->|"是"| b19
  b18 -->|"否 · 经过finally 22"| b23
  b19 -->|"是"| b20
  b19 -->|"否"| b21
  b20 -->|"否"| b21
  b20 -->|"是"| b18
  b21 -->|"Throw"| x21
  b18 -. "退出保护区时 finally（示意）" .-> b22

```

<a id="m7b7025df84dce0ca"></a>

## Reset · L46

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_instance=null;<br/>Restore(null);"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [CorpseCleanupController.Restore · L38](d2cacacf25d2c28f.md#m88264fca2e08063a)（方法）
