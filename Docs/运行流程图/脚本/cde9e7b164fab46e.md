# PlanetTerrainGenerator.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Planet/Terrain/PlanetTerrainGenerator.cs`

[源文件](../../../Assets/DarwinPark/Planet/Terrain/PlanetTerrainGenerator.cs) · 源码校验：`68d38cc7a60a9b1a38a5c9de063497134804add5639513b62f6491d4b7d0c966`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m7c6d74a69a660a78"></a>

## Generate · L20

`public static PlanetTerrainData Generate(PlanetTerrainConfig cfg, float seaLevelRatio, float continentRatio)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：cfg == null"}
  b2["结果：null"]
  b3["sub = Mathf.Max(2, cfg.subdivisions)<br/>vertsPerFace = (sub + 1) * (sub + 1)"]
  b4["new PlanetTerrainData { Subdivisions = sub, Radius = cfg.radius, HeightScale = cfg.heightScale, FaceHeights = new float[6][] }<br/>Subdivisions = sub<br/>Radius = cfg.radius<br/>HeightScale = cfg.heightScale<br/>FaceHeights = new float[6][]<br/>data = new P…"]
  b5["continentNoise = new FBMNoise(cfg.baseNoise, cfg.seed)<br/>detailNoise = new FBMNoise(cfg.detailNoise, cfg.seed + 1337)<br/>mountains = new PlanetMountainField(cfg)"]
  b6{"cfg.landforms<br/>判断：cfg.landforms"}
  b7["cfg.landforms"]
  b8["new LandformSettings{enabled=false}<br/>enabled=false"]
  b9["landforms=new PlanetLandformField(cfg.landforms??new LandformSettings{enabled=false},cfg.seed)"]
  b10["new FBMConfig { octaves = 3, frequency = cfg.basinFrequency, amplitude = 1f, lacunarity = 2f, gain = 0.5f, warpStrength = 0.15f }<br/>octaves = 3<br/>frequency = cfg.basinFrequency<br/>amplitude = 1f<br/>lacunarity = 2f<br/>gain = 0.5f<br/>warpStrength = 0.15f<br/>…"]
  b11["basinNoise = new FBMNoise(basinCfg, cfg.seed + 1777)"]
  b12["new FBMConfig { octaves = 4, frequency = cfg.valleyFrequency, amplitude = 1f, lacunarity = 2f, gain = 0.5f, warpStrength = 0.25f }<br/>octaves = 4<br/>frequency = cfg.valleyFrequency<br/>amplitude = 1f<br/>lacunarity = 2f<br/>gain = 0.5f<br/>warpStrength = 0.25…"]
  b13["valleyNoise = new FBMNoise(valleyCfg, cfg.seed + 2663)<br/>rawHeights = new float[6][]<br/>allHeights = new List&lt;float&gt;(6 * vertsPerFace)"]
  b14["face = 0"]
  b15{"判断：face &lt; 6"}
  b16["heights = new float[vertsPerFace]"]
  b17["y = 0"]
  b18{"判断：y &lt;= sub"}
  b19["x = 0"]
  b20{"判断：x &lt;= sub"}
  b21["u = (float)x / sub * 2f - 1f<br/>v = (float)y / sub * 2f - 1f<br/>dir = FacePoint(FaceNormals[face], u, v).normalized<br/>c = Noise01(continentNoise, dir)<br/>heights[y * (sub + 1) + x] = c;<br/>allHeights.Add(c);"]
  b22["x++"]
  b23["y++"]
  b24["rawHeights[face] = heights;"]
  b25["face++"]
  b26["allHeights.Sort();<br/>landFraction = Mathf.Clamp01(continentRatio)<br/>seaIndex = Mathf.FloorToInt((1.0f - landFraction) * allHeights.Count)<br/>seaIndex = Mathf.Clamp(seaIndex, 0, allHeights.Count - 1);<br/>seaLevelThreshold = allHeights[seaIndex]<br/>lan…"]
  b27["face = 0"]
  b28{"判断：face &lt; 6"}
  b29["heights = new float[vertsPerFace]"]
  b30["y = 0"]
  b31{"判断：y &lt;= sub"}
  b32["x = 0"]
  b33{"判断：x &lt;= sub"}
  b34{"i = y * (sub + 1) + x<br/>c = rawHeights[face][i]<br/>u = (float)x / sub * 2f - 1f<br/>v = (float)y / sub * 2f - 1f<br/>dir = FacePoint(FaceNormals[face], u, v).normalized<br/>判断：c &lt; seaLevelThreshold"}
  b35{"判断：seaLevelThreshold &gt; 1e-5f"}
  b36["Mathf.Clamp01(c / seaLevelThreshold)"]
  b37["0f"]
  b38["oceanT = seaLevelThreshold &gt; 1e-5f ? Mathf.Clamp01(c / seaLevelThreshold) : 0f"]
  b39["floor = Mathf.Lerp( seaLevelThreshold * cfg.oceanFloorRatio, seaLevelThreshold, Mathf.Pow(oceanT, 1.8f))<br/>floor += detailNoise.Sample(dir) * cfg.detailStrength * 0.4f * (1f - oceanT);<br/>height = floor;"]
  b40["land = Mathf.Clamp01((c - seaLevelThreshold) / landRange)<br/>baseHeight = seaLevelThreshold + cfg.coastalRise + cfg.inlandRise * Mathf.Pow(land, Mathf.Max(0.1f, cfg.inlandCurve))<br/>baseHeight += detailNoise.Sample(dir) * cfg.detailStrength * …"]
  b41["heights[i] = Mathf.Clamp(height, 0f, PlanetTerrainData.MaxNormalizedHeight);"]
  b42["x++"]
  b43["y++"]
  b44["data.FaceHeights[face] = heights;"]
  b45["face++"]
  b46["ApplyErosion(cfg, data.FaceHeights, sub);<br/>MergeSeamVertices(data.FaceHeights, sub);<br/>PlanetSeamContinuity.Smooth(data,cfg.erosion);<br/>MergeSeamVertices(data.FaceHeights,sub);"]
  b47{"cfg.landforms<br/>判断：cfg.landforms"}
  b48[".enabled"]
  b49["cfg.landforms"]
  b50{"判断：cfg.landforms?.enabled==true"}
  b51["data.Hydrology=PlanetHydrology.Build(data,cfg.landforms);<br/>data.Hydrology.Carve(data);<br/>MergeSeamVertices(data.FaceHeights,sub);"]
  b52["BuildMesh(data);<br/>PlanetMeshTopology.Weld(data);"]
  b53["data.Navigation<br/>data"]
  b54{"cfg.navigation<br/>判断：cfg.navigation"}
  b55["cfg.navigation"]
  b56["new LandNavigationSettings()"]
  b57["data.Navigation=new LandNavigationGraph(data,cfg.navigation??new LandNavigationSettings());"]
  b58["结果：data"]
  b59(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b59
  b3 --> b4
  b4 --> b5
  b5 --> b6
  b6 -->|"否"| b7
  b6 -->|"是"| b8
  b7 --> b9
  b8 --> b9
  b9 --> b10
  b10 --> b11
  b11 --> b12
  b12 --> b13
  b13 --> b14
  b14 --> b15
  b15 -->|"是"| b16
  b15 -->|"否"| b26
  b16 --> b17
  b17 --> b18
  b18 -->|"是"| b19
  b18 -->|"否"| b24
  b19 --> b20
  b20 -->|"是"| b21
  b20 -->|"否"| b23
  b21 --> b22
  b22 --> b20
  b23 --> b18
  b24 --> b25
  b25 --> b15
  b26 --> b27
  b27 --> b28
  b28 -->|"是"| b29
  b28 -->|"否"| b46
  b29 --> b30
  b30 --> b31
  b31 -->|"是"| b32
  b31 -->|"否"| b44
  b32 --> b33
  b33 -->|"是"| b34
  b33 -->|"否"| b43
  b34 -->|"是"| b35
  b34 -->|"否"| b40
  b35 -->|"是"| b36
  b35 -->|"否"| b37
  b36 --> b38
  b37 --> b38
  b38 --> b39
  b39 --> b41
  b40 --> b41
  b41 --> b42
  b42 --> b33
  b43 --> b31
  b44 --> b45
  b45 --> b28
  b46 --> b47
  b47 -->|"否"| b48
  b47 -->|"是"| b49
  b48 --> b50
  b49 --> b50
  b50 -->|"是"| b51
  b50 -->|"否"| b52
  b51 --> b52
  b52 --> b53
  b53 --> b54
  b54 -->|"否"| b55
  b54 -->|"是"| b56
  b55 --> b57
  b56 --> b57
  b57 --> b58
  b58 -->|"返回"| b59

```

