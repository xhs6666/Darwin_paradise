# PlanetSurfaceDetail.hlsl

[返回总索引](../README.md) · `GPU` · `Assets/DarwinPark/Planet/Terrain/PlanetSurfaceDetail.hlsl`

[源文件](../../../Assets/DarwinPark/Planet/Terrain/PlanetSurfaceDetail.hlsl) · 源码校验：`806ee13209c90077e1c36ba89780821e305d014400e076b87fec593a2f268f98`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m366638440cd34ebe"></a>

## SurfaceHash · L4

`float SurfaceHash(float3 p)`



### GPU · GPU控制结构（名称链接，不作C#类型推断）

```mermaid
flowchart TD
  b0(["入口"])
  b1["p = frac(p * .1031);<br/>p += dot(p, p.yzx + 33.33);<br/>结果：frac((p.x + p.y) * p.z)"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m932c128f3b0b9f2e"></a>

## SurfaceNoise · L10

`float SurfaceNoise(float3 p)`



### GPU · GPU控制结构（名称链接，不作C#类型推断）

```mermaid
flowchart TD
  b0(["入口"])
  b1["a = floor(p)<br/>f = frac(p)<br/>f = f * f * (3 - 2 * f);<br/>结果：lerp(lerp(lerp(SurfaceHash(a), SurfaceHash(a + float3(1,0,0)), f.x), lerp(SurfaceHash(a + float3(0,1,0)), SurfaceHash(a + float3(1,1,0)), f.x), f.y), lerp(lerp(SurfaceHash(a + float3(0…"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [PlanetSurfaceDetail.SurfaceHash · L4](bcbe3a3de96b1f72.md#m366638440cd34ebe)（GPU函数）

<a id="mf71c1e43fd82f7c3"></a>

## SurfaceDetail · L19

`float SurfaceDetail(float3 direction, float scale)`



### GPU · GPU控制结构（名称链接，不作C#类型推断）

```mermaid
flowchart TD
  b0(["入口"])
  b1["p = direction * scale<br/>结果：SurfaceNoise(p) * .57 + SurfaceNoise(p * 2.03 + 7.1) * .28 + SurfaceNoise(p * 4.1 + 13.5) * .15"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [PlanetSurfaceDetail.SurfaceNoise · L10](bcbe3a3de96b1f72.md#m932c128f3b0b9f2e)（GPU函数）
