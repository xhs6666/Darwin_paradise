# Log.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Core/Logging/Log.cs`

[源文件](../../../Assets/DarwinPark/Core/Logging/Log.cs) · 源码校验：`6941a6a2077baeef3e240f787158f21bbb47e040b8bbd67779730f3202afe8d1`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="md1fe08215a3ce4d3"></a>

## Info · L31

`public static void Info(Tag tag, string msg)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Mask != Tag.None"}
  b2{"判断：(Mask &amp; tag) == 0"}
  b3["Debug.Log($&quot;[{tag}] {msg}&quot;);"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"是"| b4
  b2 -->|"否"| b3
  b3 --> b4

```

<a id="m25a5d11252901de1"></a>

## Warn · L37

`public static void Warn(Tag tag, string msg)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Mask != Tag.None"}
  b2{"判断：(Mask &amp; tag) == 0"}
  b3["Debug.LogWarning($&quot;[{tag}] {msg}&quot;);"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"是"| b4
  b2 -->|"否"| b3
  b3 --> b4

```

<a id="m2addd5e1d74b000d"></a>

## Error · L43

`public static void Error(Tag tag, string msg)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Debug.LogError($&quot;[{tag}] {msg}&quot;);"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m523652bd455f6a6f"></a>

## Exception · L48

`public static void Exception(Tag tag, string msg, System.Exception e)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Debug.LogError($&quot;[{tag}] {msg}：{e}&quot;);"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```
