# GlobalMutationManager.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Simulation/GlobalMutationManager.cs`

[源文件](../../../Assets/DarwinPark/Entities/Simulation/GlobalMutationManager.cs) · 源码校验：`1a6b80685267d92026a188e21a280f0e33e5b62c6edbf8741849d6ebac522bde`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m61fe7ce485547f4f"></a>

## Allowed.get · L10

`get`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m96facc28b4573bac"></a>

## Allowed.set · L10

`private set`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m380d4fa8cff8bd88"></a>

## MergedSkipped.get · L11

`get`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m729fec21f34fd1a6"></a>

## MergedSkipped.set · L11

`private set`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m957f818f0882d2b9"></a>

## BudgetSkipped.get · L12

`get`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m1d09a05b22718d06"></a>

## BudgetSkipped.set · L12

`private set`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m59143ff44607ebd4"></a>

## Permit · L13

`public static bool Permit(EntityGeneController genes)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_frame!=Time.frameCount"}
  b2["_frame=Time.frameCount;<br/>_attempts=0;"]
  b3{"tuning=GameplayTuning.Current<br/>判断：EntityWorkScheduler.TryCohortOrdinal(genes,out int ordinal,out int count)"}
  b4{"判断：count&gt;1"}
  b5{"判断：ordinal&gt;0"}
  b6{"判断：tuning.mergedMutationFraction&lt;=0"}
  b7["int.MaxValue"]
  b8["Mathf.Max(1,Mathf.CeilToInt(1/tuning.mergedMutationFraction))"]
  b9["stride=tuning.mergedMutationFraction&lt;=0?int.MaxValue:Mathf.Max(1,Mathf.CeilToInt(1/tuning.mergedMutationFraction))"]
  b10{"判断：ordinal%stride!=0"}
  b11["MergedSkipped++;<br/>结果：false"]
  b12{"判断：_attempts&gt;=tuning.mutationAttemptsPerFrame"}
  b13["BudgetSkipped++;<br/>结果：false"]
  b14["_attempts++;<br/>Allowed++;<br/>结果：true"]
  b15(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否"| b12
  b4 -->|"是"| b5
  b4 -->|"否"| b12
  b5 -->|"是"| b6
  b5 -->|"否"| b12
  b6 -->|"是"| b7
  b6 -->|"否"| b8
  b7 --> b9
  b8 --> b9
  b9 --> b10
  b10 -->|"是"| b11
  b10 -->|"否"| b12
  b11 -->|"返回"| b15
  b12 -->|"是"| b13
  b12 -->|"否"| b14
  b13 -->|"返回"| b15
  b14 -->|"返回"| b15

```

项目内调用/回调引用：

- [GameplayTuning.Current.get · L31](db546e5713b32d53.md#m557c9ecc76cc372d)（访问器）
- [EntityWorkScheduler.TryCohortOrdinal · L78](465ccdbb260bcac2.md#mb819ba724b68267a)（方法）
- [GlobalMutationManager.MergedSkipped.set · L11](debf58c0eabf33e3.md#m729fec21f34fd1a6)（访问器）
- [GlobalMutationManager.MergedSkipped.get · L11](debf58c0eabf33e3.md#m380d4fa8cff8bd88)（访问器）
- [GlobalMutationManager.BudgetSkipped.set · L12](debf58c0eabf33e3.md#m1d09a05b22718d06)（访问器）
- [GlobalMutationManager.BudgetSkipped.get · L12](debf58c0eabf33e3.md#m957f818f0882d2b9)（访问器）
- [GlobalMutationManager.Allowed.set · L10](debf58c0eabf33e3.md#m96facc28b4573bac)（访问器）
- [GlobalMutationManager.Allowed.get · L10](debf58c0eabf33e3.md#m61fe7ce485547f4f)（访问器）

<a id="me42cf71db33310f5"></a>

## Reset · L25

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_frame=-1;<br/>_attempts=0;<br/>Allowed=MergedSkipped=BudgetSkipped=0;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [GlobalMutationManager.Allowed.set · L10](debf58c0eabf33e3.md#m96facc28b4573bac)（访问器）
- [GlobalMutationManager.MergedSkipped.set · L11](debf58c0eabf33e3.md#m729fec21f34fd1a6)（访问器）
- [GlobalMutationManager.BudgetSkipped.set · L12](debf58c0eabf33e3.md#m1d09a05b22718d06)（访问器）
