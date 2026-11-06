# EntityMovementLimits.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Logic/EntityMovementLimits.cs`

[源文件](../../../Assets/DarwinPark/Entities/Logic/EntityMovementLimits.cs) · 源码校验：`9aec1d253aff9627a10e9d7cdb1aadec200138213b153a4c1e6319e70ec1f420`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="mbe765ae4c73829b7"></a>

## Speed · L9

`public static float Speed(float value)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：Mathf.Clamp(value,0,LocomotionSettings.Load().maximumMoveSpeed)"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [LocomotionSettings.Load · L17](8cce83c7d4563cf0.md#m06b74c04dd63fb4a)（方法）

<a id="m5656261bdba0054b"></a>

## Attribute · L10

`public static float Attribute(EntityGeneController genes,string id,float value)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：id==&quot;moveSpeed&quot;"}
  b2["结果：Speed(value)"]
  b3{"判断：id!=&quot;jumpAbility&quot;"}
  b4["结果：value"]
  b5["settings=LocomotionSettings.Load()<br/>stats=genes.GetComponent&lt;EntityStats&gt;()"]
  b6{"判断：stats!=null"}
  b7{"判断：stats.TryGet(&quot;size&quot;,out float actual)"}
  b8["actual"]
  b9["1"]
  b10["size=stats!=null&amp;&amp;stats.TryGet(&quot;size&quot;,out float actual)?actual:1"]
  b11["结果：Mathf.Clamp(value/Mathf.Pow(Mathf.Max(settings.minimumMovementSize,size),settings.jumpSizeExponent),0,settings.maximumJumpSpeed)"]
  b12(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b12
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 -->|"返回"| b12
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b9
  b7 -->|"是"| b8
  b7 -->|"否"| b9
  b8 --> b10
  b9 --> b10
  b10 --> b11
  b11 -->|"返回"| b12

```

项目内调用/回调引用：

- [EntityMovementLimits.Speed · L9](bf2a44fffc5f10d6.md#mbe765ae4c73829b7)（方法）
- [LocomotionSettings.Load · L17](8cce83c7d4563cf0.md#m06b74c04dd63fb4a)（方法）
- [EntityStats.TryGet · L90](75aa86a368efdf24.md#m24ee1433e4a70d94)（方法）
