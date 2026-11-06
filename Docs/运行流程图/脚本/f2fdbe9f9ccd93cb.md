# ServiceLocator.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Core/Services/ServiceLocator.cs`

[源文件](../../../Assets/DarwinPark/Core/Services/ServiceLocator.cs) · 源码校验：`087a0ed7a14b7c9cd9fadf79388f3e0f7eef9913aaf5aaecf6538f76b9d43131`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m26753aeca74b231e"></a>

## Register · L16

`public static void Register<T>(T service) where T : class`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：service == null"}
  b2["Log.Error(Log.Tag.Bootstrap, $&quot;注册 null 服务：{typeof(T).Name}&quot;);"]
  b3{"判断：_services.ContainsKey(typeof(T))"}
  b4["Log.Warn(Log.Tag.Bootstrap, $&quot;重复注册服务：{typeof(T).Name}（覆盖）&quot;);"]
  b5["_services[typeof(T)] = service;"]
  b6(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b6
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b5
  b5 --> b6

```

项目内调用/回调引用：

- [Log.Error · L43](735e5abf0df2bddc.md#m2addd5e1d74b000d)（方法）
- [Log.Warn · L37](735e5abf0df2bddc.md#m25a5d11252901de1)（方法）

<a id="m24b4fe5c1600b5cf"></a>

## Unregister · L24

`public static void Unregister<T>() where T : class`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_services.Remove(typeof(T))"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m143b24e6d58471b0"></a>

## TryGet · L26

`public static bool TryGet<T>(out T service) where T : class`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_services.TryGetValue(typeof(T), out var o)"}
  b2["service = o as T;<br/>结果：service != null"]
  b3["service = null;<br/>结果：false"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b4
  b3 -->|"返回"| b4

```

<a id="m2ae625299520c56d"></a>

## Get · L33

`public static T Get<T>(string reason = null) where T : class`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：TryGet&lt;T&gt;(out var s)"}
  b2["结果：s"]
  b3{"Log.Tag.Bootstrap<br/>$&quot;服务缺失：{typeof(T).Name}&quot;<br/>判断：string.IsNullOrEmpty(reason)"}
  b4["&quot;&quot;"]
  b5["$&quot;（{reason}）&quot;"]
  b6["Log.Error(Log.Tag.Bootstrap, $&quot;服务缺失：{typeof(T).Name}&quot; + (string.IsNullOrEmpty(reason) ? &quot;&quot; : $&quot;（{reason}）&quot;));"]
  b7["结果：null"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b8
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b6
  b5 --> b6
  b6 --> b7
  b7 -->|"返回"| b8

```

项目内调用/回调引用：

- [ServiceLocator.TryGet · L26](f2fdbe9f9ccd93cb.md#m143b24e6d58471b0)（方法）
- [Log.Error · L43](735e5abf0df2bddc.md#m2addd5e1d74b000d)（方法）

<a id="mc1c97e0c05315803"></a>

## Clear · L41

`public static void Clear()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_services.Clear()"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```
