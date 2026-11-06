# GeneTreeStorage.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Genetics/GeneTreeStorage.cs`

[源文件](../../../Assets/DarwinPark/Genetics/GeneTreeStorage.cs) · 源码校验：`8062251c390308e23e9936c7a8021876d2ea2a0da74ff62062b9e3e8e9b6c216`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m4330d6af5427b828"></a>

## Folder.get · L24

`public static string Folder`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Path.Combine(DarwinPark.Core.GamePaths.Mods,&quot;Default&quot;)"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mc19a5b1d5f28ef3d"></a>

## GenesFolder.get · L25

`public static string GenesFolder`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Path.Combine(Folder,&quot;Genes&quot;)"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mcd6b1d82c9cf8f5c"></a>

## Active.get · L26

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

<a id="mf0de4f332af140af"></a>

## Active.set · L26

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

<a id="m89083f320ec287c0"></a>

## ReadCatalog · L27

`public static Dictionary<string,string> ReadCatalog(string directory=null)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"result=new Dictionary&lt;string,string&gt;()<br/>判断：string.IsNullOrWhiteSpace(directory)"}
  b2["Resources.LoadAll&lt;TextAsset&gt;(&quot;Genes&quot;)"]
  b3{"判断：Resources.LoadAll&lt;TextAsset&gt;(&quot;Genes&quot;)"}
  b4["var<br/>Add(result,asset.text,asset.name);"]
  b5{"Resources.LoadAll&lt;TextAsset&gt;(&quot;Genes&quot;)<br/>判断：Resources.LoadAll&lt;TextAsset&gt;(&quot;Genes&quot;)"}
  b6["Resources.LoadAll&lt;TextAsset&gt;(&quot;Genes&quot;)"]
  b7["连接 / 离开区域"]
  b8["directory=GenesFolder;"]
  b9{"判断：Directory.Exists(directory)"}
  b10["Directory.GetFiles(directory,&quot;*.json&quot;,SearchOption.AllDirectories)"]
  b11{"判断：Directory.GetFiles(directory,&quot;*.json&quot;,SearchOption.AllDirectories)"}
  b12["var<br/>Add(result,File.ReadAllText(file),Path.GetFileNameWithoutExtension(file));"]
  b13{"Directory.GetFiles(directory,&quot;*.json&quot;,SearchOption.AllDirectories)<br/>判断：Directory.GetFiles(directory,&quot;*.json&quot;,SearchOption.AllDirectories)"}
  b14["Directory.GetFiles(directory,&quot;*.json&quot;,SearchOption.AllDirectories)"]
  b15["连接 / 离开区域"]
  b16{"判断：directory!=GenesFolder"}
  b17["结果：new IOException(&quot;文件夹不存在：&quot;+directory)"]
  x17(["抛出异常 / 离开方法"])
  b18["结果：result"]
  b19(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b9
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否 · 经过finally 5"| b8
  b4 --> b3
  b5 -->|"否"| b6
  b5 -->|"是"| b7
  b6 --> b7
  b8 --> b9
  b9 -->|"是"| b10
  b9 -->|"否"| b16
  b10 --> b11
  b11 -->|"是"| b12
  b11 -->|"否 · 经过finally 13"| b18
  b12 --> b11
  b13 -->|"否"| b14
  b13 -->|"是"| b15
  b14 --> b15
  b16 -->|"是"| b17
  b16 -->|"否"| b18
  b17 -->|"Throw"| x17
  b18 -->|"返回"| b19
  b3 -. "退出保护区时 finally（示意）" .-> b5
  b11 -. "退出保护区时 finally（示意）" .-> b13

```

项目内调用/回调引用：

