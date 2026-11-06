# EntityStatsGeneBridge.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Stats/EntityStatsGeneBridge.cs`

[源文件](../../../Assets/DarwinPark/Entities/Stats/EntityStatsGeneBridge.cs) · 源码校验：`fa7b2d7f074c170453a58332ba3ae4541660e5c472f3c5f38e2e0762fa2b3045`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="ma706f92eaeb7834c"></a>

## Awake · L15

`private void Awake()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_genes = GetComponent&lt;EntityGeneController&gt;();<br/>_stats = GetComponent&lt;EntityStats&gt;();"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m602f3d6d16972a98"></a>

## OnEnable · L21

`private void OnEnable()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_genes != null"}
  b2["_genes.OnEvaluationChanged += OnGenesChanged;"]
  b3(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3

```

项目内调用/回调引用：

- [EntityStatsGeneBridge.OnGenesChanged · L31](57e2ec35cce2a9ba.md#m753d8dde206a419c)（方法）

<a id="m00053caa1f5ad7ca"></a>

## OnDisable · L26

`private void OnDisable()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_genes != null"}
  b2["_genes.OnEvaluationChanged -= OnGenesChanged;"]
  b3(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3

```

项目内调用/回调引用：

- [EntityStatsGeneBridge.OnGenesChanged · L31](57e2ec35cce2a9ba.md#m753d8dde206a419c)（方法）

<a id="m753d8dde206a419c"></a>

## OnGenesChanged · L31

`private void OnGenesChanged(GenomeEvaluation eval)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1(["结束 / 返回"])
  b0 --> b1

```
