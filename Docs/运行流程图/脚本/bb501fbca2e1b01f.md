# NaturalPopulationPace.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Ecology/NaturalPopulationPace.cs`

[源文件](../../../Assets/DarwinPark/Entities/Ecology/NaturalPopulationPace.cs) · 源码校验：`98dd8d66725511e4b50fb3c1c82180b1c00bd849224fb0488d474f92d5660570`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m902ff80ab222d2ce"></a>

## Current.get · L19

`get`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_current==null"}
  b2["Reload();"]
  b3["结果：_current"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3
  b3 -->|"返回"| b4

```

项目内调用/回调引用：

- [NaturalPopulationPace.Reload · L21](bb501fbca2e1b01f.md#m3287761d49aaa330)（方法）

<a id="medfb9861825d543f"></a>

## FilePath.get · L20

`public static string FilePath`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Path.Combine(GamePaths.Root,&quot;config&quot;,&quot;natural_population.json&quot;)"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m3287761d49aaa330"></a>

## Reload · L21

`public static void Reload()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["previous=_current"]
  b2["Load();"]
  b3{"(Exception ex)<br/>判断：previous==null"}
  b4{"fallback=Resources.Load&lt;TextAsset&gt;(&quot;Entities/natural_population&quot;)<br/>判断：fallback==null"}
  x4(["重新抛出异常"])
  b5["_current=Parse(fallback.text);"]
  b6["Debug.LogWarning(&quot;[自然种群] 配置无效，保留有效值：&quot;+ex.Message);"]
  b7(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b7
  b3 -->|"是"| b4
  b3 -->|"否"| b6
  b4 -->|"Rethrow"| x4
  b4 -->|"否"| b5
  b5 --> b6
  b6 --> b7
  b2 -. "保护区可能异常（示意）" .-> b3

```

项目内调用/回调引用：

- [NaturalPopulationPace.Load · L29](bb501fbca2e1b01f.md#mbeba1b6fbceaba64)（方法）
- [NaturalPopulationPace.Parse · L35](bb501fbca2e1b01f.md#mb550373dea39a9d7)（方法）

<a id="m7b5c779b973102c9"></a>

## Poll · L27

`public static void Poll()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Time.unscaledTime&lt;_nextPoll"}
  b2["_nextPoll=Time.unscaledTime+2;"]
  b3{"stamp=File.GetLastWriteTimeUtc(FilePath).Ticks<br/>判断：_current==null"}
  b4{"判断：stamp!=_stamp"}
  b5["Reload();<br/>_stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;"]
  b6["(Exception ex)<br/>Debug.LogWarning(&quot;[自然种群] 无法重新载入：&quot;+ex.Message);"]
  b7(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b7
  b1 -->|"否"| b2
  b2 --> b3
  b3 -->|"否"| b4
  b3 -->|"是"| b5
  b4 -->|"是"| b5
  b4 -->|"否"| b7
  b5 --> b7
  b6 --> b7
  b3 -. "保护区可能异常（示意）" .-> b6

```

项目内调用/回调引用：

- [NaturalPopulationPace.Reload · L21](bb501fbca2e1b01f.md#m3287761d49aaa330)（方法）

<a id="mbeba1b6fbceaba64"></a>

## Load · L29

`private static void Load()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"asset=Resources.Load&lt;TextAsset&gt;(&quot;Entities/natural_population&quot;)<br/>判断：asset==null"}
  b2["结果：new InvalidDataException(&quot;缺少natural_population&quot;)"]
  x2(["抛出异常 / 离开方法"])
  b3{"判断：File.Exists(FilePath)"}
  b4["File.ReadAllText(FilePath)"]
  b5["asset.text"]
  b6["raw=File.Exists(FilePath)?File.ReadAllText(FilePath):asset.text"]
  b7{"value=Parse(raw)<br/>_current=value;<br/>判断：File.Exists(FilePath)"}
  b8["Directory.CreateDirectory(Path.GetDirectoryName(FilePath));<br/>File.WriteAllText(FilePath,asset.text,new System.Text.UTF8Encoding(false));"]
  b9["_stamp=File.GetLastWriteTimeUtc(FilePath).Ticks;"]
  b10(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"Throw"| x2
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b6
  b5 --> b6
  b6 --> b7
  b7 -->|"否"| b8
  b7 -->|"是"| b9
  b8 --> b9
  b9 --> b10

```

项目内调用/回调引用：

- [NaturalPopulationPace.Parse · L35](bb501fbca2e1b01f.md#mb550373dea39a9d7)（方法）

<a id="mb550373dea39a9d7"></a>

## Parse · L35

`public static NaturalPopulationSettings Parse(string raw)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"value=JsonUtility.FromJson&lt;NaturalPopulationSettings&gt;(CommentedJson.Strip(raw))<br/>判断：value==null"}
  b2{"判断：value.version!=1"}
  b3{"判断：value.initialPopulation&lt;2"}
  b4{"判断：value.initialPopulation&gt;1000"}
  b5{"判断：value.maturityYears&lt;=0"}
  b6{"判断：value.elderYears&lt;=value.maturityYears"}
  b7{"判断：value.lifespanYears&lt;=value.elderYears"}
  b8{"判断：value.minimumGestationDays&lt;=0"}
  b9{"判断：value.maximumGestationDays&lt;value.minimumGestationDays"}
  b10["结果：new InvalidDataException(&quot;自然种群年限或初始数量无效&quot;)"]
  x10(["抛出异常 / 离开方法"])
  b11["typeof(NaturalPopulationSettings).GetFields()"]
  b12{"判断：typeof(NaturalPopulationSettings).GetFields()"}
  b13{"var<br/>判断：field.FieldType==typeof(float)"}
  b14{"n=(float)field.GetValue(value)<br/>判断：float.IsFinite(n)"}
  b15{"判断：n&lt;=0"}
  b16["结果：new InvalidDataException(&quot;自然种群参数须为正有限数：&quot;+field.Name)"]
  x16(["抛出异常 / 离开方法"])
  b17{"typeof(NaturalPopulationSettings).GetFields()<br/>判断：typeof(NaturalPopulationSettings).GetFields()"}
  b18["typeof(NaturalPopulationSettings).GetFields()"]
  b19["连接 / 离开区域"]
  b20{"判断：string.IsNullOrWhiteSpace(value.producerTemplate)"}
  b21{"判断：string.IsNullOrWhiteSpace(value.mobileTemplate)"}
  b22["结果：new InvalidDataException(&quot;初始种源模板不能为空&quot;)"]
  x22(["抛出异常 / 离开方法"])
  b23["结果：value"]
  b24(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b10
  b2 -->|"否"| b3
  b2 -->|"是"| b10
  b3 -->|"否"| b4
  b3 -->|"是"| b10
  b4 -->|"否"| b5
  b4 -->|"是"| b10
  b5 -->|"否"| b6
  b5 -->|"是"| b10
  b6 -->|"否"| b7
  b6 -->|"是"| b10
  b7 -->|"否"| b8
  b7 -->|"是"| b10
  b8 -->|"否"| b9
  b8 -->|"是"| b10
  b9 -->|"是"| b10
  b9 -->|"否"| b11
  b10 -->|"Throw"| x10
  b11 --> b12
  b12 -->|"是"| b13
  b12 -->|"否 · 经过finally 17"| b20
  b13 -->|"是"| b14
  b13 -->|"否"| b12
  b14 -->|"是"| b15
  b14 -->|"否"| b16
  b15 -->|"是"| b16
  b15 -->|"否"| b12
  b16 -->|"Throw"| x16
  b17 -->|"否"| b18
  b17 -->|"是"| b19
  b18 --> b19
  b20 -->|"否"| b21
  b20 -->|"是"| b22
  b21 -->|"是"| b22
  b21 -->|"否"| b23
  b22 -->|"Throw"| x22
  b23 -->|"返回"| b24
  b12 -. "退出保护区时 finally（示意）" .-> b17

```

项目内调用/回调引用：

- [CommentedJson.Strip · L9](f72593057d514ce3.md#mc9c02edd5a7f0a4d)（方法）

<a id="m7d12dfc0703924bf"></a>

## LifeSpeed · L43

`public static float LifeSpeed(EntityGeneController genes)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"life=LifeCycleSettings.Load()<br/>cfg=Current<br/>year=life.secondsPerDay*365<br/>判断：cfg.enabled"}
  b2{"判断：genes.CurrentAge==DarwinPark.Entities.AgeState.Elder"}
  b3["Mathf.Max(life.minimumAgingSpeed,genes.GetAttribute(&quot;agingSpeed&quot;))*life.agingProgressScale"]
  b4["Mathf.Max(0,genes.GetAttribute(&quot;growthSpeed&quot;))*life.growthProgressScale"]
  b5["结果：genes.CurrentAge==DarwinPark.Entities.AgeState.Elder?Mathf.Max(life.minimumAgingSpeed,genes.GetAttribute(&quot;agingSpeed&quot;))*life.agingProgressScale:Mathf.Max(0,genes.GetAttribute(&quot;growthSpeed&quot;))*life.growthProgressScale"]
  b6["aging=Mathf.Max(life.minimumAgingSpeed,genes.GetAttribute(&quot;agingSpeed&quot;))/cfg.referenceAgingSpeed<br/>growth=Mathf.Max(0,genes.GetAttribute(&quot;growthSpeed&quot;))/cfg.referenceGrowthSpeed<br/>member=OptionalComponent.Read&lt;AntColonyMember&gt;(genes)"]
  b7{"判断：member!=null"}
  b8{"判断：member.Active"}
  b9{"判断：member.Role==AntRole.Worker"}
  b10["cfg.workerMaturityDays/365"]
  b11["cfg.maturityYears"]
  b12["mature=member!=null&amp;&amp;member.Active&amp;&amp;member.Role==AntRole.Worker?cfg.workerMaturityDays/365:cfg.maturityYears"]
  b13{"判断：genes.CurrentAge==DarwinPark.Entities.AgeState.Infant"}
  b14["life.adultProgress/(mature*year)*growth"]
  b15{"判断：genes.CurrentAge==DarwinPark.Entities.AgeState.Adult"}
  b16["(life.elderProgress-life.adultProgress)/((cfg.elderYears-mature)*year)*aging"]
  b17["(life.deathProgress-life.elderProgress)/((cfg.lifespanYears-cfg.elderYears)*year)*aging"]
  b18["结果：genes.CurrentAge==DarwinPark.Entities.AgeState.Infant?life.adultProgress/(mature*year)*growth:genes.CurrentAge==DarwinPark.Entities.AgeState.Adult?(life.elderProgress-life.adultProgress)/((cfg.elderYears-mature)*year)*aging:(life.deat…"]
  b19(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b6
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5
  b5 -->|"返回"| b19
  b6 --> b7
  b7 -->|"是"| b8
  b7 -->|"否"| b11
  b8 -->|"是"| b9
  b8 -->|"否"| b11
  b9 -->|"是"| b10
  b9 -->|"否"| b11
  b10 --> b12
  b11 --> b12
  b12 --> b13
  b13 -->|"是"| b14
  b13 -->|"否"| b15
  b14 --> b18
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 --> b18
  b17 --> b18
  b18 -->|"返回"| b19

```

项目内调用/回调引用：

- [LifeCycleSettings.Load · L16](ace76969fe8f61de.md#m6fe263852e5a8989)（方法）
- [NaturalPopulationPace.Current.get · L19](bb501fbca2e1b01f.md#m902ff80ab222d2ce)（访问器）
- [EntityGeneController.CurrentAge.get · L39](7494d53178552f0e.md#m977f52932bb90600)（访问器）
- [EntityGeneController.GetAttribute · L288](7494d53178552f0e.md#m45127bcdd833aaf5)（方法）
- [OptionalComponent.Read · L7](806c46b42ced4d36.md#m69622a4dee165c5a)（方法）
- [AntColonyMember.Role.get · L40](baebd85c05598377.md#m382545c3d4092bf1)（访问器）

<a id="m8e5fcd1046b5ff0c"></a>

## Reset · L51

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_current=null;<br/>_stamp=0;<br/>_nextPoll=0;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```
