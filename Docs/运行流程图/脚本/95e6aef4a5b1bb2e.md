# HudRegressionInput.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/UI/HudRegressionInput.cs`

[源文件](../../../Assets/DarwinPark/UI/HudRegressionInput.cs) · 源码校验：`87013360f0b1dec5823e228c82617db8edc86aa0c9f6327b844923304c2db7b9`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m78a9b6b05503a823"></a>

## mousePresent.get · L11

`public override bool mousePresent`



### Editor · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["true"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m8004480d12b2921c"></a>

## mousePosition.get · L12

`public override Vector2 mousePosition`



### Editor · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Position"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m8c97503ea557a89d"></a>

## mouseScrollDelta.get · L13

`public override Vector2 mouseScrollDelta`



### Editor · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Vector2.zero"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mc19aa4365df61647"></a>

## touchSupported.get · L14

`public override bool touchSupported`



### Editor · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["false"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m57a48aba41f3939c"></a>

## touchCount.get · L15

`public override int touchCount`



### Editor · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["0"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mdd47e2952a677b98"></a>

## GetMouseButtonDown · L16

`public override bool GetMouseButtonDown(int button)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：button==0"}
  b2["Down"]
  b3["button==0"]
  b4["结果：button==0&amp;&amp;Down"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="md099308d9b52b312"></a>

## GetMouseButtonUp · L17

`public override bool GetMouseButtonUp(int button)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：button==0"}
  b2["Up"]
  b3["button==0"]
  b4["结果：button==0&amp;&amp;Up"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="ma23cbc90b0ed6ee8"></a>

## GetMouseButton · L18

`public override bool GetMouseButton(int button)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：button==0"}
  b2["Held"]
  b3["button==0"]
  b4["结果：button==0&amp;&amp;Held"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="m6dac3bcdc11be0ab"></a>

## GetAxisRaw · L19

`public override float GetAxisRaw(string axisName)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：0"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m6bb4675ca60cc545"></a>

## GetButtonDown · L20

`public override bool GetButtonDown(string buttonName)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：false"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```