项目内调用/回调引用：

- [FBMNoise.FBMNoise.ctor · L12](8e4c1118f3935129.md#m008bcd375f95dd84)（构造函数）
- [PlanetMountainField.PlanetMountainField.ctor · L11](c498fd5cb2934213.md#m01b09f6e87580131)（构造函数）
- [PlanetLandformField.PlanetLandformField.ctor · L73](282937bf06cb5f2a.md#m4519328e460b4907)（构造函数）
- [PlanetTerrainGenerator.FacePoint · L336](cde9e7b164fab46e.md#mfe86a4a87bf91862)（方法）
- [PlanetTerrainGenerator.Noise01 · L185](cde9e7b164fab46e.md#m960131033c9d0fea)（方法）
- [FBMNoise.Sample · L24](8e4c1118f3935129.md#m3997a25fbe63e25e)（方法）
- [PlanetLandformField.Shape · L74](282937bf06cb5f2a.md#m85f86151c8cdf293)（方法）
- [PlanetMountainField.Sample · L19](c498fd5cb2934213.md#m75c89d630c47707c)（方法）
- [PlanetTerrainGenerator.ApplyErosion · L190](cde9e7b164fab46e.md#m0dbe1eafcc76c5bf)（方法）
- [PlanetTerrainGenerator.MergeSeamVertices · L241](cde9e7b164fab46e.md#ma9f3aeb1ced1b0cd)（方法）
- [PlanetSeamContinuity.Smooth · L9](b45fffad7e3300e3.md#m8e8308fbbdc4322e)（方法）
- [PlanetHydrology.Build · L19](282937bf06cb5f2a.md#m89a4629b61694b3d)（方法）
- [PlanetHydrology.Carve · L63](282937bf06cb5f2a.md#m33922055e07c89f3)（方法）
- [PlanetTerrainGenerator.BuildMesh · L283](cde9e7b164fab46e.md#m12cee070473d98c6)（方法）
- [PlanetMeshTopology.Weld · L9](a561079dc7786744.md#md9a5bdc8ae62f315)（方法）
- [LandNavigationGraph.LandNavigationGraph.ctor · L16](6f2590f045ea5d3f.md#m734eccceddc6424c)（构造函数）

<a id="m960131033c9d0fea"></a>

## Noise01 · L185

`private static float Noise01(FBMNoise noise, Vector3 dir)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：Mathf.Clamp01((noise.Sample(dir) + 1f) * 0.5f)"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [FBMNoise.Sample · L24](8e4c1118f3935129.md#m3997a25fbe63e25e)（方法）

<a id="m0dbe1eafcc76c5bf"></a>

## ApplyErosion · L190

`private static void ApplyErosion(PlanetTerrainConfig cfg, float[][] faceHeights, int sub)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：cfg.erosion == null"}
  b2["face = 0"]
  b3{"判断：face &lt; 6"}
  b4["grid = new float[sub + 1, sub + 1]"]
  b5["y = 0"]
  b6{"判断：y &lt;= sub"}
  b7["x = 0"]
  b8{"判断：x &lt;= sub"}
  b9["grid[x, y] = faceHeights[face][y * (sub + 1) + x];<br/>x++"]
  b10["y++"]
  b11{"rng = new DeterministicRandom(cfg.seed + face * 7919 + 17)<br/>判断：cfg.erosion.enableHydraulic"}
  b12{"判断：cfg.erosion.hydraulicIterations &gt; 0"}
  b13["HydraulicErosion.Apply(grid, sub, cfg.erosion.hydraulicIterations, cfg.erosion.hydraulicStrength, rng);"]
  b14{"判断：cfg.erosion.enableThermal"}
  b15{"判断：cfg.erosion.thermalIterations &gt; 0"}
  b16["ThermalErosion.Apply(grid, sub, cfg.erosion.thermalIterations, cfg.erosion.thermalTalusAngle, cfg.erosion.thermalStrength);"]
  b17["y = 0"]
  b18{"判断：y &lt;= sub"}
  b19["x = 0"]
  b20{"判断：x &lt;= sub"}
  b21{"eroded = grid[x, y]<br/>判断：float.IsNaN(eroded)"}
  b22{"判断：float.IsInfinity(eroded)"}
  b23["eroded = 0f;"]
  b24["edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(sub - x, sub - y))<br/>weight = Mathf.SmoothStep(0f, 1f, edge / Mathf.Max(1f, cfg.erosion.seamBlendCells))<br/>index = y * (sub + 1) + x<br/>faceHeights[face][index] = Mathf.Clamp( Mathf.Lerp(faceHeights[f…"]
  b25["x++"]
  b26["y++"]
  b27["face++"]
  b28(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b28
  b1 -->|"否"| b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否"| b28
  b4 --> b5
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b11
  b7 --> b8
  b8 -->|"是"| b9
  b8 -->|"否"| b10
  b9 --> b8
  b10 --> b6
  b11 -->|"是"| b12
  b11 -->|"否"| b14
  b12 -->|"是"| b13
  b12 -->|"否"| b14
  b13 --> b14
  b14 -->|"是"| b15
  b14 -->|"否"| b17
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 --> b17
  b17 --> b18
  b18 -->|"是"| b19
  b18 -->|"否"| b27
  b19 --> b20
  b20 -->|"是"| b21
  b20 -->|"否"| b26
  b21 -->|"否"| b22
  b21 -->|"是"| b23
  b22 -->|"是"| b23
  b22 -->|"否"| b24
  b23 --> b24
  b24 --> b25
  b25 --> b20
  b26 --> b18
  b27 --> b3

```

项目内调用/回调引用：

- [DeterministicRandom.DeterministicRandom.ctor · L15](0c2b60ffd3650bfd.md#mc863035084287113)（构造函数）
- [HydraulicErosion.Apply · L12](8ec387cb4eb3fa28.md#m45b3f13be397f937)（方法）
- [ThermalErosion.Apply · L11](6648fb3132139fb8.md#m2992e195e77e1a49)（方法）

<a id="ma9f3aeb1ced1b0cd"></a>

## MergeSeamVertices · L241

`private static void MergeSeamVertices(float[][] faceHeights, int sub)`

<summary>  侵蚀会分别修改 6 个面；接缝处是重复顶点，如果不取平均会出现裂缝。  </summary>

### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["perFace = (sub + 1) * (sub + 1)<br/>keys = new Vector3Int[6][]<br/>sums = new Dictionary&lt;Vector3Int, float&gt;(6 * perFace)<br/>counts = new Dictionary&lt;Vector3Int, int&gt;(6 * perFace)"]
  b2["face = 0"]
  b3{"判断：face &lt; 6"}
  b4["keys[face] = new Vector3Int[perFace];"]
  b5["y = 0"]
  b6{"判断：y &lt;= sub"}
  b7["x = 0"]
  b8{"判断：x &lt;= sub"}
  b9["i = y * (sub + 1) + x<br/>u = (float)x / sub * 2f - 1f<br/>v = (float)y / sub * 2f - 1f<br/>dir = FacePoint(FaceNormals[face], u, v).normalized<br/>key = new Vector3Int( Mathf.RoundToInt(dir.x * SeamQuantizeScale), Mathf.RoundToInt(dir.y * SeamQuantizeS…"]
  b10["x++"]
  b11["y++"]
  b12["face++"]
  b13["face = 0"]
  b14{"判断：face &lt; 6"}
  b15["i = 0"]
  b16{"判断：i &lt; perFace"}
  b17["key = keys[face][i]<br/>faceHeights[face][i] = sums[key] / Mathf.Max(1, counts[key]);"]
  b18["i++"]
  b19["face++"]
  b20(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否"| b13
  b4 --> b5
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b12
  b7 --> b8
  b8 -->|"是"| b9
  b8 -->|"否"| b11
  b9 --> b10
  b10 --> b8
  b11 --> b6
  b12 --> b3
  b13 --> b14
  b14 -->|"是"| b15
  b14 -->|"否"| b20
  b15 --> b16
  b16 -->|"是"| b17
  b16 -->|"否"| b19
  b17 --> b18
  b18 --> b16
  b19 --> b14

```

项目内调用/回调引用：

- [PlanetTerrainGenerator.FacePoint · L336](cde9e7b164fab46e.md#mfe86a4a87bf91862)（方法）

<a id="m12cee070473d98c6"></a>

## BuildMesh · L283

`private static void BuildMesh(PlanetTerrainData data)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["sub = data.Subdivisions<br/>perFace = (sub + 1) * (sub + 1)<br/>data.Vertices = new Vector3[perFace * 6];<br/>tris = new List&lt;int&gt;(sub * sub * 36)"]
  b2["face = 0"]
  b3{"判断：face &lt; 6"}
  b4["baseIdx = face * perFace"]
  b5["y = 0"]
  b6{"判断：y &lt;= sub"}
  b7["x = 0"]
  b8{"判断：x &lt;= sub"}
  b9["i = baseIdx + y * (sub + 1) + x<br/>u = (float)x / sub * 2f - 1f<br/>v = (float)y / sub * 2f - 1f<br/>dir = FacePoint(FaceNormals[face], u, v).normalized<br/>h = data.FaceHeights[face][y * (sub + 1) + x]<br/>r = data.Radius + h * data.HeightScale<br/>data.Verti…"]
  b10["x++"]
  b11["y++"]
  b12["first_i00 = baseIdx<br/>first_i10 = first_i00 + 1<br/>first_i11 = first_i00 + (sub + 1) + 1<br/>p0 = data.Vertices[first_i00].normalized<br/>p1 = data.Vertices[first_i10].normalized<br/>p2 = data.Vertices[first_i11].normalized<br/>calcNormal = Vector3.Cross(p1 …"]
  b13["y = 0"]
  b14{"判断：y &lt; sub"}
  b15["x = 0"]
  b16{"判断：x &lt; sub"}
  b17{"i00 = baseIdx + y * (sub + 1) + x<br/>i10 = i00 + 1<br/>i01 = baseIdx + (y + 1) * (sub + 1) + x<br/>i11 = i01 + 1<br/>判断：flip"}
  b18["tris.Add(i00);<br/>tris.Add(i10);<br/>tris.Add(i11);<br/>tris.Add(i01);"]
  b19["tris.Add(i00);<br/>tris.Add(i11);<br/>tris.Add(i10);<br/>tris.Add(i01);"]
  b20["x++"]
  b21["y++"]
  b22["face++"]
  b23["data.Triangles = tris.ToArray();"]
  b24(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否"| b23
  b4 --> b5
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b12
  b7 --> b8
  b8 -->|"是"| b9
  b8 -->|"否"| b11
  b9 --> b10
  b10 --> b8
  b11 --> b6
  b12 --> b13
  b13 --> b14
  b14 -->|"是"| b15
  b14 -->|"否"| b22
  b15 --> b16
  b16 -->|"是"| b17
  b16 -->|"否"| b21
  b17 -->|"否"| b18
  b17 -->|"是"| b19
  b18 --> b20
  b19 --> b20
  b20 --> b16
  b21 --> b14
  b22 --> b3
  b23 --> b24

```

项目内调用/回调引用：

- [PlanetTerrainGenerator.FacePoint · L336](cde9e7b164fab46e.md#mfe86a4a87bf91862)（方法）

<a id="mfe86a4a87bf91862"></a>

## FacePoint · L336

`private static Vector3 FacePoint(Vector3 n, float u, float v)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Mathf.Abs(n.x) &gt; 0.5f"}
  b2["结果：new Vector3(n.x, v, u)"]
  b3{"判断：Mathf.Abs(n.y) &gt; 0.5f"}
  b4["结果：new Vector3(u, n.y, v)"]
  b5["结果：new Vector3(u, v, n.z)"]
  b6(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b6
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 -->|"返回"| b6
  b5 -->|"返回"| b6

```
