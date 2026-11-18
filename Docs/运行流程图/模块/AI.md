# AI 模块流程

[返回总索引](../README.md) · [离线交互手册](../index.html)

## 脚本目录

- [Assets/DarwinPark/AI/EntityAIContext.cs](#s99b6d9053b41a538)（0个方法/回调）
- [Assets/DarwinPark/AI/EntityAIController.cs](#sca7f038c139e16a4)（5个方法/回调）
- [Assets/DarwinPark/AI/GeneAIModuleRegistry.cs](#s58bd7c2d48df02e1)（6个方法/回调）
- [Assets/DarwinPark/AI/IGeneAIModule.cs](#s1d1cab401bb81820)（4个方法/回调）
- [Assets/DarwinPark/AI/Modules/BuiltinAIModules.cs](#sb5ed539c7f743461)（8个方法/回调）

---

<a id="s99b6d9053b41a538"></a>

## EntityAIContext.cs

`Runtime` · `Assets/DarwinPark/AI/EntityAIContext.cs`

[源文件](../../../Assets/DarwinPark/AI/EntityAIContext.cs) · 源码校验：`92d967a05e7105ef74969539790efa6b069f531296e09d7894b44fbb030cf23b`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

本文件没有显式方法体，属于数据/枚举/标记声明；字段与类型定义请在离线源码视图查看。


---

<a id="sca7f038c139e16a4"></a>

## EntityAIController.cs

`Runtime` · `Assets/DarwinPark/AI/EntityAIController.cs`

[源文件](../../../Assets/DarwinPark/AI/EntityAIController.cs) · 源码校验：`fc86ad0efd65d1138a8049308c339bb172fedb9785f83204f6a4beae37f5571b`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

<a id="madfd035a7a600128"></a>

### Awake · L20

`private void Awake()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_genes = GetComponent&lt;EntityGeneController&gt;();"]
  b2["_context<br/>new EntityAIContext { Entity = GetComponent&lt;DarwinEntity&gt;(), Genes = _genes }<br/>Entity = GetComponent&lt;DarwinEntity&gt;()<br/>Genes = _genes<br/>_context = new EntityAIContext { Entity = GetComponent&lt;DarwinEntity&gt;(), Genes = _genes };"]
  b3["_genes.OnEvaluationChanged += OnGenesChanged;"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 --> b4

```

项目内调用/回调引用：

- [EntityAIController.OnGenesChanged · L28](AI.md#m6fb2b40bd1a36f95)（方法）

<a id="ma452e87ac884af81"></a>

### Start · L26

`private void Start()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["OnGenesChanged(_genes.Evaluation);"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [EntityAIController.OnGenesChanged · L28](AI.md#m6fb2b40bd1a36f95)（方法）
- [EntityGeneController.Evaluation.get · L20](Entities.md#mf275f100ea21601a)（访问器）

<a id="m6fb2b40bd1a36f95"></a>

### OnGenesChanged · L28

`private void OnGenesChanged(GenomeEvaluation evaluation)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：evaluation == null"}
  b2["GeneAIDecisionService.Collect(evaluation, _desiredModules);<br/>_removedModules.Clear();"]
  b3["_activeModules.Keys"]
  b4{"判断：_activeModules.Keys"}
  b5{"var<br/>判断：_desiredModules.Contains(id)"}
  b6["_removedModules.Add(id);"]
  b7["_activeModules.Keys"]
  b8["_removedModules"]
  b9{"判断：_removedModules"}
  b10["var"]
  b11["module = _activeModules[id]<br/>_activeModules.Remove(id);<br/>module.OnDisable();"]
  b12["_removedModules"]
  b13["_desiredModules"]
  b14{"判断：_desiredModules"}
  b15["var"]
  b16{"判断：_activeModules.ContainsKey(id)"}
  b17{"module = GeneAIModuleRegistry.Create(id)<br/>判断：module == null"}
  b18["module.Initialize(_context);<br/>_activeModules.Add(id, module);"]
  b19["_desiredModules"]
  b20["_snapshotDirty = true;<br/>ticks = false"]
  b21["_activeModules.Values"]
  b22{"判断：_activeModules.Values"}
  b23{"var<br/>判断：module is IPassiveGeneAIModule"}
  b24["ticks = true;"]
  b25["_activeModules.Values"]
  b26{"判断：ticks"}
  b27{"判断：enabled"}
  b28["_sleeping = true;<br/>enabled = false;"]
  b29{"判断：ticks"}
  b30{"判断：_sleeping"}
  b31["_sleeping = false;<br/>enabled = true;"]
  b32(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b32
  b1 -->|"否"| b2
  b2 --> b3
  b3 --> b4
  b4 -->|"是"| b5
  b4 -->|"否 · 经过finally 7"| b8
  b5 -->|"否"| b6
  b5 -->|"是"| b4
  b6 --> b4
  b8 --> b9
  b9 -->|"是"| b10
  b9 -->|"否 · 经过finally 12"| b13
  b10 --> b11
  b11 --> b9
  b13 --> b14
  b14 -->|"是"| b15
  b14 -->|"否 · 经过finally 19"| b20
  b15 --> b16
  b16 -->|"是"| b14
  b16 -->|"否"| b17
  b17 -->|"是"| b14
  b17 -->|"否"| b18
  b18 --> b14
  b20 --> b21
  b21 --> b22
  b22 -->|"是"| b23
  b22 -->|"否 · 经过finally 25"| b26
  b23 -->|"否"| b24
  b23 -->|"是"| b22
  b24 -->|"经过finally 25"| b26
  b26 -->|"否"| b27
  b26 -->|"是"| b29
  b27 -->|"是"| b28
  b27 -->|"否"| b29
  b28 --> b32
  b29 -->|"是"| b30
  b29 -->|"否"| b32
  b30 -->|"是"| b31
  b30 -->|"否"| b32
  b31 --> b32
  b4 -. "退出保护区时 finally（示意）" .-> b7
  b9 -. "退出保护区时 finally（示意）" .-> b12
  b14 -. "退出保护区时 finally（示意）" .-> b19
  b22 -. "退出保护区时 finally（示意）" .-> b25

```

项目内调用/回调引用：

- [GeneAIDecisionService.Collect · L26](AI.md#m4adc27b5d1ec883e)（方法）
- [IGeneAIModule.OnDisable · L11](AI.md#m86b9146578be701b)（方法）
- [GeneAIModuleRegistry.Create · L17](AI.md#me23b2681b42c7371)（方法）
- [IGeneAIModule.Initialize · L9](AI.md#m870ffaac0a27d013)（方法）

<a id="m6ee480c0a857cdef"></a>

### Update · L53

`private void Update()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：WorldAuthority.IsReplica"}
  b2{"判断：ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)"}
  b3["clock.GameDeltaTime"]
  b4["Time.deltaTime"]
  b5["dt = ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock) ? clock.GameDeltaTime : Time.deltaTime"]
  b6{"判断：dt &lt;= 0"}
  b7{"判断：_snapshotDirty"}
  b8["_tickModules.Clear();"]
  b9["_activeModules"]
  b10{"判断：_activeModules"}
  b11{"var<br/>判断：pair.Value is IPassiveGeneAIModule"}
  b12["_tickModules.Add(pair);"]
  b13["_activeModules"]
  b14["_snapshotDirty = false;"]
  b15["_tickModules"]
  b16{"判断：_tickModules"}
  b17["var"]
  b18{"判断：_activeModules.TryGetValue(pair.Key, out var current)"}
  b19{"判断：ReferenceEquals(current, pair.Value)"}
  b20["current.Tick(dt);"]
  b21["_tickModules"]
  b22(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b22
  b1 -->|"否"| b2
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5
  b5 --> b6
  b6 -->|"是"| b22
  b6 -->|"否"| b7
  b7 -->|"是"| b8
  b7 -->|"否"| b15
  b8 --> b9
  b9 --> b10
  b10 -->|"是"| b11
  b10 -->|"否 · 经过finally 13"| b14
  b11 -->|"否"| b12
  b11 -->|"是"| b10
  b12 --> b10
  b14 --> b15
  b15 --> b16
  b16 -->|"是"| b17
  b16 -->|"否 · 经过finally 21"| b22
  b17 --> b18
  b18 -->|"是"| b19
  b18 -->|"否"| b16
  b19 -->|"是"| b20
  b19 -->|"否"| b16
  b20 --> b16
  b10 -. "退出保护区时 finally（示意）" .-> b13
  b16 -. "退出保护区时 finally（示意）" .-> b21

```

项目内调用/回调引用：

- [ServiceLocator.TryGet · L26](Core.md#m143b24e6d58471b0)（方法）
- [SimulationClock.GameDeltaTime.get · L26](Core.md#me7f11abae18a4f2d)（访问器）
- [IGeneAIModule.Tick · L10](AI.md#mf9c0b116b922ed11)（方法）

<a id="ma43bb43bf49bca3b"></a>

### OnDestroy · L69

`private void OnDestroy()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_genes != null"}
  b2["_genes.OnEvaluationChanged -= OnGenesChanged;"]
  b3["_activeModules.Values"]
  b4{"判断：_activeModules.Values"}
  b5["var<br/>module.OnDisable();"]
  b6["_activeModules.Values"]
  b7["_activeModules.Clear();<br/>_tickModules.Clear();"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3
  b3 --> b4
  b4 -->|"是"| b5
  b4 -->|"否 · 经过finally 6"| b7
  b5 --> b4
  b7 --> b8
  b4 -. "退出保护区时 finally（示意）" .-> b6

```

项目内调用/回调引用：

- [IGeneAIModule.OnDisable · L11](AI.md#m86b9146578be701b)（方法）
- [EntityAIController.OnGenesChanged · L28](AI.md#m6fb2b40bd1a36f95)（方法）


---

<a id="s58bd7c2d48df02e1"></a>

## GeneAIModuleRegistry.cs

`Runtime` · `Assets/DarwinPark/AI/GeneAIModuleRegistry.cs`

[源文件](../../../Assets/DarwinPark/AI/GeneAIModuleRegistry.cs) · 源码校验：`c97361a535baeb0e7808b7f9d449eb72e73c8a14c80f5333632b9655273bb8ee`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

<a id="m3d487202d25dffa3"></a>

### 回调@12 · L12

`( … => 回调`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["new Modules.FlightAIModule()"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m40762356dba3abef"></a>

### 回调@13 · L13

`( … => 回调`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["new Modules.AutotrophAIModule()"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m57270d33d3f557a6"></a>

### Register · L15

`public static void Register(string moduleId, Func<IGeneAIModule> factory)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：string.IsNullOrEmpty(moduleId)"}
  b2{"判断：factory != null"}
  b3["Factories[moduleId] = factory;"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b4
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b4

```

<a id="me23b2681b42c7371"></a>

### Create · L17

`public static IGeneAIModule Create(string moduleId)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：moduleId != null"}
  b2{"判断：Factories.TryGetValue(moduleId, out var factory)"}
  b3["factory()"]
  b4["null"]
  b5["结果：moduleId != null &amp;&amp; Factories.TryGetValue(moduleId, out var factory) ? factory() : null"]
  b6(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b4
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5
  b5 -->|"返回"| b6

```

<a id="mc80721b2ab051be4"></a>

### DecideActiveModules · L24

`public static HashSet<string> DecideActiveModules(GenomeEvaluation evaluation)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["result = new HashSet&lt;string&gt;()<br/>Collect(evaluation, result);<br/>结果：result"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [GeneAIDecisionService.Collect · L26](AI.md#m4adc27b5d1ec883e)（方法）

<a id="m4adc27b5d1ec883e"></a>

### Collect · L26

`public static void Collect(GenomeEvaluation evaluation, HashSet<string> result)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"result.Clear();<br/>判断：evaluation == null"}
  b2["evaluation.Genes"]
  b3{"判断：evaluation.Genes"}
  b4{"var<br/>判断：gene.IsActive"}
  b5{"判断：string.IsNullOrEmpty(gene.Definition.AiModuleId)"}
  b6["result.Add(gene.Definition.AiModuleId);"]
  b7["evaluation.Genes"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b8
  b1 -->|"否"| b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否 · 经过finally 7"| b8
  b4 -->|"是"| b5
  b4 -->|"否"| b3
  b5 -->|"否"| b6
  b5 -->|"是"| b3
  b6 --> b3
  b3 -. "退出保护区时 finally（示意）" .-> b7

```


---

<a id="s1d1cab401bb81820"></a>

## IGeneAIModule.cs

`Runtime` · `Assets/DarwinPark/AI/IGeneAIModule.cs`

[源文件](../../../Assets/DarwinPark/AI/IGeneAIModule.cs) · 源码校验：`6cb7360521c46b72da299855966b04b4b9dfd2246855559deca21b0e6c6b4036`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

<a id="mf605b4cf109e56fe"></a>

### ModuleId.get · L8

`get`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m870ffaac0a27d013"></a>

### Initialize · L9

`void Initialize(EntityAIContext context)`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mf9c0b116b922ed11"></a>

### Tick · L10

`void Tick(float deltaTime)`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m86b9146578be701b"></a>

### OnDisable · L11

`void OnDisable()`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```


---

<a id="sb5ed539c7f743461"></a>

## BuiltinAIModules.cs

`Runtime` · `Assets/DarwinPark/AI/Modules/BuiltinAIModules.cs`

[源文件](../../../Assets/DarwinPark/AI/Modules/BuiltinAIModules.cs) · 源码校验：`54b2dfe5e85fd90e015847d35679c6801d1333ac5965df016cecca3977d27a15`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

<a id="m95e7e5d4ddc4d991"></a>

### ModuleId.get · L6

`public string ModuleId`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["&quot;ai_flight&quot;"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="ma10b2716f80bbaed"></a>

### Initialize · L7

`public void Initialize(EntityAIContext context)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1(["结束 / 返回"])
  b0 --> b1

```

<a id="m1e0ab50d7fcfc95c"></a>

### Tick · L8

`public void Tick(float deltaTime)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1(["结束 / 返回"])
  b0 --> b1

```

<a id="ma66db427f28ee87b"></a>

### OnDisable · L9

`public void OnDisable()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1(["结束 / 返回"])
  b0 --> b1

```

<a id="m633b6ba6bf971b21"></a>

### ModuleId.get · L13

`public string ModuleId`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["&quot;ai_autotroph&quot;"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m321d2ce70cb3b960"></a>

### Initialize · L14

`public void Initialize(EntityAIContext context)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1(["结束 / 返回"])
  b0 --> b1

```

<a id="m38f24202d898f22e"></a>

### Tick · L15

`public void Tick(float deltaTime)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1(["结束 / 返回"])
  b0 --> b1

```

<a id="m07fe534f0e209580"></a>

### OnDisable · L16

`public void OnDisable()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1(["结束 / 返回"])
  b0 --> b1

```
