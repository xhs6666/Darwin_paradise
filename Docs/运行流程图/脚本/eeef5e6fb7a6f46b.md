# OffspringPlacement.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/OffspringPlacement.cs`

[源文件](../../../Assets/DarwinPark/Entities/OffspringPlacement.cs) · 源码校验：`f40da0009140bd46aebeedfc63f2c5ebe85aa8e8d0b59d9a31ff462efd186b68`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="mcf47222366c0f594"></a>

## TryGetPosition · L9

`public static bool TryGetPosition(Vector3 parent,bool spread,out Vector3 position)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.BirthPlacement)"]
  b2["position=parent;<br/>builder=Object.FindFirstObjectByType&lt;PlanetTerrainMeshBuilder&gt;()"]
  b3{"判断：builder!=null"}
  b4["builder.GetComponent&lt;MeshCollider&gt;()"]
  b5["null"]
  b6["ground=builder!=null?builder.GetComponent&lt;MeshCollider&gt;():null"]
  b7{"判断：ground==null"}
  b8{"判断：builder.Data==null"}
  b9["Debug.LogWarning(&quot;[繁殖] 地形未就绪，暂缓出生&quot;);<br/>结果：false"]
  b10["settings=LifeCycleSettings.Load()<br/>up=(parent-builder.transform.position).normalized"]
  b11{"up<br/>判断：Mathf.Abs(up.y)&gt;.95f"}
  b12["Vector3.right"]
  b13["Vector3.up"]
  b14["a=Vector3.Cross(up,Mathf.Abs(up.y)&gt;.95f?Vector3.right:Vector3.up).normalized"]
  b15["b=Vector3.Cross(up,a)"]
  b16{"判断：spread"}
  b17["settings.stationaryBirthMinRadius"]
  b18["settings.movingBirthRadius"]
  b19["min=spread?settings.stationaryBirthMinRadius:settings.movingBirthRadius"]
  b20{"判断：spread"}
  b21["settings.stationaryBirthMaxRadius"]
  b22["settings.movingBirthRadius"]
  b23["max=spread?settings.stationaryBirthMaxRadius:settings.movingBirthRadius"]
  b24{"radius=Mathf.Sqrt(Random.Range(min*min,max*max))<br/>angle=Random.Range(0,Mathf.PI*2)<br/>direction=(parent-builder.transform.position+(a*Mathf.Cos(angle)+b*Mathf.Sin(angle))*radius).normalized<br/>outside=ground.bounds.extents.magnitude+settings.bi…"}
  b25["结果：false"]
  b26["position=hit.point+direction*settings.birthHeight;<br/>结果：true"]
  b27["performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.BirthPlacement)"]
  b28(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b6
  b5 --> b6
  b6 --> b7
  b7 -->|"否"| b8
  b7 -->|"是"| b9
  b8 -->|"是"| b9
  b8 -->|"否"| b10
  b9 -->|"返回 · 经过finally 27"| b28
  b10 --> b11
  b11 -->|"是"| b12
  b11 -->|"否"| b13
  b12 --> b14
  b13 --> b14
  b14 --> b15
  b15 --> b16
  b16 -->|"是"| b17
  b16 -->|"否"| b18
  b17 --> b19
  b18 --> b19
  b19 --> b20
  b20 -->|"是"| b21
  b20 -->|"否"| b22
  b21 --> b23
  b22 --> b23
  b23 --> b24
  b24 -->|"否"| b25
  b24 -->|"是"| b26
  b25 -->|"返回 · 经过finally 27"| b28
  b26 -->|"返回 · 经过finally 27"| b28
  b2 -. "退出保护区时 finally（示意）" .-> b27

```

项目内调用/回调引用：

- [PerformanceTimings.Measure · L24](a8403a4c7503726d.md#m5b1de577e94d50e9)（方法）
- [PlanetTerrainMeshBuilder.Data.get · L78](415285cf043354a9.md#m8382f579717fbc6d)（访问器）
- [LifeCycleSettings.Load · L16](ace76969fe8f61de.md#mbd2925785484f343)（方法）
