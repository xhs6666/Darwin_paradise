# EntityGeneFilter.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/UI/EntityGeneFilter.cs`

[源文件](../../../Assets/DarwinPark/UI/EntityGeneFilter.cs) · 源码校验：`209a5c88399b10c02a73d9a9c6c36984f9e59643b46dad88910acdc30a8711f0`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m420be6b3ba8f0626"></a>

## Included.get · L14

`public IReadOnlyCollection<string> Included`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_included"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="med3f81b1070382e0"></a>

## Excluded.get · L15

`public IReadOnlyCollection<string> Excluded`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_excluded"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m3e568df1c7cc7da4"></a>

## Set · L16

`public void Set(string id,bool include,bool enabled)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：string.IsNullOrEmpty(id)"}
  b2{"判断：include"}
  b3["_included"]
  b4["_excluded"]
  b5["selected=include?_included:_excluded"]
  b6{"判断：include"}
  b7["_excluded"]
  b8["_included"]
  b9["other=include?_excluded:_included"]
  b10{"判断：enabled"}
  b11["selected.Add(id);<br/>other.Remove(id);"]
  b12["selected.Remove(id);"]
  b13(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b13
  b1 -->|"否"| b2
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b8
  b7 --> b9
  b8 --> b9
  b9 --> b10
  b10 -->|"是"| b11
  b10 -->|"否"| b12
  b11 --> b13
  b12 --> b13

```

<a id="m33cd3d9bf4fab3c6"></a>

## Clear · L22

`public void Clear()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_included.Clear();<br/>_excluded.Clear();"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m8e2d24c782ff7469"></a>

## Clear · L23

`public void Clear(bool include)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：include"}
  b2["_included.Clear();"]
  b3["_excluded.Clear();"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4

```

<a id="mffcd67113dd1cbf2"></a>

## KeepKnown · L24

`public void KeepKnown(HashSet<string> known)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_included.RemoveWhere(id=&gt;!known.Contains(id));<br/>_excluded.RemoveWhere(id=&gt;!known.Contains(id));"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [EntityGeneFilter.回调@24 · L24](9ff35fef2ba68deb.md#m3e0040d27b496a8c)（回调/委托）
- [EntityGeneFilter.回调@24 · L24](9ff35fef2ba68deb.md#ma6ff31ddc0c9a028)（回调/委托）

<a id="m3e0040d27b496a8c"></a>

## 回调@24 · L24

`id … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：!known.Contains(id)"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="ma6ff31ddc0c9a028"></a>

## 回调@24 · L24

`id … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：!known.Contains(id)"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="ma1a047f98683fad4"></a>

## Contains · L25

`public bool Contains(string id,bool include)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：include"}
  b2["_included.Contains(id)"]
  b3["_excluded.Contains(id)"]
  b4["结果：include?_included.Contains(id):_excluded.Contains(id)"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="m900802de8e1f1939"></a>

## Matches · L27

`public bool Matches(DarwinEntity entity)`

只在UI主线程调用，复用去重集合，避免每个实体分配HashSet或复制完整基因组。

### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：EcologyQueries.Alive(entity)"}
  b2{"entity.Genes<br/>判断：entity.Genes"}
  b3[".Genome"]
  b4["entity.Genes"]
  b5{"判断：entity.Genes?.Genome==null"}
  b6["结果：false"]
  b7{"判断：_included.Count==0"}
  b8{"判断：_excluded.Count==0"}
  b9["结果：true"]
  b10["required=0<br/>_seen.Clear();"]
  b11["entity.Genes.Genome.Genes"]
  b12{"判断：entity.Genes.Genome.Genes"}
  b13{"var<br/>判断：gene==null"}
  b14{"判断：string.IsNullOrEmpty(gene.DefinitionId)"}
  b15{"判断：_excluded.Contains(gene.DefinitionId)"}
  b16["结果：false"]
  b17{"判断：_included.Contains(gene.DefinitionId)"}
  b18{"判断：_seen.Add(gene.DefinitionId)"}
  b19["required++;"]
  b20["entity.Genes.Genome.Genes"]
  b21["结果：required==_included.Count"]
  b22(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b6
  b2 -->|"否"| b3
  b2 -->|"是"| b4
  b3 --> b5
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 -->|"返回"| b22
  b7 -->|"是"| b8
  b7 -->|"否"| b10
  b8 -->|"是"| b9
  b8 -->|"否"| b10
  b9 -->|"返回"| b22
  b10 --> b11
  b11 --> b12
  b12 -->|"是"| b13
  b12 -->|"否 · 经过finally 20"| b21
  b13 -->|"否"| b14
  b13 -->|"是"| b12
  b14 -->|"是"| b12
  b14 -->|"否"| b15
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 -->|"返回 · 经过finally 20"| b22
  b17 -->|"是"| b18
  b17 -->|"否"| b12
  b18 -->|"是"| b19
  b18 -->|"否"| b12
  b19 --> b12
  b21 -->|"返回"| b22
  b12 -. "退出保护区时 finally（示意）" .-> b20

```

项目内调用/回调引用：

- [EcologyQueries.Alive · L38](bdf7ccc2c9c0452f.md#mb2877c487f449970)（方法）
- [DarwinEntity.Genes.get · L14](70798dde169f2e8b.md#m622eb1adf401d431)（访问器）
