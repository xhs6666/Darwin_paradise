# OffspringPlacement.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/OffspringPlacement.cs`

[源文件](../../../Assets/DarwinPark/Entities/OffspringPlacement.cs) · 源码校验：`b8b729db0f39224194e4c412f061a403e41176f6ae2ed8fbbe24fd4d6a464738`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="mcf47222366c0f594"></a>

## TryGetPosition · L9

`public static bool TryGetPosition(Vector3 parent,bool spread,out Vector3 position)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["position=parent;<br/>builder=Object.FindFirstObjectByType&lt;PlanetTerrainMeshBuilder&gt;()"]
  b2{"判断：builder!=null"}
  b3["builder.GetComponent&lt;MeshCollider&gt;()"]
  b4["null"]
  b5["ground=builder!=null?builder.GetComponent&lt;MeshCollider&gt;():null"]
  b6{"判断：ground==null"}
  b7{"判断：builder.Data==null"}
  b8["Debug.LogWarning(&quot;[繁殖] 地形未就绪，暂缓出生&quot;);<br/>结果：false"]
  b9["settings=LifeCycleSettings.Load()<br/>up=(parent-builder.transform.position).normalized"]
  b10{"up<br/>判断：Mathf.Abs(up.y)&gt;.95f"}
  b11["Vector3.right"]
  b12["Vector3.up"]
  b13["a=Vector3.Cross(up,Mathf.Abs(up.y)&gt;.95f?Vector3.right:Vector3.up).normalized"]
  b14["b=Vector3.Cross(up,a)"]
  b15{"判断：spread"}
  b16["settings.stationaryBirthMinRadius"]
  b17["settings.movingBirthRadius"]
  b18["min=spread?settings.stationaryBirthMinRadius:settings.movingBirthRadius"]
  b19{"判断：spread"}
  b20["settings.stationaryBirthMaxRadius"]
  b21["settings.movingBirthRadius"]
  b22["max=spread?settings.stationaryBirthMaxRadius:settings.movingBirthRadius"]
  b23{"radius=Mathf.Sqrt(Random.Range(min*min,max*max))<br/>angle=Random.Range(0,Mathf.PI*2)<br/>direction=(parent-builder.transform.position+(a*Mathf.Cos(angle)+b*Mathf.Sin(angle))*radius).normalized<br/>outside=ground.bounds.extents.magnitude+settings.bi…"}
  b24["结果：false"]
  b25["position=hit.point+direction*settings.birthHeight;<br/>结果：true"]
  b26(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5
  b5 --> b6
  b6 -->|"否"| b7
  b6 -->|"是"| b8
  b7 -->|"是"| b8
  b7 -->|"否"| b9
  b8 -->|"返回"| b26
  b9 --> b10
  b10 -->|"是"| b11
  b10 -->|"否"| b12
  b11 --> b13
  b12 --> b13
  b13 --> b14
  b14 --> b15
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 --> b18
  b17 --> b18
  b18 --> b19
  b19 -->|"是"| b20
  b19 -->|"否"| b21
  b20 --> b22
  b21 --> b22
  b22 --> b23
  b23 -->|"否"| b24
  b23 -->|"是"| b25
  b24 -->|"返回"| b26
  b25 -->|"返回"| b26

```

项目内调用/回调引用：

- [PlanetTerrainMeshBuilder.Data.get · L78](415285cf043354a9.md#m8382f579717fbc6d)（访问器）
- [LifeCycleSettings.Load · L16](ace76969fe8f61de.md#mbd2925785484f343)（方法）
