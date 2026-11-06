# PopulationCommandPanel.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/UI/PopulationCommandPanel.cs`

[源文件](../../../Assets/DarwinPark/UI/PopulationCommandPanel.cs) · 源码校验：`4d4e2f422473cfc5d308aaa0b37bc186edb1aaf8df94f1fd03ab0e4e04185b9a`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="mf9957bd87e83f0e2"></a>

## Build · L15

`public static void Build(DarwinEntity entity,Transform parent)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"population=entity.GetComponent&lt;PopulationBehavior&gt;()<br/>判断：population==null"}
  b2{"判断：population.Active"}
  b3["obj=UIFactory.NewRect(&quot;PopulationCommands&quot;,parent,typeof(VerticalLayoutGroup),typeof(LayoutElement))<br/>obj.GetComponent&lt;LayoutElement&gt;().preferredHeight=165;<br/>layout=obj.GetComponent&lt;VerticalLayoutGroup&gt;()<br/>layout.childControlWidth=true;<br/>lay…"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b4
  b2 -->|"否"| b4
  b2 -->|"是"| b3
  b3 --> b4

```

项目内调用/回调引用：

- [UIFactory.NewRect · L33](dd1096a6995423db.md#med612116f3152344)（方法）
- [UIFactory.NewText · L57](dd1096a6995423db.md#m9dbccb5f0c38189e)（方法）
- [UIFactory.NewButton · L73](dd1096a6995423db.md#m5896f1b097c8bdde)（方法）
- [PopulationCommandPanel.Refresh · L32](be52342abb2dced2.md#m7cac95f603bd237f)（方法）
- [PopulationCommandPanel.回调@26 · L26](be52342abb2dced2.md#medb41ae895e87ac6)（回调/委托）
- [PopulationCommandPanel.Challenge · L43](be52342abb2dced2.md#mcb24fd9c823c3aa4)（方法）

<a id="medb41ae895e87ac6"></a>

## 回调@26 · L26

`command … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：UIFactory.NewButton(command.ToString(),row.transform,PopulationBehavior.CommandLabel(command),()=&gt;panel.Issue(command),new Vector2(65,28))"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [UIFactory.NewButton · L73](dd1096a6995423db.md#m5896f1b097c8bdde)（方法）
- [PopulationBehavior.CommandLabel · L208](21e4f62f75a9a80f.md#m7907c38716c35174)（方法）
- [PopulationCommandPanel.回调@26 · L26](be52342abb2dced2.md#m98b77f482fa7ee22)（回调/委托）

<a id="m98b77f482fa7ee22"></a>

## 回调@26 · L26

`( … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["panel.Issue(command)"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [PopulationCommandPanel.Issue · L44](be52342abb2dced2.md#m7176948fd6361d25)（方法）

<a id="m085dd08c7feff382"></a>

## Update · L31

`private void Update()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Refresh();"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [PopulationCommandPanel.Refresh · L32](be52342abb2dced2.md#m7cac95f603bd237f)（方法）

<a id="m7cac95f603bd237f"></a>

## Refresh · L32

`private void Refresh()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_entity==null"}
  b2{"判断：_status==null"}
  b3{"population=_entity.GetComponent&lt;PopulationBehavior&gt;()<br/>判断：population==null"}
  b4["blocked=GameInputGate.Blocked<br/>leader=population.IsLeader"]
  b5{"_entity.GetComponent&lt;AntColonyMember&gt;()<br/>判断：_entity.GetComponent&lt;AntColonyMember&gt;()"}
  b6[".Active"]
  b7["_entity.GetComponent&lt;AntColonyMember&gt;()"]
  b8{"判断：_entity.GetComponent&lt;AntColonyMember&gt;()?.Active==true"}
  b9["_status.text=ColonyStatusPresentation.Summary(_entity);"]
  b10["_orders"]
  b11{"判断：_orders"}
  b12["var<br/>button.interactable=false;"]
  b13{"_orders<br/>判断：_orders"}
  b14["_orders"]
  b15["连接 / 离开区域"]
  b16["_challenge.interactable=false;"]
  b17["_status.text"]
  b18{"判断：population.Mode==EcologySettings.Load().antState"}
  b19["&quot;蚁群&quot;"]
  b20["&quot;狼群&quot;"]
  b21["(population.Mode==EcologySettings.Load().antState?&quot;蚁群&quot;:&quot;狼群&quot;)+&quot; · &quot;+population.MemberCount+&quot; 成员 · &quot;"]
  b22{"判断：leader"}
  b23["&quot;领袖&quot;"]
  b24["&quot;成员&quot;"]
  b25["(population.Mode==EcologySettings.Load().antState?&quot;蚁群&quot;:&quot;狼群&quot;)+&quot; · &quot;+population.MemberCount+&quot; 成员 · &quot;+(leader?&quot;领袖&quot;:&quot;成员&quot;)+&quot;\n领袖：&quot;"]
  b26{"判断：population.Leader!=null"}
  b27["population.Leader.GetComponent&lt;DarwinEntity&gt;().DisplayName"]
  b28["&quot;待选&quot;"]
  b29["_status.text=(population.Mode==EcologySettings.Load().antState?&quot;蚁群&quot;:&quot;狼群&quot;)+&quot; · &quot;+population.MemberCount+&quot; 成员 · &quot;+(leader?&quot;领袖&quot;:&quot;成员&quot;)+&quot;\n领袖：&quot;+(population.Leader!=null?population.Leader.GetComponent&lt;DarwinEntity&gt;().DisplayName:&quot;待选&quot;)+&quot; · &quot;+Po…"]
  b30["_orders"]
  b31{"判断：_orders"}
  b32["var"]
  b33{"button.interactable<br/>判断：blocked"}
  b34["leader"]
  b35["!blocked"]
  b36["button.interactable=!blocked&amp;&amp;leader;"]
  b37{"_orders<br/>判断：_orders"}
  b38["_orders"]
  b39["连接 / 离开区域"]
  b40{"_challenge.interactable<br/>判断：blocked"}
  b41{"判断：leader"}
  b42{"判断：population.Active"}
  b43["population.DuelRemaining&lt;=0"]
  b44["!blocked&amp;&amp;!leader&amp;&amp;population.Active&amp;&amp;population.DuelRemaining&lt;=0"]
  b45["_challenge.interactable=!blocked&amp;&amp;!leader&amp;&amp;population.Active&amp;&amp;population.DuelRemaining&lt;=0;"]
  b46(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b46
  b2 -->|"是"| b46
  b2 -->|"否"| b3
  b3 -->|"是"| b46
  b3 -->|"否"| b4
  b4 --> b5
  b5 -->|"否"| b6
  b5 -->|"是"| b7
  b6 --> b8
  b7 --> b8
  b8 -->|"是"| b9
  b8 -->|"否"| b17
  b9 --> b10
  b10 --> b11
  b11 -->|"是"| b12
  b11 -->|"否 · 经过finally 13"| b16
  b12 --> b11
  b13 -->|"否"| b14
  b13 -->|"是"| b15
  b14 --> b15
  b16 --> b46
  b17 --> b18
  b18 -->|"是"| b19
  b18 -->|"否"| b20
  b19 --> b21
  b20 --> b21
  b21 --> b22
  b22 -->|"是"| b23
  b22 -->|"否"| b24
  b23 --> b25
  b24 --> b25
  b25 --> b26
  b26 -->|"是"| b27
  b26 -->|"否"| b28
  b27 --> b29
  b28 --> b29
  b29 --> b30
  b30 --> b31
  b31 -->|"是"| b32
  b31 -->|"否 · 经过finally 37"| b40
  b32 --> b33
  b33 -->|"否"| b34
  b33 -->|"是"| b35
  b34 --> b36
  b35 --> b36
  b36 --> b31
  b37 -->|"否"| b38
  b37 -->|"是"| b39
  b38 --> b39
  b40 -->|"否"| b41
  b40 -->|"是"| b44
  b41 -->|"否"| b42
  b41 -->|"是"| b44
  b42 -->|"是"| b43
  b42 -->|"否"| b44
  b43 --> b45
  b44 --> b45
  b45 --> b46
  b11 -. "退出保护区时 finally（示意）" .-> b13
  b31 -. "退出保护区时 finally（示意）" .-> b37

```

项目内调用/回调引用：

- [GameInputGate.Blocked.get · L8](52955daae7e592ab.md#m765763b665829633)（访问器）
- [ColonyStatusPresentation.Summary · L18](37a7c5ffa1a1a8c2.md#m2e20a4d682fb38ba)（方法）
- [EcologySettings.Load · L18](90428a275d065ef6.md#mb0575490373edaf2)（方法）
- [PopulationBehavior.CommandLabel · L208](21e4f62f75a9a80f.md#m7907c38716c35174)（方法）
- [PopulationBehavior.Command.get · L23](21e4f62f75a9a80f.md#md6a4209cecb72885)（访问器）

<a id="mcb24fd9c823c3aa4"></a>

## Challenge · L43

`private void Challenge()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_entity.GetComponent&lt;PopulationBehavior&gt;().ChallengeLeadership(out _message);"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [PopulationBehavior.ChallengeLeadership · L125](21e4f62f75a9a80f.md#m5391b2572c2724a5)（方法）

<a id="m7176948fd6361d25"></a>

## Issue · L44

`private void Issue(PopulationCommand command)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：GameInputGate.Blocked"}
  b2["pop=_entity.GetComponent&lt;PopulationBehavior&gt;()<br/>settings=EcologySettings.Load()<br/>territory=_entity.GetComponent&lt;TerritoryBehavior&gt;()"]
  b3{"判断：territory!=null"}
  b4["territory.Center"]
  b5["_entity.transform.position"]
  b6["point=territory!=null?territory.Center:_entity.transform.position"]
  b7{"prey=null<br/>判断：command==PopulationCommand.Hunt"}
  b8["prey=EntityRegistry.All.Where(e=&gt;e!=_entity&amp;&amp;EcologyQueries.Alive(e)&amp;&amp;!pop.SameGroup(e)&amp;&amp;EcologyQueries.Distance(_entity.transform.position,e.transform.position)&lt;=settings.maximumCommandRange) .OrderBy(e=&gt;(e.transform.position-_entity.tr…"]
  b9{"判断：command==PopulationCommand.Gather"}
  b10{"判断：pop.FindOre(out point)"}
  b11["_message=&quot;附近没有可采集矿脉&quot;;"]
  b12["pop.Issue(command,prey,point,true,out _message);"]
  b13(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b13
  b1 -->|"否"| b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b6
  b5 --> b6
  b6 --> b7
  b7 -->|"是"| b8
  b7 -->|"否"| b9
  b8 --> b9
  b9 -->|"是"| b10
  b9 -->|"否"| b12
  b10 -->|"否"| b11
  b10 -->|"是"| b12
  b11 --> b13
  b12 --> b13

```

项目内调用/回调引用：

- [GameInputGate.Blocked.get · L8](52955daae7e592ab.md#m765763b665829633)（访问器）
- [EcologySettings.Load · L18](90428a275d065ef6.md#mb0575490373edaf2)（方法）
- [PopulationBehavior.FindOre · L163](21e4f62f75a9a80f.md#mc4d95f5f8d7fcd10)（方法）
- [PopulationBehavior.Issue · L138](21e4f62f75a9a80f.md#m814bead2a1a9395d)（方法）
- [PopulationCommandPanel.回调@50 · L50](be52342abb2dced2.md#m82cf527d322d4573)（回调/委托）
- [PopulationCommandPanel.回调@51 · L51](be52342abb2dced2.md#m1c1286172d8c1be4)（回调/委托）

<a id="m82cf527d322d4573"></a>

## 回调@50 · L50

`e … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：e!=_entity"}
  b2{"判断：EcologyQueries.Alive(e)"}
  b3{"判断：pop.SameGroup(e)"}
  b4["EcologyQueries.Distance(_entity.transform.position,e.transform.position)&lt;=settings.maximumCommandRange"]
  b5["e!=_entity&amp;&amp;EcologyQueries.Alive(e)&amp;&amp;!pop.SameGroup(e)&amp;&amp;EcologyQueries.Distance(_entity.transform.position,e.transform.position)&lt;=settings.maximumCommandRange"]
  b6["结果：e!=_entity&amp;&amp;EcologyQueries.Alive(e)&amp;&amp;!pop.SameGroup(e)&amp;&amp;EcologyQueries.Distance(_entity.transform.position,e.transform.position)&lt;=settings.maximumCommandRange"]
  b7(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b5
  b2 -->|"是"| b3
  b2 -->|"否"| b5
  b3 -->|"否"| b4
  b3 -->|"是"| b5
  b4 --> b6
  b5 --> b6
  b6 -->|"返回"| b7

```

项目内调用/回调引用：

- [EcologyQueries.Alive · L38](bdf7ccc2c9c0452f.md#mb2877c487f449970)（方法）
- [PopulationBehavior.SameGroup · L123](21e4f62f75a9a80f.md#m7a9d850e1d8967d4)（方法）
- [EcologyQueries.Distance · L42](bdf7ccc2c9c0452f.md#mba9c3e3c95d0f2dd)（方法）

<a id="m1c1286172d8c1be4"></a>

## 回调@51 · L51

`e … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：(e.transform.position-_entity.transform.position).sqrMagnitude"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```
