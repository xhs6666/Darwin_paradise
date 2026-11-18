# Environment 模块流程

[返回总索引](../README.md) · [离线交互手册](../index.html)

## 脚本目录

- [Assets/DarwinPark/Environment/EnvironmentConfig.cs](#s019465d17c786108)（0个方法/回调）
- [Assets/DarwinPark/Environment/EnvironmentConfigDatabase.cs](#s4f3727b144de4ba2)（4个方法/回调）
- [Assets/DarwinPark/Environment/EnvironmentSample.cs](#sf194255f71acde59)（0个方法/回调）
- [Assets/DarwinPark/Environment/EnvironmentSystem.cs](#sbfb4f2987b577f02)（12个方法/回调）

---

<a id="s019465d17c786108"></a>

## EnvironmentConfig.cs

`Runtime` · `Assets/DarwinPark/Environment/EnvironmentConfig.cs`

[源文件](../../../Assets/DarwinPark/Environment/EnvironmentConfig.cs) · 源码校验：`836f47e2a7e02a6b9cd11852f113888cb44928b414654372b05d709c62de51ba`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

本文件没有显式方法体，属于数据/枚举/标记声明；字段与类型定义请在离线源码视图查看。


---

<a id="s4f3727b144de4ba2"></a>

## EnvironmentConfigDatabase.cs

`Runtime` · `Assets/DarwinPark/Environment/EnvironmentConfigDatabase.cs`

[源文件](../../../Assets/DarwinPark/Environment/EnvironmentConfigDatabase.cs) · 源码校验：`b84efb5ee35d104149736381656e99691055c48b9a3ed3ace320bcc5d83f7881`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

<a id="m5d12ddf69306a123"></a>

### EnvironmentConfigDatabase.ctor · L8

`public EnvironmentConfigDatabase() : base(Log.Tag.Environment)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1[": base(Log.Tag.Environment)"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [JsonResourceDatabase.JsonResourceDatabase.ctor · L17](Core.md#m8faa5713647c7cfe)（构造函数）

<a id="m22c6ba99f680ee6d"></a>

### ResourceFolder.get · L10

`protected override string ResourceFolder`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["&quot;Environment&quot;"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m17c46c1e249b515b"></a>

### GetId · L11

`protected override string GetId(EnvironmentConfig item)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：item.id"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m9dd5cd41a1b28dab"></a>

### ConvertFromJson · L13

`protected override EnvironmentConfig ConvertFromJson(string jsonText, string assetName)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"cfg = UnityEngine.JsonUtility.FromJson&lt;EnvironmentConfig&gt;(jsonText)<br/>判断：cfg == null"}
  b2["Log.Error(Log.Tag.Environment, $&quot;{assetName} 解析为空&quot;);<br/>结果：null"]
  b3["结果：cfg"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b4
  b3 -->|"返回"| b4

```

项目内调用/回调引用：

- [Log.Error · L43](Core.md#m2addd5e1d74b000d)（方法）


---

<a id="sf194255f71acde59"></a>

## EnvironmentSample.cs

`Runtime` · `Assets/DarwinPark/Environment/EnvironmentSample.cs`

[源文件](../../../Assets/DarwinPark/Environment/EnvironmentSample.cs) · 源码校验：`68dc21b3577c7609c7e5a8c237ef87d33dd05822bf554971e865514a5a831762`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

本文件没有显式方法体，属于数据/枚举/标记声明；字段与类型定义请在离线源码视图查看。


---

<a id="sbfb4f2987b577f02"></a>

## EnvironmentSystem.cs

`Runtime` · `Assets/DarwinPark/Environment/EnvironmentSystem.cs`

[源文件](../../../Assets/DarwinPark/Environment/EnvironmentSystem.cs) · 源码校验：`e5535142f1de866baf4f69c7e82d8f2ac29a2731a6b0a2aecc6a5d759a8caf1b`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

<a id="m3a0c2df48ae2c37d"></a>

### CurrentTemperature.get · L21

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

<a id="m2074178aacc5c234"></a>

### CurrentTemperature.set · L21

`private set`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="me5d44e71ef36315f"></a>

### CurrentHumidity.get · L22

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

<a id="mba8a0c9b18e5c7d9"></a>

### CurrentHumidity.set · L22

`private set`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m89db420c1afba8f5"></a>

### Config.get · L25

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

<a id="mb6374e1af2989d78"></a>

### Config.set · L25

`private set`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m33ee5994493beb9a"></a>

### Awake · L29

`private void Awake()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：ServiceLocator.TryGet&lt;EnvironmentSystem&gt;(out _)"}
  b2["Debug.LogWarning(&quot;[Env] 重复实例，销毁&quot;);<br/>Destroy(this);"]
  b3{"ServiceLocator.Register(this);<br/>Config = EnvironmentConfigDatabase.Instance.Get(ConfigId);<br/>判断：Config == null"}
  b4["Log.Error(Log.Tag.Environment, $&quot;环境配置缺失：{ConfigId}，使用代码内默认值&quot;);<br/>Config = new EnvironmentConfig();"]
  b5["BaseTemperature = Config.baseTemperature;<br/>BaseHumidity = Config.baseHumidity;<br/>Log.Info(Log.Tag.Environment, $&quot;环境配置已加载：{Config.id}，基准 {BaseTemperature:F1}°C / {BaseHumidity:F1}%&quot;);"]
  b6(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b6
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b6
  b5 --> b6

```

项目内调用/回调引用：

- [ServiceLocator.TryGet · L26](Core.md#m143b24e6d58471b0)（方法）
- [ServiceLocator.Register · L16](Core.md#m26753aeca74b231e)（方法）
- [EnvironmentSystem.Config.set · L25](Environment.md#mb6374e1af2989d78)（访问器）
- [JsonResourceDatabase.Get · L69](Core.md#m20bbd84a76422a9e)（方法）
- [EnvironmentSystem.Config.get · L25](Environment.md#m89db420c1afba8f5)（访问器）
- [Log.Error · L43](Core.md#m2addd5e1d74b000d)（方法）
- [Log.Info · L31](Core.md#md1fe08215a3ce4d3)（方法）

<a id="m6bdb95d9430bbe62"></a>

### Start · L54

`private void Start()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["builder = FindFirstObjectByType&lt;DarwinPark.Planet.PlanetTerrainMeshBuilder&gt;()"]
  b2{"_planetRoot<br/>判断：builder != null"}
  b3["builder.transform"]
  b4["null"]
  b5["_planetRoot = builder != null ? builder.transform : null;"]
  b6(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5
  b5 --> b6

```

<a id="m5d3f499c72844d62"></a>

### Update · L61

`private void Update()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock)"}
  b2["clock.GameTime"]
  b3["0f"]
  b4["time = ServiceLocator.TryGet&lt;SimulationClock&gt;(out var clock) ? clock.GameTime : 0f"]
  b5{"ClimateDataHub.NorthernSummer = 0;<br/>判断：enableClimateCycle"}
  b6["cycleRad = (time / 3600f) * Mathf.PI * 2f<br/>ClimateDataHub.NorthernSummer = Mathf.Sin(cycleRad);<br/>seasonalTemp = ClimateDataHub.NorthernSummer * TempSwing<br/>seasonalHum = Mathf.Cos(cycleRad) * HumSwing<br/>CurrentTemperature = BaseTemperature + s…"]
  b7["CurrentTemperature = BaseTemperature;<br/>CurrentHumidity = BaseHumidity;"]
  b8["ClimateDataHub.BaseTemperature = BaseTemperature;<br/>ClimateDataHub.BaseHumidity = BaseHumidity;<br/>ClimateDataHub.SeasonalTemperatureOffset = CurrentTemperature - BaseTemperature;<br/>ClimateDataHub.SeasonalHumidityOffset = CurrentHumidity - Base…"]
  b9(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 --> b8
  b7 --> b8
  b8 --> b9

```

项目内调用/回调引用：

- [ServiceLocator.TryGet · L26](Core.md#m143b24e6d58471b0)（方法）
- [SimulationClock.GameTime.get · L23](Core.md#m74adfa39a896652f)（访问器）
- [EnvironmentSystem.CurrentTemperature.set · L21](Environment.md#m2074178aacc5c234)（访问器）
- [EnvironmentSystem.CurrentHumidity.set · L22](Environment.md#mba8a0c9b18e5c7d9)（访问器）
- [EnvironmentSystem.CurrentTemperature.get · L21](Environment.md#m3a0c2df48ae2c37d)（访问器）
- [EnvironmentSystem.CurrentHumidity.get · L22](Environment.md#me5d44e71ef36315f)（访问器）
- [EnvironmentSystem.UpdatePlanetShader · L92](Environment.md#mb7a4874958dc6258)（方法）

<a id="mb7a4874958dc6258"></a>

### UpdatePlanetShader · L92

`private void UpdatePlanetShader()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"planet = GameObject.Find(&quot;Planet&quot;)<br/>判断：planet != null"}
  b2{"mr = planet.GetComponent&lt;MeshRenderer&gt;()<br/>判断：mr != null"}
  b3["block = new MaterialPropertyBlock()<br/>mr.GetPropertyBlock(block);<br/>block.SetFloat(&quot;_TemperatureOffset&quot;, CurrentTemperature);<br/>block.SetFloat(&quot;_HumidityOffset&quot;, CurrentHumidity);<br/>mr.SetPropertyBlock(block);"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b4
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b4

```

项目内调用/回调引用：

- [EnvironmentSystem.CurrentTemperature.get · L21](Environment.md#m3a0c2df48ae2c37d)（访问器）
- [EnvironmentSystem.CurrentHumidity.get · L22](Environment.md#me5d44e71ef36315f)（访问器）

<a id="mfd85d307a19af095"></a>

### OnDisable · L109

`private void OnDisable()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["ServiceLocator.Unregister&lt;EnvironmentSystem&gt;()"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [ServiceLocator.Unregister · L24](Core.md#m24b4fe5c1600b5cf)（方法）

<a id="me13be13f66a8459d"></a>

### Sample · L111

`public EnvironmentSample Sample(Vector3 pos)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["new EnvironmentSample { Temperature = CurrentTemperature, Humidity = CurrentHumidity }<br/>Temperature = CurrentTemperature<br/>Humidity = CurrentHumidity<br/>结果：new EnvironmentSample { Temperature = CurrentTemperature, Humidity = CurrentHumidity }"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [EnvironmentSystem.CurrentTemperature.get · L21](Environment.md#m3a0c2df48ae2c37d)（访问器）
- [EnvironmentSystem.CurrentHumidity.get · L22](Environment.md#me5d44e71ef36315f)（访问器）
