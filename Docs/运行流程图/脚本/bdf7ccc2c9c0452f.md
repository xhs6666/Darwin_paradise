# EcologyIdentity.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Ecology/EcologyIdentity.cs`

[源文件](../../../Assets/DarwinPark/Entities/Ecology/EcologyIdentity.cs) · 源码校验：`2cc514412f215bbf010244af1f9f8ed3424184b3f9ce3392c964ee396ddbd4cb`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m7ac96c56a47a45d3"></a>

## Id.get · L15

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

<a id="mfb0d5b1814d3e594"></a>

## Id.set · L15

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

<a id="me29a2247bba15e88"></a>

## OnEnable · L16

`private void OnEnable()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_entity=GetComponent&lt;DarwinEntity&gt;();<br/>Identities[Id]=this;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [EcologyIdentity.Id.get · L15](bdf7ccc2c9c0452f.md#m7ac96c56a47a45d3)（访问器）

<a id="m360f829a5f85f872"></a>

## OnDisable · L17

`private void OnDisable()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Identities.TryGetValue(Id,out var current)"}
  b2{"判断：current==this"}
  b3["Identities.Remove(Id);"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b4
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b4

```

项目内调用/回调引用：

- [EcologyIdentity.Id.get · L15](bdf7ccc2c9c0452f.md#m7ac96c56a47a45d3)（访问器）

<a id="m98cb86822bff605e"></a>

## Reset · L18

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Identities.Clear()"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m4b6a41779b342bc3"></a>

## Restore · L19

`public void Restore(string id)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Guid.TryParseExact(id,&quot;N&quot;,out _)"}
  b2{"OnDisable();<br/>Id=id;<br/>判断：isActiveAndEnabled"}
  b3["OnEnable();"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b4
  b1 -->|"是"| b2
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b4

```

项目内调用/回调引用：

- [EcologyIdentity.OnDisable · L17](bdf7ccc2c9c0452f.md#m360f829a5f85f872)（方法）
- [EcologyIdentity.Id.set · L15](bdf7ccc2c9c0452f.md#mfb0d5b1814d3e594)（访问器）
- [EcologyIdentity.OnEnable · L16](bdf7ccc2c9c0452f.md#me29a2247bba15e88)（方法）

<a id="mb670b64dc2baff58"></a>

## Find · L21

`public static DarwinEntity Find(string id)`

★ 修复（防Bug回归）：运输每帧解析实体 ID，不能为每名工匠再次遍历全部实体；恢复 ID 和停用时同步更新索引。

### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：string.IsNullOrEmpty(id)"}
  b2{"判断：Identities.TryGetValue(id,out var identity)"}
  b3{"判断：identity!=null"}
  b4{"判断：identity.isActiveAndEnabled"}
  b5["identity._entity"]
  b6["null"]
  b7["结果：!string.IsNullOrEmpty(id)&amp;&amp;Identities.TryGetValue(id,out var identity)&amp;&amp;identity!=null&amp;&amp;identity.isActiveAndEnabled?identity._entity:null"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b6
  b2 -->|"是"| b3
  b2 -->|"否"| b6
  b3 -->|"是"| b4
  b3 -->|"否"| b6
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 --> b7
  b6 --> b7
  b7 -->|"返回"| b8

```

<a id="mb23020fe763aafd3"></a>

## Of · L22

`public static string Of(DarwinEntity entity)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：Ensure(entity.gameObject).Id"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [EcologyIdentity.Id.get · L15](bdf7ccc2c9c0452f.md#m7ac96c56a47a45d3)（访问器）
- [EcologyIdentity.Ensure · L23](bdf7ccc2c9c0452f.md#m8adfd15f583f225a)（方法）

<a id="m8adfd15f583f225a"></a>

## Ensure · L23

`public static EcologyIdentity Ensure(GameObject obj)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"obj.GetComponent&lt;EcologyIdentity&gt;()<br/>判断：obj.GetComponent&lt;EcologyIdentity&gt;()"}
  b2["obj.GetComponent&lt;EcologyIdentity&gt;()"]
  b3["obj.AddComponent&lt;EcologyIdentity&gt;()"]
  b4["结果：obj.GetComponent&lt;EcologyIdentity&gt;()??obj.AddComponent&lt;EcologyIdentity&gt;()"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="m161b5058306f1ffe"></a>

## Planet.get · L28

`public static PlanetTerrainMeshBuilder Planet`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_planet!=null?_planet:(_planet=UnityEngine.Object.FindFirstObjectByType&lt;PlanetTerrainMeshBuilder&gt;())"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m200839bb9db5ec55"></a>

## Active · L29

`public static GeneEvaluation Active(EntityGeneController genes,string id)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：genes==null"}
  b2{"OptionalComponent.Read&lt;EggIncubation&gt;(genes)<br/>判断：OptionalComponent.Read&lt;EggIncubation&gt;(genes)"}
  b3[".Incubating"]
  b4["OptionalComponent.Read&lt;EggIncubation&gt;(genes)"]
  b5{"判断：OptionalComponent.Read&lt;EggIncubation&gt;(genes)?.Incubating==true"}
  b6["结果：null"]
  b7{"selected=null<br/>判断：genes.Evaluation!=null"}
  b8["genes.Evaluation.Genes"]
  b9{"判断：genes.Evaluation.Genes"}
  b10{"var<br/>判断：gene.IsActive"}
  b11{"判断：gene.Definition.Id==id"}
  b12{"判断：selected==null"}
  b13{"判断：gene.EffectiveStrength&gt;selected.EffectiveStrength"}
  b14["selected=gene;"]
  b15["genes.Evaluation.Genes"]
  b16["结果：selected"]
  b17(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b6
  b2 -->|"否"| b3
  b2 -->|"是"| b4
  b3 --> b5
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 -->|"返回"| b17
  b7 -->|"是"| b8
  b7 -->|"否"| b16
  b8 --> b9
  b9 -->|"是"| b10
  b9 -->|"否 · 经过finally 15"| b16
  b10 -->|"是"| b11
  b10 -->|"否"| b9
  b11 -->|"是"| b12
  b11 -->|"否"| b9
  b12 -->|"否"| b13
  b12 -->|"是"| b14
  b13 -->|"是"| b14
  b13 -->|"否"| b9
  b14 --> b9
  b16 -->|"返回"| b17
  b9 -. "退出保护区时 finally（示意）" .-> b15

```

项目内调用/回调引用：

- [OptionalComponent.Read · L7](806c46b42ced4d36.md#m69622a4dee165c5a)（方法）
- [EntityGeneController.Evaluation.get · L20](7494d53178552f0e.md#mf275f100ea21601a)（访问器）

<a id="m8352bfacbe2996a8"></a>

## Alive · L39

`public static bool Alive(DarwinEntity entity)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：entity!=null"}
  b2{"判断：entity.isActiveAndEnabled"}
  b3{"判断：entity.GetComponent&lt;Stats.EntityStats&gt;() is Stats.EntityStats stats"}
  b4{"判断：stats.TryGet(&quot;health&quot;,out var hp)"}
  b5["hp&gt;0"]
  b6["entity!=null&amp;&amp;entity.isActiveAndEnabled&amp;&amp;entity.GetComponent&lt;Stats.EntityStats&gt;() is Stats.EntityStats stats&amp;&amp;stats.TryGet(&quot;health&quot;,out var hp)&amp;&amp;hp&gt;0"]
  b7["结果：entity!=null&amp;&amp;entity.isActiveAndEnabled&amp;&amp;entity.GetComponent&lt;Stats.EntityStats&gt;() is Stats.EntityStats stats&amp;&amp;stats.TryGet(&quot;health&quot;,out var hp)&amp;&amp;hp&gt;0"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b6
  b2 -->|"是"| b3
  b2 -->|"否"| b6
  b3 -->|"是"| b4
  b3 -->|"否"| b6
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 --> b7
  b6 --> b7
  b7 -->|"返回"| b8

```

项目内调用/回调引用：

- [EntityStats.TryGet · L93](75aa86a368efdf24.md#m03cef847022eee28)（方法）

<a id="mfa1ace432c73012a"></a>

## ToLocal · L40

`public static Vector3 ToLocal(Vector3 point)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Planet!=null"}
  b2["Planet.transform.InverseTransformPoint(point)"]
  b3["point"]
  b4["结果：Planet!=null?Planet.transform.InverseTransformPoint(point):point"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="mc67423f4877c3534"></a>

## ToWorld · L41

`public static Vector3 ToWorld(Vector3 point)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Planet!=null"}
  b2["Planet.transform.TransformPoint(point)"]
  b3["point"]
  b4["结果：Planet!=null?Planet.transform.TransformPoint(point):point"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="ma73e6bbbc726282f"></a>

## Up · L42

`public static Vector3 Up(Vector3 point)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"point<br/>判断：Planet!=null"}
  b2["Planet.transform.position"]
  b3["Vector3.zero"]
  b4["结果：(point-(Planet!=null?Planet.transform.position:Vector3.zero)).normalized"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="m6e395dce655ebcac"></a>

## Distance · L43

`public static float Distance(Vector3 a,Vector3 b)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Planet==null"}
  b2["结果：Vector3.Distance(a,b)"]
  b3["结果：Mathf.Acos(Mathf.Clamp(Vector3.Dot(Up(a),Up(b)),-1,1))*Mathf.Max(Planet.radius,Planet.WaterRadius)"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b4
  b3 -->|"返回"| b4

```

项目内调用/回调引用：

- [EcologyQueries.Up · L42](bdf7ccc2c9c0452f.md#ma73e6bbbc726282f)（方法）
- [PlanetTerrainMeshBuilder.WaterRadius.get · L99](415285cf043354a9.md#m76719c213b06ba56)（访问器）

<a id="m40ee3e975d7f5137"></a>

## Surface · L48

`public static Vector3 Surface(Vector3 point)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Planet==null"}
  b2["结果：point"]
  b3{"direction=Up(point)<br/>判断：direction.sqrMagnitude&lt;.5f"}
  b4["direction=Vector3.up;"]
  b5{"col=Planet.GetComponent&lt;MeshCollider&gt;()<br/>outside=Planet.radius+Planet.heightScale+100<br/>判断：SimulationOptimization.Current.directGroundQueries"}
  b6{"判断：DarwinPark.Planet.RadialTerrainSurface.TrySample(Planet,col,Planet.transform.position+direction*outside,out var surface)"}
  b7["结果：surface.point+direction"]
  b8{"判断：col!=null"}
  b9{"判断：col.Raycast(new Ray(Planet.transform.position+direction*outside,-direction),out var hit,outside*2)"}
  b10["结果：hit.point+direction"]
  b11["结果：Planet.transform.position+direction*(Planet.WaterRadius+1)"]
  b12(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b12
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否"| b8
  b6 -->|"是"| b7
  b6 -->|"否"| b8
  b7 -->|"返回"| b12
  b8 -->|"是"| b9
  b8 -->|"否"| b11
  b9 -->|"是"| b10
  b9 -->|"否"| b11
  b10 -->|"返回"| b12
  b11 -->|"返回"| b12

```

项目内调用/回调引用：

- [EcologyQueries.Up · L42](bdf7ccc2c9c0452f.md#ma73e6bbbc726282f)（方法）
- [SimulationOptimization.Current.get · L30](11c9091e70893352.md#mcfa8eb2e650e57b1)（访问器）
- [RadialTerrainSurface.TrySample · L12](4eb5c0c8143bed20.md#m994c30d4296dac34)（方法）
- [PlanetTerrainMeshBuilder.WaterRadius.get · L99](415285cf043354a9.md#m76719c213b06ba56)（访问器）
