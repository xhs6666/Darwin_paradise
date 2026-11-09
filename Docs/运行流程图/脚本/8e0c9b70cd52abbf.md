# SimulationRuleList.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/UI/SimulationRuleList.cs`

[源文件](../../../Assets/DarwinPark/UI/SimulationRuleList.cs) · 源码校验：`29b18df608ea49e91e51a40bb5401fc96a79999de88663b8497a271a52078196`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="mf3c329d938444463"></a>

## Dropdown.get · L12

`public Dropdown Dropdown`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["_dropdown"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mcd47bace7682d304"></a>

## SimulationRuleList.ctor · L13

`public SimulationRuleList(Transform parent)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["public SimulationRuleList(Transform parent) { var go=DefaultControls.CreateDropdown(new DefaultControls.Resources());go.name=&quot;RuleList&quot;;go.transform.SetParent(parent,false);var rect=go.GetComponent&lt;RectTransform&gt;();rect.anchorMin=rect.an…"]
  b2["go=DefaultControls.CreateDropdown(new DefaultControls.Resources())<br/>go.name=&quot;RuleList&quot;;<br/>go.transform.SetParent(parent,false);<br/>rect=go.GetComponent&lt;RectTransform&gt;()<br/>rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);<br/>rect.anchoredPo…"]
  b3["go.GetComponentsInChildren&lt;Text&gt;(true)"]
  b4{"判断：go.GetComponentsInChildren&lt;Text&gt;(true)"}
  b5["var<br/>text.font=UIFactory.DefaultFont;<br/>text.fontSize=11;<br/>text.color=UIVisualTheme.Text;"]
  b6{"go.GetComponentsInChildren&lt;Text&gt;(true)<br/>判断：go.GetComponentsInChildren&lt;Text&gt;(true)"}
  b7["go.GetComponentsInChildren&lt;Text&gt;(true)"]
  b8["连接 / 离开区域"]
  b9["_dropdown.captionText.rectTransform.offsetMin=new Vector2(8,1);<br/>_dropdown.captionText.rectTransform.offsetMax=new Vector2(-20,-1);<br/>arrow=go.transform.Find(&quot;Arrow&quot;).GetComponent&lt;Image&gt;()<br/>arrow.sprite=UIIconRegistry.Get(&quot;ui_icon_next&quot;);<br/>ar…"]
  b10(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 --> b4
  b4 -->|"是"| b5
  b4 -->|"否 · 经过finally 6"| b9
  b5 --> b4
  b6 -->|"否"| b7
  b6 -->|"是"| b8
  b7 --> b8
  b9 --> b10
  b4 -. "退出保护区时 finally（示意）" .-> b6

```

项目内调用/回调引用：

- [UIVisualTheme.Paint · L58](ca525a798a08f966.md#m2d61a6b26ab24980)（方法）
- [UIFactory.DefaultFont.get · L17](dd1096a6995423db.md#m89af6843ae7629d3)（访问器）
- [UIIconRegistry.Get · L38](5e97f4545b48cac2.md#m95da89c0db2bbab9)（方法）
- [RuntimeDropdownSkin.Apply · L10](4e9d66f8395f54ec.md#m2b589d708f286228)（方法）
- [SimulationRuleList.回调@21 · L21](8e0c9b70cd52abbf.md#mcaf2dbe042cfef25)（回调/委托）

<a id="mcaf2dbe042cfef25"></a>

## 回调@21 · L21

`index … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：index&gt;=0"}
  b2{"判断：index&lt;_rules.Count"}
  b3["_rules[index].selected();"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b4
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b4

```

<a id="m3753c727dbdbe6be"></a>

## Register · L23

`public void Register(string id,string name,Action selected)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_rules.Exists(r=&gt;r.id==id)"}
  b2["结果：new ArgumentException(&quot;规则编号重复：&quot;+id)"]
  x2(["抛出异常 / 离开方法"])
  b3["_rules.Add((id,selected));<br/>_dropdown.options.Add(new Dropdown.OptionData(name));<br/>_dropdown.RefreshShownValue();"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"Throw"| x2
  b3 --> b4

```

项目内调用/回调引用：

- [SimulationRuleList.回调@24 · L24](8e0c9b70cd52abbf.md#m1152a64a25db914f)（回调/委托）

<a id="m1152a64a25db914f"></a>

## 回调@24 · L24

`r … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：r.id==id"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m93e0ddaa26b331e3"></a>

## Select · L25

`public bool Select(string id)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"index=_rules.FindIndex(r=&gt;r.id==id)<br/>判断：index&lt;0"}
  b2["结果：false"]
  b3["_dropdown.SetValueWithoutNotify(index);<br/>_rules[index].selected();<br/>结果：true"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b4
  b3 -->|"返回"| b4

```

项目内调用/回调引用：

- [SimulationRuleList.回调@26 · L26](8e0c9b70cd52abbf.md#me55ff423b1e557c6)（回调/委托）

<a id="me55ff423b1e557c6"></a>

## 回调@26 · L26

`r … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：r.id==id"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```
