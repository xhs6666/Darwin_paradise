# SurfaceAnchorPhysics.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/SurfaceAnchorPhysics.cs`

[源文件](../../../Assets/DarwinPark/Entities/SurfaceAnchorPhysics.cs) · 源码校验：`095ee09814c84e4e0b9f95d372693b2d6bb2b044b22b21cd88df426edc071875`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m130276acc88982fb"></a>

## Advance · L7

`public static void Advance(EntitySurfaceAnchor settings,Rigidbody body,EntityGroundSample surface,ref float airTime)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：surface.hasSurface"}
  b2{"判断：surface.distance&lt;1"}
  b3{"判断：surface.underwater"}
  b4["airTime=0;"]
  b5{"altitude=surface.altitude<br/>up=surface.up<br/>判断：altitude&lt; -2"}
  b6["body.position=surface.point+up*.5f;<br/>EntityGroundProbe.Velocity(body,Vector3.ProjectOnPlane(body.linearVelocity,up));<br/>airTime=0;"]
  b7{"判断：altitude&lt;=settings.maxAltitude"}
  b8["airTime=0;"]
  b9{"airTime+=Time.fixedDeltaTime;<br/>velocity=body.linearVelocity<br/>outward=Vector3.Dot(velocity,up)<br/>判断：outward&gt;0"}
  b10["EntityGroundProbe.Velocity(body,velocity-up*outward);"]
  b11{"pull=Mathf.Min(settings.pullAcceleration,4+(altitude-settings.maxAltitude)*8)<br/>body.AddForce(-up*pull,ForceMode.Acceleration);<br/>判断：airTime&gt;=settings.rescueDelay"}
  b12{"判断：altitude&gt;settings.rescueAltitude"}
  b13["body.position=surface.point+up;<br/>EntityGroundProbe.Velocity(body,Vector3.ProjectOnPlane(body.linearVelocity,up)*.5f);<br/>airTime=0;"]
  b14(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b14
  b2 -->|"是"| b14
  b2 -->|"否"| b3
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b14
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 --> b14
  b7 -->|"是"| b8
  b7 -->|"否"| b9
  b8 --> b14
  b9 -->|"是"| b10
  b9 -->|"否"| b11
  b10 --> b11
  b11 -->|"否"| b12
  b11 -->|"是"| b13
  b12 -->|"是"| b13
  b12 -->|"否"| b14
  b13 --> b14

```

项目内调用/回调引用：

- [EntityGroundProbe.Velocity · L35](6cefee808441951d.md#mdc5d4538aa1ee338)（方法）