- [GeneTreeStorage.Add · L40](a885bad0d7dedee8.md#mfdfd46dfe0299377)（方法）

<a id="mfdfd46dfe0299377"></a>

## Add · L40

`private static void Add(Dictionary<string,string> map,string text,string name)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"text=EcologyGeneDefaults.Upgrade(GeneEntityStateTags.Upgrade(text));<br/>def=JsonUtility.FromJson&lt;GeneDefinitionJson&gt;(text)<br/>判断：def==null"}
  b2{"判断：def.id!=name"}
  b3{"def.id<br/>判断：def.id"}
  b4["def.id"]
  b5["&quot;&quot;"]
  b6{"判断：System.Text.RegularExpressions.Regex.IsMatch(def.id??&quot;&quot;,&quot;^[A-Za-z0-9_-]+$&quot;)"}
  b7["结果：new InvalidDataException(&quot;文件名与基因 id 必须一致：&quot;+name)"]
  x7(["抛出异常 / 离开方法"])
  b8{"判断：Enum.TryParse&lt;GeneCategory&gt;(def.category,true,out _)"}
  b9["结果：new InvalidDataException(&quot;无效 category：&quot;+name)"]
  x9(["抛出异常 / 离开方法"])
  b10{"判断：def.attributeCurves==null"}
  b11{"判断：def.states==null"}
  b12{"判断：def.suppressionRules==null"}
  b13{"判断：def.attributeLinkages==null"}
  b14["结果：new InvalidDataException(&quot;基因列表字段不能为 null：&quot;+name)"]
  x14(["抛出异常 / 离开方法"])
  b15["def.attributeCurves"]
  b16{"判断：def.attributeCurves"}
  b17{"var<br/>判断：float.IsNaN(curve.minValue)"}
  b18{"判断：float.IsNaN(curve.maxValue)"}
  b19{"判断：float.IsInfinity(curve.minValue)"}
  b20{"判断：float.IsInfinity(curve.maxValue)"}
  b21["结果：new InvalidDataException(&quot;基因数值必须有限：&quot;+name)"]
  x21(["抛出异常 / 离开方法"])
  b22["def.attributeCurves"]
  b23["GeneEntityStateTags.Validate(def,EntityStateRegistry.Current);<br/>map[def.id]=text;"]
  b24(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b7
  b2 -->|"否"| b3
  b2 -->|"是"| b7
  b3 -->|"否"| b4
  b3 -->|"是"| b5
  b4 --> b6
  b5 --> b6
  b6 -->|"否"| b7
  b6 -->|"是"| b8
  b7 -->|"Throw"| x7
  b8 -->|"否"| b9
  b8 -->|"是"| b10
  b9 -->|"Throw"| x9
  b10 -->|"否"| b11
  b10 -->|"是"| b14
  b11 -->|"否"| b12
  b11 -->|"是"| b14
  b12 -->|"否"| b13
  b12 -->|"是"| b14
  b13 -->|"是"| b14
  b13 -->|"否"| b15
  b14 -->|"Throw"| x14
  b15 --> b16
  b16 -->|"是"| b17
  b16 -->|"否 · 经过finally 22"| b23
  b17 -->|"否"| b18
  b17 -->|"是"| b21
  b18 -->|"否"| b19
  b18 -->|"是"| b21
  b19 -->|"否"| b20
  b19 -->|"是"| b21
  b20 -->|"是"| b21
  b20 -->|"否"| b16
  b21 -->|"Throw"| x21
  b23 --> b24
  b16 -. "退出保护区时 finally（示意）" .-> b22

```

项目内调用/回调引用：

- [EcologyGeneDefaults.Upgrade · L14](ce4f4683a31b001c.md#m0cb614734c6a2d00)（方法）
- [GeneEntityStateTags.Upgrade · L11](d87d75eb0b331d5c.md#m2462e83f788e9dea)（方法）
- [GeneEntityStateTags.Validate · L24](d87d75eb0b331d5c.md#m07abded73ee4a80f)（方法）
- [EntityStateRegistry.Current.get · L41](440408d2db5c7d96.md#m61b7aa1a54b9a56c)（访问器）

<a id="m60e52e1c6f841f94"></a>

## Load · L53

`public static GeneTreeDocument Load()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：Active=LoadFrom(Folder)"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [GeneTreeStorage.Active.set · L26](a885bad0d7dedee8.md#mf0de4f332af140af)（访问器）
- [GeneTreeStorage.LoadFrom · L54](a885bad0d7dedee8.md#mc2371aed207c1d6a)（方法）

<a id="mc2371aed207c1d6a"></a>

## LoadFrom · L54

`public static GeneTreeDocument LoadFrom(string folder)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"file=Path.Combine(folder,&quot;tree.json&quot;)<br/>判断：File.Exists(file)"}
  b2{"判断：Path.GetFullPath(folder)!=Path.GetFullPath(Folder)"}
  b3["结果：new GeneTreeDocument()"]
  b4{"builtin=Resources.Load&lt;TextAsset&gt;(&quot;Genetics/default_tree&quot;)<br/>判断：builtin==null"}
  b5["Debug.LogWarning(&quot;[GeneTree] 缺少 Genetics/default_tree&quot;);<br/>结果：new GeneTreeDocument()"]
  b6["结果：JsonUtility.FromJson&lt;GeneTreeDocument&gt;(builtin.text)"]
  b7{"JsonUtility.FromJson&lt;GeneTreeDocument&gt;(File.ReadAllText(file))<br/>判断：JsonUtility.FromJson&lt;GeneTreeDocument&gt;(File.ReadAllText(file))"}
  b8["结果：new InvalidDataException(&quot;基因树为空&quot;)"]
  x8(["抛出异常 / 离开方法"])
  b9["结果：JsonUtility.FromJson&lt;GeneTreeDocument&gt;(File.ReadAllText(file))"]
  b10(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b7
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 -->|"返回"| b10
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 -->|"返回"| b10
  b6 -->|"返回"| b10
  b7 -->|"否"| b9
  b7 -->|"是"| b8
  b8 -->|"Throw"| x8
  b9 -->|"返回"| b10

```

<a id="m3371bf1e3c5ae267"></a>

## Validate · L67

`public static void Validate(GeneTreeDocument doc,Dictionary<string,string> catalog)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"NutrientUtilizationEvolutionUpgrade.Apply(doc,catalog);<br/>NutritionEvolutionUpgrade.Apply(doc,catalog);<br/>ParthenogenesisEvolutionUpgrade.Apply(doc,catalog);<br/>判断：doc==null"}
  b2{"判断：doc.nodes==null"}
  b3{"判断：doc.edges==null"}
  b4["结果：new InvalidDataException(&quot;无效基因树&quot;)"]
  x4(["抛出异常 / 离开方法"])
  b5["ids=new HashSet&lt;string&gt;()"]
  b6["doc.nodes"]
  b7{"判断：doc.nodes"}
  b8{"var<br/>判断：catalog.ContainsKey(n.id)"}
  b9{"判断：ids.Add(n.id)"}
  b10["结果：new InvalidDataException(&quot;缺失或重复节点：&quot;+n.id)"]
  x10(["抛出异常 / 离开方法"])
  b11["doc.nodes"]
  b12["doc.edges"]
  b13{"判断：doc.edges"}
  b14{"var<br/>判断：ids.Contains(e.from)"}
  b15{"判断：ids.Contains(e.to)"}
  b16{"判断：e.from==e.to"}
  b17["结果：new InvalidDataException(&quot;连线端点无效&quot;)"]
  x17(["抛出异常 / 离开方法"])
  b18{"判断：new[]{&quot;evolution&quot;,&quot;error&quot;,&quot;suppress&quot;,&quot;promote&quot;}.Contains(e.kind)"}
  b19{"判断：float.IsNaN(e.value)"}
  b20{"判断：float.IsInfinity(e.value)"}
  b21{"判断：e.value&lt;0"}
  b22["结果：new InvalidDataException(&quot;关系类型或数值无效&quot;)"]
  x22(["抛出异常 / 离开方法"])
  b23{"判断：e.kind==&quot;evolution&quot;"}
  b24{"判断：e.kind==&quot;error&quot;"}
  b25{"判断：GeneEvolutionRules.Allows(e.from,e.to,e.kind,float.NaN)"}
  b26["结果：new InvalidDataException(&quot;受限演化路径无效：&quot;+e.from+&quot; → &quot;+e.to)"]
  x26(["抛出异常 / 离开方法"])
  b27["doc.edges"]
  b28["ids"]
  b29{"判断：ids"}
  b30["var<br/>Visit(id,doc,new HashSet&lt;string&gt;(),new HashSet&lt;string&gt;());"]
  b31["ids"]
  b32(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b4
  b2 -->|"否"| b3
  b2 -->|"是"| b4
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 -->|"Throw"| x4
  b5 --> b6
  b6 --> b7
  b7 -->|"是"| b8
  b7 -->|"否 · 经过finally 11"| b12
  b8 -->|"是"| b9
  b8 -->|"否"| b10
  b9 -->|"否"| b10
  b9 -->|"是"| b7
  b10 -->|"Throw"| x10
  b12 --> b13
  b13 -->|"是"| b14
  b13 -->|"否 · 经过finally 27"| b28
  b14 -->|"是"| b15
  b14 -->|"否"| b17
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 -->|"是"| b17
  b16 -->|"否"| b18
  b17 -->|"Throw"| x17
  b18 -->|"是"| b19
  b18 -->|"否"| b22
  b19 -->|"否"| b20
  b19 -->|"是"| b22
  b20 -->|"否"| b21
  b20 -->|"是"| b22
  b21 -->|"是"| b22
  b21 -->|"否"| b23
  b22 -->|"Throw"| x22
  b23 -->|"否"| b24
  b23 -->|"是"| b25
  b24 -->|"是"| b25
  b24 -->|"否"| b13
  b25 -->|"否"| b26
  b25 -->|"是"| b13
  b26 -->|"Throw"| x26
  b28 --> b29
  b29 -->|"是"| b30
  b29 -->|"否 · 经过finally 31"| b32
  b30 --> b29
  b7 -. "退出保护区时 finally（示意）" .-> b11
  b13 -. "退出保护区时 finally（示意）" .-> b27
  b29 -. "退出保护区时 finally（示意）" .-> b31

```

项目内调用/回调引用：

- [NutrientUtilizationEvolutionUpgrade.Apply · L15](31507b5a0e171a28.md#mc03cba57fe71e4fe)（方法）
- [NutritionEvolutionUpgrade.Apply · L10](02dbee4dabe4bc85.md#md1d08580fa5cdf72)（方法）
- [ParthenogenesisEvolutionUpgrade.Apply · L28](9270126d968c1a50.md#m34aca29b443343d1)（方法）
- [GeneEvolutionRules.Allows · L25](95d3abc5e23f05d3.md#m56e7f05bb0fe9159)（方法）
- [GeneTreeStorage.Visit · L85](a885bad0d7dedee8.md#m4ccdebfae3291496)（方法）

<a id="m4ccdebfae3291496"></a>

## Visit · L85

`private static void Visit(string id,GeneTreeDocument doc,HashSet<string> path,HashSet<string> done)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：done.Contains(id)"}
  b2{"判断：path.Add(id)"}
  b3["结果：new InvalidDataException(&quot;进化关系不能成环&quot;)"]
  x3(["抛出异常 / 离开方法"])
  b4["doc.edges.Where(e=&gt;e.from==id&amp;&amp;e.kind==&quot;evolution&quot;)"]
  b5{"判断：doc.edges.Where(e=&gt;e.from==id&amp;&amp;e.kind==&quot;evolution&quot;)"}
  b6["var<br/>Visit(e.to,doc,path,done);"]
  b7{"判断：doc.edges.Where(e=&gt;e.from==id&amp;&amp;e.kind==&quot;evolution&quot;)"}
  b8["doc.edges.Where(e=&gt;e.from==id&amp;&amp;e.kind==&quot;evolution&quot;)"]
  b9["连接 / 离开区域"]
  b10["path.Remove(id);<br/>done.Add(id);"]
  b11(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b11
  b1 -->|"否"| b2
  b2 -->|"否"| b3
  b2 -->|"是"| b4
  b3 -->|"Throw"| x3
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否 · 经过finally 7"| b10
  b6 --> b5
  b7 -->|"否"| b8
  b7 -->|"是"| b9
  b8 --> b9
  b10 --> b11
  b5 -. "退出保护区时 finally（示意）" .-> b7

```

项目内调用/回调引用：

- [GeneTreeStorage.Visit · L85](a885bad0d7dedee8.md#m4ccdebfae3291496)（方法）
- [GeneTreeStorage.回调@89 · L89](a885bad0d7dedee8.md#m6f20e6092cda103f)（回调/委托）

<a id="m6f20e6092cda103f"></a>

## 回调@89 · L89

`e … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：e.from==id"}
  b2["e.kind==&quot;evolution&quot;"]
  b3["e.from==id"]
  b4["结果：e.from==id&amp;&amp;e.kind==&quot;evolution&quot;"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 -->|"返回"| b5

```

<a id="m92ca7c9be670a39c"></a>

## Save · L92

`public static void Save(GeneTreeDocument doc,Dictionary<string,string> catalog)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["SaveTo(Folder,doc,catalog);<br/>Active=doc;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [GeneTreeStorage.SaveTo · L96](a885bad0d7dedee8.md#m44f80bc754af0f5a)（方法）
- [GeneTreeStorage.Active.set · L26](a885bad0d7dedee8.md#mf0de4f332af140af)（访问器）

<a id="m44f80bc754af0f5a"></a>

## SaveTo · L96

`public static void SaveTo(string folder,GeneTreeDocument doc,Dictionary<string,string> catalog)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Validate(doc,catalog);"]
  b2["catalog"]
  b3{"判断：catalog"}
  b4["var<br/>Add(new Dictionary&lt;string,string&gt;(),item.Value,item.Key);"]
  b5["catalog"]
  b6["genes=Path.Combine(folder,&quot;Genes&quot;)<br/>Directory.CreateDirectory(genes);"]
  b7["catalog"]
  b8{"判断：catalog"}
  b9["var<br/>Write(Path.Combine(genes,item.Key+&quot;.json&quot;),item.Value);"]
  b10["catalog"]
  b11["Write(Path.Combine(folder,&quot;tree.json&quot;),JsonUtility.ToJson(doc,true));<br/>Write(Path.Combine(folder,&quot;entity_states.json&quot;),EntityStateRegistry.CurrentJson);"]
  b12(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否 · 经过finally 5"| b6
  b4 --> b3
  b6 --> b7
  b7 --> b8
  b8 -->|"是"| b9
  b8 -->|"否 · 经过finally 10"| b11
  b9 --> b8
  b11 --> b12
  b3 -. "退出保护区时 finally（示意）" .-> b5
  b8 -. "退出保护区时 finally（示意）" .-> b10

```

项目内调用/回调引用：

- [GeneTreeStorage.Validate · L67](a885bad0d7dedee8.md#m3371bf1e3c5ae267)（方法）
- [GeneTreeStorage.Add · L40](a885bad0d7dedee8.md#mfdfd46dfe0299377)（方法）
- [GeneTreeStorage.Write · L108](a885bad0d7dedee8.md#m1c51177a976c0727)（方法）
- [EntityStateRegistry.CurrentJson.get · L43](440408d2db5c7d96.md#m32449837062f8705)（访问器）

<a id="m1c51177a976c0727"></a>

## Write · L108

`private static void Write(string path,string text)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"File.WriteAllText(path+&quot;.tmp&quot;,text,new System.Text.UTF8Encoding(false));<br/>判断：File.Exists(path)"}
  b2["File.Delete(path);"]
  b3["File.Move(path+&quot;.tmp&quot;,path);"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3
  b3 --> b4

```

<a id="m3b680cb19d835ce7"></a>

## RuntimeAssets · L110

`public static IEnumerable<TextAsset> RuntimeAssets()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：GeneRuntimeProfile.Catalog!=null"}
  b2["catalog=GeneRuntimeProfile.Catalog;<br/>GeneEntityStateTags.UpgradeCatalog(catalog);<br/>EcologyGeneDefaults.UpgradeCatalog(catalog);<br/>Active=GeneRuntimeProfile.Tree;"]
  b3["catalog=ReadCatalog();<br/>Load();"]
  b4["Validate(Active,catalog);"]
  b5["(Exception ex)<br/>Debug.LogError(&quot;[GeneTree] 自定义配置加载失败：&quot;+ex.Message);<br/>Active=new GeneTreeDocument();<br/>结果：Resources.LoadAll&lt;TextAsset&gt;(&quot;Genes&quot;)"]
  b6["结果：catalog.Select(k=&gt;new TextAsset(k.Value){name=k.Key}).ToArray()"]
  b7(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4
  b4 --> b6
  b5 -->|"返回"| b7
  b6 -->|"返回"| b7
  b1 -. "保护区可能异常（示意）" .-> b5

```

项目内调用/回调引用：

- [GeneEntityStateTags.UpgradeCatalog · L22](d87d75eb0b331d5c.md#m648749d3ce7da216)（方法）
- [EcologyGeneDefaults.UpgradeCatalog · L30](ce4f4683a31b001c.md#mf8204e9a94d6e72f)（方法）
- [GeneTreeStorage.Active.set · L26](a885bad0d7dedee8.md#mf0de4f332af140af)（访问器）
- [GeneTreeStorage.ReadCatalog · L27](a885bad0d7dedee8.md#m89083f320ec287c0)（方法）
- [GeneTreeStorage.Load · L53](a885bad0d7dedee8.md#m60e52e1c6f841f94)（方法）
- [GeneTreeStorage.Validate · L67](a885bad0d7dedee8.md#m3371bf1e3c5ae267)（方法）
- [GeneTreeStorage.Active.get · L26](a885bad0d7dedee8.md#mcd6b1d82c9cf8f5c)（访问器）
- [GeneTreeStorage.回调@115 · L115](a885bad0d7dedee8.md#m7cd441e4771f6a41)（回调/委托）

<a id="m7cd441e4771f6a41"></a>

## 回调@115 · L115

`k … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["new TextAsset(k.Value){name=k.Key}<br/>name=k.Key<br/>结果：new TextAsset(k.Value){name=k.Key}"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="ma58a13a0fae63ad6"></a>

## ApplyInfluences · L117

`public static void ApplyInfluences(GeneDefinition definition)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Active.edges.Where(e=&gt;e.from==definition.Id&amp;&amp;(e.kind==&quot;suppress&quot;&#124;&#124;e.kind==&quot;promote&quot;))"]
  b2{"判断：Active.edges.Where(e=&gt;e.from==definition.Id&amp;&amp;(e.kind==&quot;suppress&quot;&#124;&#124;e.kind==&quot;promote&quot;))"}
  b3["var"]
  b4["definition.SuppressionRules<br/>new GeneSuppressionRule{TargetGeneId=edge.to,Coefficient=(edge.kind==&quot;promote&quot;?-1:1)*edge.value}<br/>TargetGeneId=edge.to"]
  b5{"判断：edge.kind==&quot;promote&quot;"}
  b6["-1"]
  b7["1"]
  b8["Coefficient=(edge.kind==&quot;promote&quot;?-1:1)*edge.value"]
  b9["definition.SuppressionRules.Add(new GeneSuppressionRule{TargetGeneId=edge.to,Coefficient=(edge.kind==&quot;promote&quot;?-1:1)*edge.value});"]
  b10{"判断：Active.edges.Where(e=&gt;e.from==definition.Id&amp;&amp;(e.kind==&quot;suppress&quot;&#124;&#124;e.kind==&quot;promote&quot;))"}
  b11["Active.edges.Where(e=&gt;e.from==definition.Id&amp;&amp;(e.kind==&quot;suppress&quot;&#124;&#124;e.kind==&quot;promote&quot;))"]
  b12["连接 / 离开区域"]
  b13(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"是"| b3
  b2 -->|"否 · 经过finally 10"| b13
  b3 --> b4
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 --> b8
  b7 --> b8
  b8 --> b9
  b9 --> b2
  b10 -->|"否"| b11
  b10 -->|"是"| b12
  b11 --> b12
  b2 -. "退出保护区时 finally（示意）" .-> b10

```

项目内调用/回调引用：

- [GeneTreeStorage.Active.get · L26](a885bad0d7dedee8.md#mcd6b1d82c9cf8f5c)（访问器）
- [GeneTreeStorage.回调@119 · L119](a885bad0d7dedee8.md#md04608867ce82829)（回调/委托）

<a id="md04608867ce82829"></a>

## 回调@119 · L119

`e … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：e.from==definition.Id"}
  b2{"判断：e.kind==&quot;suppress&quot;"}
  b3["e.kind==&quot;promote&quot;"]
  b4["e.kind==&quot;suppress&quot;"]
  b5["e.from==definition.Id"]
  b6["结果：e.from==definition.Id&amp;&amp;(e.kind==&quot;suppress&quot;&#124;&#124;e.kind==&quot;promote&quot;)"]
  b7(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b5
  b2 -->|"否"| b3
  b2 -->|"是"| b4
  b3 --> b6
  b4 --> b6
  b5 --> b6
  b6 -->|"返回"| b7

```

<a id="m5b6c0b3324290975"></a>

## Successor · L122

`public static string Successor(string id,string kind,float sourceValue=float.NaN,Genome genome=null)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"edges=Active.edges.Where(e=&gt;e.from==id&amp;&amp;e.kind==kind&amp;&amp;e.value&gt;0&amp;&amp;GeneEvolutionRules.Allows(id,e.to,kind,sourceValue,genome)).ToArray()<br/>sum=edges.Sum(e=&gt;e.value)<br/>判断：sum&lt;=0"}
  b2["结果：null"]
  b3["roll=UnityEngine.Random.value*sum"]
  b4["edges"]
  b5{"判断：edges"}
  b6{"var<br/>roll-=edge.value;<br/>判断：roll&lt;=0"}
  b7["结果：edge.to"]
  b8{"edges<br/>判断：edges"}
  b9["edges"]
  b10["连接 / 离开区域"]
  b11["结果：edges.Last().to"]
  b12(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b12
  b3 --> b4
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否 · 经过finally 8"| b11
  b6 -->|"是"| b7
  b6 -->|"否"| b5
  b7 -->|"返回 · 经过finally 8"| b12
  b8 -->|"否"| b9
  b8 -->|"是"| b10
  b9 --> b10
  b11 -->|"返回"| b12
  b5 -. "退出保护区时 finally（示意）" .-> b8

```

项目内调用/回调引用：

- [GeneTreeStorage.Active.get · L26](a885bad0d7dedee8.md#mcd6b1d82c9cf8f5c)（访问器）
- [GeneTreeStorage.回调@124 · L124](a885bad0d7dedee8.md#m3e2872c2c5ef9ea0)（回调/委托）
- [GeneTreeStorage.回调@125 · L125](a885bad0d7dedee8.md#m4852ecebeaf5997f)（回调/委托）

<a id="m3e2872c2c5ef9ea0"></a>

## 回调@124 · L124

`e … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：e.from==id"}
  b2{"判断：e.kind==kind"}
  b3{"判断：e.value&gt;0"}
  b4["GeneEvolutionRules.Allows(id,e.to,kind,sourceValue,genome)"]
  b5["e.from==id&amp;&amp;e.kind==kind&amp;&amp;e.value&gt;0&amp;&amp;GeneEvolutionRules.Allows(id,e.to,kind,sourceValue,genome)"]
  b6["结果：e.from==id&amp;&amp;e.kind==kind&amp;&amp;e.value&gt;0&amp;&amp;GeneEvolutionRules.Allows(id,e.to,kind,sourceValue,genome)"]
  b7(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b5
  b2 -->|"是"| b3
  b2 -->|"否"| b5
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 --> b6
  b5 --> b6
  b6 -->|"返回"| b7

```

项目内调用/回调引用：

- [GeneEvolutionRules.Allows · L25](95d3abc5e23f05d3.md#m56e7f05bb0fe9159)（方法）

<a id="m4852ecebeaf5997f"></a>

## 回调@125 · L125

`e … => 回调`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：e.value"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```
