# EcologyIdentity.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Ecology/EcologyIdentity.cs`

[源文件](../../../Assets/DarwinPark/Entities/Ecology/EcologyIdentity.cs) · 源码校验：`6e20a484e56abcdef706dccca449900df1ef5a8f548b6d486cfeaad8d7161a64`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m989150a58b56a6c2"></a>

## Id.get · L14

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

<a id="m49c42c1725782f99"></a>

## Id.set · L14

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

<a id="m8f343f942b17b807"></a>

## OnEnable · L15

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

- [EcologyIdentity.Id.get · L14](bdf7ccc2c9c0452f.md#m989150a58b56a6c2)（访问器）

<a id="m4d069aaa794dcdd9"></a>

## OnDisable · L16

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

- [EcologyIdentity.Id.get · L14](bdf7ccc2c9c0452f.md#m989150a58b56a6c2)（访问器）

<a id="m0a1c898af0605edf"></a>

## Reset · L17

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

<a id="m393d203b2d4098fb"></a>

## Restore · L18

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

- [EcologyIdentity.OnDisable · L16](bdf7ccc2c9c0452f.md#m4d069aaa794dcdd9)（方法）
- [EcologyIdentity.Id.set · L14](bdf7ccc2c9c0452f.md#m49c42c1725782f99)（访问器）
- [EcologyIdentity.OnEnable · L15](bdf7ccc2c9c0452f.md#m8f343f942b17b807)（方法）

<a id="m58f756a3ba9ec990"></a>

## Find · L20

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

<a id="m1143cf9570a93b72"></a>

## Of · L21

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

- [EcologyIdentity.Id.get · L14](bdf7ccc2c9c0452f.md#m989150a58b56a6c2)（访问器）
- [EcologyIdentity.Ensure · L22](bdf7ccc2c9c0452f.md#m40609054a6d444d0)（方法）

<a id="m40609054a6d444d0"></a>

## Ensure · L22

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

<a id="mb75bba579a9de112"></a>

## Planet.get · L27

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

<a id="m4fb10ad424167e3b"></a>

## Active · L28

`public static GeneEvaluation Active(EntityGeneController genes,string id)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：genes==null"}
  b2{"genes.GetComponent&lt;EggIncubation&gt;()<br/>判断：genes.GetComponent&lt;EggIncubation&gt;()"}
  b3[".Incubating"]
  b4["genes.GetComponent&lt;EggIncubation&gt;()"]
  b5{"判断：genes.GetComponent&lt;EggIncubation&gt;()?.Incubating==true"}
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

- [EntityGeneController.Evaluation.get · L19](7494d53178552f0e.md#m2cb74e57d810ca42)（访问器）

<a id="mb2877c487f449970"></a>

## Alive · L38

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

- [EntityStats.TryGet · L90](75aa86a368efdf24.md#m24ee1433e4a70d94)（方法）

<a id="m1a07639eacf3aa47"></a>

## ToLocal · L39

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

<a id="m320932c05ccecbf7"></a>

## ToWorld · L40

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

<a id="maca7070196f125fd"></a>

## Up · L41

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

<a id="mba9c3e3c95d0f2dd"></a>

## Distance · L42

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

- [EcologyQueries.Up · L41](bdf7ccc2c9c0452f.md#maca7070196f125fd)（方法）
- [PlanetTerrainMeshBuilder.WaterRadius.get · L94](415285cf043354a9.md#m28d8809083881ec1)（访问器）

<a id="m2f1f6873ee5fc8df"></a>

## Surface · L47

`public static Vector3 Surface(Vector3 point)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Planet==null"}
  b2["结果：point"]
  b3{"direction=Up(point)<br/>判断：direction.sqrMagnitude&lt;.5f"}
  b4["direction=Vector3.up;"]
  b5{"col=Planet.GetComponent&lt;MeshCollider&gt;()<br/>outside=Planet.radius+Planet.heightScale+100<br/>判断：col!=null"}
  b6{"判断：col.Raycast(new Ray(Planet.transform.position+direction*outside,-direction),out var hit,outside*2)"}
  b7["结果：hit.point+direction"]
  b8["结果：Planet.transform.position+direction*(Planet.WaterRadius+1)"]
  b9(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b9
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否"| b8
  b6 -->|"是"| b7
  b6 -->|"否"| b8
  b7 -->|"返回"| b9
  b8 -->|"返回"| b9

```

项目内调用/回调引用：

- [EcologyQueries.Up · L41](bdf7ccc2c9c0452f.md#maca7070196f125fd)（方法）
- [PlanetTerrainMeshBuilder.WaterRadius.get · L94](415285cf043354a9.md#m28d8809083881ec1)（访问器）
