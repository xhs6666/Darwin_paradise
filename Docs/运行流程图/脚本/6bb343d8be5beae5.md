# PopulationGroupIndex.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/Ecology/PopulationGroupIndex.cs`

[源文件](../../../Assets/DarwinPark/Entities/Ecology/PopulationGroupIndex.cs) · 源码校验：`98a5c15c3dfc6fd1c4fad66b754d223ff219a77fd9e4884d28a8cf4dd32a71dc`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="mbb4bfaa8b0e1b169"></a>

## BuildCount.get · L17

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

<a id="m631c88183f8fb2a5"></a>

## BuildCount.set · L17

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

<a id="m42c682636166d39b"></a>

## Revision.get · L19

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

<a id="m51047157d7761a13"></a>

## Revision.set · L19

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

<a id="m5e79b5692d858f7a"></a>

## Invalidate · L20

`public static void Invalidate()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Groups.Clear();<br/>Revision++;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [PopulationGroupIndex.Revision.set · L19](6bb343d8be5beae5.md#m51047157d7761a13)（访问器）
- [PopulationGroupIndex.Revision.get · L19](6bb343d8be5beae5.md#m42c682636166d39b)（访问器）

<a id="m1eb0159362acd03a"></a>

## Reset · L21

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Groups.Clear();<br/>BuildCount=0;<br/>Revision=0;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [PopulationGroupIndex.BuildCount.set · L17](6bb343d8be5beae5.md#m631c88183f8fb2a5)（访问器）
- [PopulationGroupIndex.Revision.set · L19](6bb343d8be5beae5.md#m51047157d7761a13)（访问器）

<a id="me1ad939202c72295"></a>

## Earlier · L23

`private static bool Earlier(PopulationBehavior a,PopulationBehavior b)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：b==null"}
  b2{"判断：a.JoinOrder&lt;b.JoinOrder"}
  b3{"判断：a.JoinOrder==b.JoinOrder"}
  b4["string.CompareOrdinal(EcologyIdentity.Of(a.GetComponent&lt;DarwinEntity&gt;()),EcologyIdentity.Of(b.GetComponent&lt;DarwinEntity&gt;()))&lt;0"]
  b5["a.JoinOrder==b.JoinOrder"]
  b6["b==null&#124;&#124;a.JoinOrder&lt;b.JoinOrder&#124;&#124;a.JoinOrder==b.JoinOrder&amp;&amp;string.CompareOrdinal(EcologyIdentity.Of(a.GetComponent&lt;DarwinEntity&gt;()),EcologyIdentity.Of(b.GetComponent&lt;DarwinEntity&gt;()))&lt;0"]
  b7["结果：b==null&#124;&#124;a.JoinOrder&lt;b.JoinOrder&#124;&#124;a.JoinOrder==b.JoinOrder&amp;&amp;string.CompareOrdinal(EcologyIdentity.Of(a.GetComponent&lt;DarwinEntity&gt;()),EcologyIdentity.Of(b.GetComponent&lt;DarwinEntity&gt;()))&lt;0"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b6
  b2 -->|"否"| b3
  b2 -->|"是"| b6
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b7
  b5 --> b7
  b6 --> b7
  b7 -->|"返回"| b8

```

项目内调用/回调引用：

- [PopulationBehavior.JoinOrder.get · L21](21e4f62f75a9a80f.md#m83871a50fb1160fd)（访问器）
- [EcologyIdentity.Of · L21](bdf7ccc2c9c0452f.md#m1143cf9570a93b72)（方法）

<a id="mc5dd4eb8610431ea"></a>

## Get · L24

`private static Group Get(PopulationBehavior owner)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"key=(owner.GroupId,owner.Mode)<br/>判断：Groups.TryGetValue(key,out var group)"}
  b2["结果：group"]
  b3["group=new Group();<br/>BuildCount++;"]
  b4["PopulationBehavior.RegisteredMembers"]
  b5{"判断：PopulationBehavior.RegisteredMembers"}
  b6["var"]
  b7{"判断：member==null"}
  b8{"判断：member.GroupId!=key.Item1"}
  b9{"判断：member.Mode!=key.Item2"}
  b10{"判断：member.Active"}
  b11{"group.members.Add(member);<br/>判断：group.leader==null"}
  b12{"判断：member.LeaderFlag"}
  b13{"判断：group.leader.LeaderFlag"}
  b14{"判断：member.LeaderFlag==group.leader.LeaderFlag"}
  b15{"判断：Earlier(member,group.leader)"}
  b16["group.leader=member;"]
  b17{"ant=member.GetComponent&lt;AntColonyMember&gt;()<br/>判断：key.Item2==EcologySettings.Load().antState"}
  b18{"判断：ant!=null"}
  b19{"判断：ant.Role==AntRole.Queen"}
  b20{"member<br/>判断：group.queen!=null"}
  b21["group.queen.Population"]
  b22["null"]
  b23{"判断：Earlier(member,group.queen!=null?group.queen.Population:null)"}
  b24["group.queen=ant;"]
  b25{"判断：PopulationBehavior.RegisteredMembers"}
  b26["PopulationBehavior.RegisteredMembers"]
  b27["连接 / 离开区域"]
  b28["Groups[key]=group;<br/>结果：group"]
  b29(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b29
  b3 --> b4
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否 · 经过finally 25"| b28
  b6 --> b7
  b7 -->|"否"| b8
  b7 -->|"是"| b5
  b8 -->|"否"| b9
  b8 -->|"是"| b5
  b9 -->|"否"| b10
  b9 -->|"是"| b5
  b10 -->|"否"| b5
  b10 -->|"是"| b11
  b11 -->|"否"| b12
  b11 -->|"是"| b16
  b12 -->|"是"| b13
  b12 -->|"否"| b14
  b13 -->|"是"| b14
  b13 -->|"否"| b16
  b14 -->|"是"| b15
  b14 -->|"否"| b17
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 --> b17
  b17 -->|"是"| b18
  b17 -->|"否"| b5
  b18 -->|"是"| b19
  b18 -->|"否"| b5
  b19 -->|"是"| b20
  b19 -->|"否"| b5
  b20 -->|"是"| b21
  b20 -->|"否"| b22
  b21 --> b23
  b22 --> b23
  b23 -->|"是"| b24
  b23 -->|"否"| b5
  b24 --> b5
  b25 -->|"否"| b26
  b25 -->|"是"| b27
  b26 --> b27
  b28 -->|"返回"| b29
  b5 -. "退出保护区时 finally（示意）" .-> b25

```

项目内调用/回调引用：

- [PopulationBehavior.GroupId.get · L20](21e4f62f75a9a80f.md#m5145936fa914ba8c)（访问器）
- [PopulationGroupIndex.BuildCount.set · L17](6bb343d8be5beae5.md#m631c88183f8fb2a5)（访问器）
- [PopulationGroupIndex.BuildCount.get · L17](6bb343d8be5beae5.md#mbb4bfaa8b0e1b169)（访问器）
- [PopulationBehavior.LeaderFlag.get · L22](21e4f62f75a9a80f.md#m381bf45ebdf091a9)（访问器）
- [PopulationGroupIndex.Earlier · L23](6bb343d8be5beae5.md#me1ad939202c72295)（方法）
- [EcologySettings.Load · L18](90428a275d065ef6.md#mb0575490373edaf2)（方法）
- [AntColonyMember.Role.get · L33](baebd85c05598377.md#mdf6df89f7116c7a3)（访问器）
- [AntColonyMember.Population.get · L30](baebd85c05598377.md#m688c2c0dfb5a66d6)（访问器）

<a id="m37d04d4079b6ece5"></a>

## Members · L39

`public static IEnumerable<PopulationBehavior> Members(PopulationBehavior owner)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：string.IsNullOrEmpty(owner.GroupId)"}
  b2["结果：Empty"]
  b3["结果：AliveMembers(Get(owner).members,owner.GroupId,owner.Mode)"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b4
  b3 -->|"返回"| b4

```

项目内调用/回调引用：

- [PopulationBehavior.GroupId.get · L20](21e4f62f75a9a80f.md#m5145936fa914ba8c)（访问器）
- [PopulationGroupIndex.AliveMembers · L44](6bb343d8be5beae5.md#m787c400f7c4d5979)（方法）
- [PopulationGroupIndex.Get · L24](6bb343d8be5beae5.md#mc5dd4eb8610431ea)（方法）

<a id="m787c400f7c4d5979"></a>

## AliveMembers · L44

`private static IEnumerable<PopulationBehavior> AliveMembers(List<PopulationBehavior> members,string id,string mode)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["members"]
  b2{"判断：members"}
  b3{"var<br/>判断：member!=null"}
  b4{"判断：member.GroupId==id"}
  b5{"判断：member.Mode==mode"}
  b6{"判断：member.Active"}
  b7["yield return member;"]
  b8["members"]
  b9(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"是"| b3
  b2 -->|"否 · 经过finally 8"| b9
  b3 -->|"是"| b4
  b3 -->|"否"| b2
  b4 -->|"是"| b5
  b4 -->|"否"| b2
  b5 -->|"是"| b6
  b5 -->|"否"| b2
  b6 -->|"是"| b7
  b6 -->|"否"| b2
  b7 --> b2
  b2 -. "退出保护区时 finally（示意）" .-> b8

```

项目内调用/回调引用：

- [PopulationBehavior.GroupId.get · L20](21e4f62f75a9a80f.md#m5145936fa914ba8c)（访问器）

<a id="m083b5efa3064e08c"></a>

## Queen · L46

`public static AntColonyMember Queen(PopulationBehavior owner)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：string.IsNullOrEmpty(owner.GroupId)"}
  b2{"判断：owner.Mode!=EcologySettings.Load().antState"}
  b3["结果：null"]
  b4{"group=Get(owner)<br/>判断：group.queen!=null"}
  b5{"判断：group.queen.Active"}
  b6{"判断：group.queen.Role!=AntRole.Queen"}
  b7["Invalidate();<br/>group=Get(owner);"]
  b8["结果：group.queen"]
  b9(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 -->|"返回"| b9
  b4 -->|"是"| b5
  b4 -->|"否"| b8
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 -->|"是"| b7
  b6 -->|"否"| b8
  b7 --> b8
  b8 -->|"返回"| b9

```

项目内调用/回调引用：

- [PopulationBehavior.GroupId.get · L20](21e4f62f75a9a80f.md#m5145936fa914ba8c)（访问器）
- [EcologySettings.Load · L18](90428a275d065ef6.md#mb0575490373edaf2)（方法）
- [PopulationGroupIndex.Get · L24](6bb343d8be5beae5.md#mc5dd4eb8610431ea)（方法）
- [AntColonyMember.Role.get · L33](baebd85c05598377.md#mdf6df89f7116c7a3)（访问器）
- [PopulationGroupIndex.Invalidate · L20](6bb343d8be5beae5.md#m5e79b5692d858f7a)（方法）

<a id="m3d1818b92838ee52"></a>

## Leader · L53

`public static PopulationBehavior Leader(PopulationBehavior owner)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：string.IsNullOrEmpty(owner.GroupId)"}
  b2["结果：null"]
  b3{"group=Get(owner)<br/>判断：group.leader!=null"}
  b4{"判断：group.leader.Active"}
  b5["Invalidate();<br/>group=Get(owner);"]
  b6{"判断：owner.Mode==EcologySettings.Load().antState"}
  b7{"判断：group.queen!=null"}
  b8{"判断：group.queen.Active"}
  b9["group.queen.Population"]
  b10["group.leader"]
  b11["结果：owner.Mode==EcologySettings.Load().antState&amp;&amp;group.queen!=null&amp;&amp;group.queen.Active?group.queen.Population:group.leader"]
  b12(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b12
  b3 -->|"是"| b4
  b3 -->|"否"| b6
  b4 -->|"否"| b5
  b4 -->|"是"| b6
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b10
  b7 -->|"是"| b8
  b7 -->|"否"| b10
  b8 -->|"是"| b9
  b8 -->|"否"| b10
  b9 --> b11
  b10 --> b11
  b11 -->|"返回"| b12

```

项目内调用/回调引用：

- [PopulationBehavior.GroupId.get · L20](21e4f62f75a9a80f.md#m5145936fa914ba8c)（访问器）
- [PopulationGroupIndex.Get · L24](6bb343d8be5beae5.md#mc5dd4eb8610431ea)（方法）
- [PopulationGroupIndex.Invalidate · L20](6bb343d8be5beae5.md#m5e79b5692d858f7a)（方法）
- [EcologySettings.Load · L18](90428a275d065ef6.md#mb0575490373edaf2)（方法）
- [AntColonyMember.Population.get · L30](baebd85c05598377.md#m688c2c0dfb5a66d6)（访问器）
