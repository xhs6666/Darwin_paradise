# ColonyFeedingTemplateRegression.cs

[返回总索引](../README.md) · `Editor` · `Assets/DarwinPark/Editor/ColonyFeedingTemplateRegression.cs`

[源文件](../../../Assets/DarwinPark/Editor/ColonyFeedingTemplateRegression.cs) · 源码校验：`49a7c03572f0d930c2fff9e06b9e253d51c77070b2a8eee87d5dcc045370dcf3`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="ma8103a186ffd4b2c"></a>

## Folder.get · L20

`private static string Folder`



### Editor · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Path.GetFullPath(&quot;Artifacts/ColonyFeedingTemplateReview&quot;)"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m34b9c42c0fe6e75b"></a>

## Check · L22

`private static void Check(bool condition,string message)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：condition"}
  b2["结果：new InvalidOperationException(message)"]
  x2(["抛出异常 / 离开方法"])
  b3["Passed.Add(message);"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 -->|"Throw"| x2
  b3 --> b4

```

<a id="m74b4dc2173d9759d"></a>

## Food · L23

`private static float Food(AntColonyMember member)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["stats=member.GetComponent&lt;EntityStats&gt;()<br/>stats.TryGet(&quot;nutrient&quot;,out float food);<br/>stats.TryGet(&quot;storedNutrient&quot;,out float reserve);<br/>结果：food+reserve"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [EntityStats.TryGet · L90](75aa86a368efdf24.md#m24ee1433e4a70d94)（方法）

<a id="m671c46cff06fdbf7"></a>

## Fill · L25

`private static void Fill(AntColonyMember member,float value)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["stats=member.GetComponent&lt;EntityStats&gt;()<br/>stats.TryGet(&quot;nutrient&quot;,out float old);<br/>stats.Modify(&quot;nutrient&quot;,value-old,false);<br/>stats.TryGet(&quot;storedNutrient&quot;,out old);<br/>stats.Modify(&quot;storedNutrient&quot;,-old,false);"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [EntityStats.TryGet · L90](75aa86a368efdf24.md#m24ee1433e4a70d94)（方法）
- [EntityStats.Modify · L111](75aa86a368efdf24.md#m9c837be197158240)（方法）

<a id="medb93c2f9a9a0576"></a>

## Verify · L27

`public static void Verify(AntColonyMember queen,AntColonyMember worker,AntColonyMember soldier,AntColonyMember young,ColonyResource nursery,ColonyResource supply)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Directory.CreateDirectory(Folder);<br/>Passed.Clear();<br/>members=new[]{queen,worker,soldier,young}<br/>positions=members.Select(m=&gt;m.transform.position).ToArray()<br/>stats=members.Select(m=&gt;m.GetComponent&lt;EntityStats&gt;().CaptureValues()).ToArray()<br/>sta…"]
  b2["new[]{queen,young}"]
  b3{"判断：new[]{queen,young}"}
  b4["var"]
  b5["Fill(member,0);<br/>member.transform.position=supply.transform.position;<br/>member.Destination(out var point,out var target,out _);<br/>edible=ColonyFeeding.Find(member,false)"]
  b6{"判断：target==null"}
  b7{"判断：edible!=null"}
  b8["EcologyQueries.Distance(point,edible.transform.position)&lt;=AntColonySettings.Current.feedRadius"]
  b9["target==null&amp;&amp;edible!=null&amp;&amp;EcologyQueries.Distance(point,edible.transform.position)&lt;=AntColonySettings.Current.feedRadius"]
  b10["Check(target==null&amp;&amp;edible!=null&amp;&amp;EcologyQueries.Distance(point,edible.transform.position)&lt;=AntColonySettings.Current.feedRadius,&quot;饥饿&quot;+member.Role+&quot;主动走向可实际进食的能量块&quot;);"]
  b11["member.transform.position=nursery.transform.position;<br/>energy=nursery.State.energy<br/>member.Advance(.1f);"]
  b12{"判断：Food(member)&gt;0"}
  b13["Mathf.Abs(energy-nursery.State.energy-Food(member))&lt;.001f"]
  b14["Food(member)&gt;0"]
  b15["Check(Food(member)&gt;0&amp;&amp;Mathf.Abs(energy-nursery.State.energy-Food(member))&lt;.001f,&quot;育幼能量经实际分工更新进入&quot;+member.Role+&quot;且守恒&quot;);"]
  b16{"new[]{queen,young}<br/>判断：new[]{queen,young}"}
  b17["new[]{queen,young}"]
  b18["连接 / 离开区域"]
  b19["Fill(queen,0);<br/>Fill(young,0);<br/>young.transform.position=nursery.transform.position;<br/>nursery.State.energy=AntColonySettings.Current.queenFeedThreshold+AntColonySettings.Current.memberFeedThreshold;<br/>ColonyResource.Feed(young,1);"]
  b20{"判断：Food(young)&gt;0"}
  b21["nursery.State.energy&gt;=AntColonySettings.Current.queenFeedThreshold"]
  b22["Food(young)&gt;0"]
  b23["Check(Food(young)&gt;0&amp;&amp;nursery.State.energy&gt;=AntColonySettings.Current.queenFeedThreshold,&quot;幼体先抵达时保留饥饿王后的最低口粮&quot;);"]
  b24["queen.transform.position=nursery.transform.position;<br/>ColonyResource.Feed(queen,.1f);<br/>Check(Food(queen)&gt;0,&quot;王后抵达后可领取预留口粮&quot;);<br/>nursery.State.energy=1000;<br/>Fill(soldier,0);<br/>typeof(AntColonyMember).GetField(&quot;HuntTarget&quot;,BindingFlags.NonPublic&#124;Bi…"]
  b25{"判断：feedingTarget==null"}
  b26["EcologyQueries.Distance(feedingPoint,supply.transform.position)&lt;=AntColonySettings.Current.feedRadius"]
  b27["feedingTarget==null"]
  b28["Check(feedingTarget==null&amp;&amp;EcologyQueries.Distance(feedingPoint,supply.transform.position)&lt;=AntColonySettings.Current.feedRadius,&quot;已有追猎目标时士兵先前往补给&quot;);"]
  b29["typeof(AntColonyMember).GetField(&quot;HuntTarget&quot;,BindingFlags.NonPublic&#124;BindingFlags.Instance).SetValue(soldier,null);"]
  b30["new ColonyResourceSave{id=Guid.NewGuid().ToString(&quot;N&quot;),group=worker.GroupId,kind=ColonyResourceKind.Corpse,energy=12,local=EcologyQueries.ToLocal(worker.transform.position),scale=Vector3.one}<br/>id=Guid.NewGuid().ToString(&quot;N&quot;)<br/>group=worker.…"]
  b31["Fill(worker,10);<br/>worker.State.resourceTarget=corpse.State.id;<br/>worker.State.scan=10;<br/>worker.Advance(.1f);<br/>Check(worker.State.carriedResource==corpse.State.id,&quot;补给测试建立真实尸体运输&quot;);<br/>worker.State.deliveryPoint=EcologyQueries.ToLocal(nursery.trans…"]
  b32{"判断：worker.Activity==ColonyActivity.Feed"}
  b33{"判断：EcologyQueries.Distance(feedingPoint,supply.transform.position)&lt;=AntColonySettings.Current.feedRadius"}
  b34{"判断：worker.State.carriedResource==corpse.State.id"}
  b35["corpse.State.claim==worker.Id"]
  b36["worker.Activity==ColonyActivity.Feed&amp;&amp;EcologyQueries.Distance(feedingPoint,supply.transform.position)&lt;=AntColonySettings.Current.feedRadius&amp;&amp;worker.State.carriedResource==corpse.State.id&amp;&amp;corpse.State.claim==worker.Id"]
  b37["Check(worker.Activity==ColonyActivity.Feed&amp;&amp;EcologyQueries.Distance(feedingPoint,supply.transform.position)&lt;=AntColonySettings.Current.feedRadius&amp;&amp;worker.State.carriedResource==corpse.State.id&amp;&amp;corpse.State.claim==worker.Id,&quot;饥饿工匠暂停运输去补给并…"]
  b38["worker.transform.position=supply.transform.position;"]
  b39["i=0"]
  b40{"判断：i&lt;100"}
  b41{"判断：ColonyFeeding.WantsFood(worker)"}
  b42["worker.Advance(.1f);<br/>i++"]
  b43["Check(Food(worker)&gt;AntColonySettings.Current.memberFeedThreshold,&quot;繁忙工匠通过实际分工更新吃饱&quot;);<br/>worker.Destination(out var delivery,out _,out _);<br/>Check(delivery==EcologyQueries.ToWorld(worker.State.deliveryPoint),&quot;工匠吃饱后自动恢复原运输目的地&quot;);<br/>worker.transform…"]
  b44{"判断：ColonyResource.Find(corpse.State.id)==null"}
  b45["string.IsNullOrEmpty(worker.State.carriedResource)"]
  b46["ColonyResource.Find(corpse.State.id)==null"]
  b47["Check(ColonyResource.Find(corpse.State.id)==null&amp;&amp;string.IsNullOrEmpty(worker.State.carriedResource),&quot;补给后运输完成且尸体仅转换一次&quot;);"]
  b48["Fill(soldier,0);<br/>soldier.transform.position=supply.transform.position;<br/>vitals=soldier.GetComponent&lt;DarwinPark.Entities.Vitals.EntityVitals&gt;()<br/>advance=typeof(DarwinPark.Entities.Vitals.EntityVitals).GetMethod(&quot;AdvanceVitals&quot;,BindingFlags.…"]
  b49["i=0"]
  b50{"判断：i&lt;1200"}
  b51["soldier.Advance(.1f);<br/>advance.Invoke(vitals,new object[]{.1f});<br/>i++"]
  b52["soldier.GetComponent&lt;EntityStats&gt;().TryGet(&quot;health&quot;,out float afterHealth);"]
  b53{"判断：Food(soldier)&gt;0"}
  b54["afterHealth&gt;=health"]
  b55["Food(soldier)&gt;0"]
  b56["Check(Food(soldier)&gt;0&amp;&amp;afterHealth&gt;=health,&quot;士兵营养基因受抑仍连续120秒吸收补给且不因饥饿掉血&quot;);"]
  b57["Fill(soldier,0);<br/>queenGroup=queen.GroupId<br/>foreignGroup=Guid.NewGuid().ToString(&quot;N&quot;)<br/>queen.Population.JoinColony(foreignGroup,true);<br/>supply.State.group=foreignGroup;<br/>foreignEnergy=supply.State.energy<br/>ColonyResource.Feed(soldier,1);<br/>Check(…"]
  b58{"判断：File.Exists(EntityTemplateStore.FilePath)"}
  b59["EntityTemplateManager.Templates.Count(t=&gt;t.templateName==name)==1"]
  b60["File.Exists(EntityTemplateStore.FilePath)"]
  b61["Check(File.Exists(EntityTemplateStore.FilePath)&amp;&amp;EntityTemplateManager.Templates.Count(t=&gt;t.templateName==name)==1,&quot;模板实际落盘且同名保存不重复&quot;);"]
  b62["File.WriteAllText(Path.Combine(Folder,&quot;fixture.json&quot;),JsonUtility.ToJson(EntityTemplateManager.Templates.Single(t=&gt;t.templateName==name)));<br/>VerifyStoreRecovery();<br/>File.WriteAllText(Path.Combine(Folder,&quot;result.txt&quot;),&quot;PASS\n&quot;+string.Join(&quot;…"]
  b63["i=0"]
  b64{"判断：i&lt;members.Length"}
  b65["members[i].transform.position=positions[i];<br/>members[i].GetComponent&lt;EntityStats&gt;().RestoreValues(stats[i]);<br/>members[i].Restore(states[i]);<br/>i++"]
  b66["nursery.State.energy=nurseryEnergy;<br/>supply.State.energy=supplyEnergy;"]
  b67(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否 · 经过finally 16"| b19
  b4 --> b5
  b5 --> b6
  b6 -->|"是"| b7
  b6 -->|"否"| b9
  b7 -->|"是"| b8
  b7 -->|"否"| b9
  b8 --> b10
  b9 --> b10
  b10 --> b11
  b11 --> b12
  b12 -->|"是"| b13
  b12 -->|"否"| b14
  b13 --> b15
  b14 --> b15
  b15 --> b3
  b16 -->|"否"| b17
  b16 -->|"是"| b18
  b17 --> b18
  b19 --> b20
  b20 -->|"是"| b21
  b20 -->|"否"| b22
  b21 --> b23
  b22 --> b23
  b23 --> b24
  b24 --> b25
  b25 -->|"是"| b26
  b25 -->|"否"| b27
  b26 --> b28
  b27 --> b28
  b28 --> b29
  b29 --> b30
  b30 --> b31
  b31 --> b32
  b32 -->|"是"| b33
  b32 -->|"否"| b36
  b33 -->|"是"| b34
  b33 -->|"否"| b36
  b34 -->|"是"| b35
  b34 -->|"否"| b36
  b35 --> b37
  b36 --> b37
  b37 --> b38
  b38 --> b39
  b39 --> b40
  b40 -->|"是"| b41
  b40 -->|"否"| b43
  b41 -->|"是"| b42
  b41 -->|"否"| b43
  b42 --> b40
  b43 --> b44
  b44 -->|"是"| b45
  b44 -->|"否"| b46
  b45 --> b47
  b46 --> b47
  b47 --> b48
  b48 --> b49
  b49 --> b50
  b50 -->|"是"| b51
  b50 -->|"否"| b52
  b51 --> b50
  b52 --> b53
  b53 -->|"是"| b54
  b53 -->|"否"| b55
  b54 --> b56
  b55 --> b56
  b56 --> b57
  b57 --> b58
  b58 -->|"是"| b59
  b58 -->|"否"| b60
  b59 --> b61
  b60 --> b61
  b61 --> b62
  b62 -->|"经过finally 63"| b67
  b63 --> b64
  b64 -->|"是"| b65
  b64 -->|"否"| b66
  b65 --> b64
  b3 -. "退出保护区时 finally（示意）" .-> b16
  b2 -. "退出保护区时 finally（示意）" .-> b63

```

项目内调用/回调引用：

- [ColonyResource.State.get · L24](293dbaecfff4b585.md#m41824f1a0e160aa5)（访问器）
- [ColonyFeedingTemplateRegression.Fill · L25](e3a5269cbeb35cff.md#m671c46cff06fdbf7)（方法）
- [AntColonyMember.Destination · L149](baebd85c05598377.md#m24592f2914612d68)（方法）
- [ColonyFeeding.Find · L11](2cb56c08d08ff8ec.md#mfea041f7d0713775)（方法）
- [ColonyFeedingTemplateRegression.Check · L22](e3a5269cbeb35cff.md#m34b9c42c0fe6e75b)（方法）
- [EcologyQueries.Distance · L43](bdf7ccc2c9c0452f.md#m6e395dce655ebcac)（方法）
- [AntColonySettings.Current.get · L31](cb0a937e97c8fbb3.md#mcd121159f407ccdf)（访问器）
- [AntColonyMember.Role.get · L39](baebd85c05598377.md#mbe541ad55a93b0dd)（访问器）
- [AntColonyMember.Advance · L90](baebd85c05598377.md#m544c00700c48a3a6)（方法）
- [ColonyFeedingTemplateRegression.Food · L23](e3a5269cbeb35cff.md#m74b4dc2173d9759d)（方法）
- [ColonyResource.Feed · L94](293dbaecfff4b585.md#m173f86b0990bddd7)（方法）
- [AntColonyMember.Entity.get · L35](baebd85c05598377.md#m22b589e7146d5e42)（访问器）
- [ColonyResource.Create · L45](293dbaecfff4b585.md#m7e8d2b0e06e1cad5)（方法）
- [EcologyQueries.ToLocal · L40](bdf7ccc2c9c0452f.md#mfa1ace432c73012a)（方法）
- [AntColonyMember.State.get · L33](baebd85c05598377.md#ma804eb9f68138343)（访问器）
- [AntColonyMember.Activity.get · L51](baebd85c05598377.md#m1cdda6741f2e8201)（访问器）
- [ColonyFeeding.WantsFood · L10](2cb56c08d08ff8ec.md#m56d2edeb3860f143)（方法）
- [EcologyQueries.ToWorld · L41](bdf7ccc2c9c0452f.md#mc67423f4877c3534)（方法）
- [ColonyResource.Find · L28](293dbaecfff4b585.md#m1324285ab40ac6f4)（方法）
- [EntityStats.TryGet · L90](75aa86a368efdf24.md#m24ee1433e4a70d94)（方法）
- [PopulationBehavior.JoinColony · L123](21e4f62f75a9a80f.md#mbffd1e99ca203f0f)（方法）
- [AntColonyMember.Population.get · L36](baebd85c05598377.md#m3fa1d6d15c7c95ba)（访问器）
- [EntityTemplateManager.SaveTemplate · L30](ab5e6457e8a7aba0.md#me559754bdf2cc955)（方法）
- [EntityTemplateManager.Templates.get · L23](ab5e6457e8a7aba0.md#m45cc5b075f7bf4ec)（访问器）
- [ColonyFeedingTemplateRegression.VerifyStoreRecovery · L95](e3a5269cbeb35cff.md#m6cc7ca67479db277)（方法）
- [EntityStats.RestoreValues · L88](75aa86a368efdf24.md#m774f994eaa3183ca)（方法）
- [AntColonyMember.Restore · L208](baebd85c05598377.md#m4d9bb62c9ea9506b)（方法）
- [ColonyFeedingTemplateRegression.回调@30 · L30](e3a5269cbeb35cff.md#med102d65212e2c31)（回调/委托）
- [ColonyFeedingTemplateRegression.回调@31 · L31](e3a5269cbeb35cff.md#maf89cf4f4b0885cf)（回调/委托）
- [ColonyFeedingTemplateRegression.回调@32 · L32](e3a5269cbeb35cff.md#mc4b8445a52b3cc47)（回调/委托）
- [ColonyFeedingTemplateRegression.回调@83 · L83](e3a5269cbeb35cff.md#m12c9f96fa4f68616)（回调/委托）
- [ColonyFeedingTemplateRegression.回调@84 · L84](e3a5269cbeb35cff.md#m4a88778226363bf6)（回调/委托）

<a id="med102d65212e2c31"></a>

## 回调@30 · L30

`m … => 回调`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：m.transform.position"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="maf89cf4f4b0885cf"></a>

## 回调@31 · L31

`m … => 回调`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：m.GetComponent&lt;EntityStats&gt;().CaptureValues()"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [EntityStats.CaptureValues · L86](75aa86a368efdf24.md#mbf54fc67f0d07a99)（方法）

<a id="mc4b8445a52b3cc47"></a>

## 回调@32 · L32

`m … => 回调`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：m.Capture()"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [AntColonyMember.Capture · L206](baebd85c05598377.md#m6a32dab32b04b280)（方法）

<a id="m12c9f96fa4f68616"></a>

## 回调@83 · L83

`t … => 回调`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：t.templateName==name"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m4a88778226363bf6"></a>

## 回调@84 · L84

`t … => 回调`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：t.templateName==name"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m6cc7ca67479db277"></a>

## VerifyStoreRecovery · L95

`private static void VerifyStoreRecovery()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["path=Path.Combine(Folder,&quot;store-recovery.json&quot;)"]
  b2["new[]{&quot;&quot;,&quot;.bak&quot;,&quot;.tmp&quot;}"]
  b3{"判断：new[]{&quot;&quot;,&quot;.bak&quot;,&quot;.tmp&quot;}"}
  b4{"string<br/>判断：File.Exists(path+suffix)"}
  b5["File.Delete(path+suffix);"]
  b6{"new[]{&quot;&quot;,&quot;.bak&quot;,&quot;.tmp&quot;}<br/>判断：new[]{&quot;&quot;,&quot;.bak&quot;,&quot;.tmp&quot;}"}
  b7["new[]{&quot;&quot;,&quot;.bak&quot;,&quot;.tmp&quot;}"]
  b8["连接 / 离开区域"]
  b9["new EntityTemplateData{templateName=&quot;备份验证&quot;,genes=new List&lt;GeneInstanceData&gt;{new GeneInstanceData{definitionId=&quot;gene_population&quot;,value=20,tier=1}}}<br/>templateName=&quot;备份验证&quot;"]
  b10["new List&lt;GeneInstanceData&gt;{new GeneInstanceData{definitionId=&quot;gene_population&quot;,value=20,tier=1}}"]
  b11["new GeneInstanceData{definitionId=&quot;gene_population&quot;,value=20,tier=1}<br/>definitionId=&quot;gene_population&quot;<br/>value=20<br/>tier=1"]
  b12["genes=new List&lt;GeneInstanceData&gt;{new GeneInstanceData{definitionId=&quot;gene_population&quot;,value=20,tier=1}}"]
  b13["item=new EntityTemplateData{templateName=&quot;备份验证&quot;,genes=new List&lt;GeneInstanceData&gt;{new GeneInstanceData{definitionId=&quot;gene_population&quot;,value=20,tier=1}}}"]
  b14["new List&lt;EntityTemplateData&gt;{item}<br/>item<br/>EntityTemplateStore.Save(new List&lt;EntityTemplateData&gt;{item},path);"]
  b15["item.genes[0].value=21;"]
  b16["new List&lt;EntityTemplateData&gt;{item}<br/>item<br/>EntityTemplateStore.Save(new List&lt;EntityTemplateData&gt;{item},path);"]
  b17["File.WriteAllText(path,&quot;{invalid&quot;);<br/>Check(EntityTemplateStore.Load(path).Single().genes[0].value==20,&quot;主模板文件损坏可读取有效备份&quot;);<br/>EntityTemplateStore.Save(EntityTemplateStore.Load(path),path);"]
  b18{"判断：EntityTemplateStore.Load(path).Count==1"}
  b19["EntityTemplateStore.Load(path+&quot;.bak&quot;).Count==1"]
  b20["EntityTemplateStore.Load(path).Count==1"]
  b21["Check(EntityTemplateStore.Load(path).Count==1&amp;&amp;EntityTemplateStore.Load(path+&quot;.bak&quot;).Count==1,&quot;备份恢复后再次保存保留有效备份&quot;);"]
  b22(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否 · 经过finally 6"| b9
  b4 -->|"是"| b5
  b4 -->|"否"| b3
  b5 --> b3
  b6 -->|"否"| b7
  b6 -->|"是"| b8
  b7 --> b8
  b9 --> b10
  b10 --> b11
  b11 --> b12
  b12 --> b13
  b13 --> b14
  b14 --> b15
  b15 --> b16
  b16 --> b17
  b17 --> b18
  b18 -->|"是"| b19
  b18 -->|"否"| b20
  b19 --> b21
  b20 --> b21
  b21 --> b22
  b3 -. "退出保护区时 finally（示意）" .-> b6

```

项目内调用/回调引用：

- [EntityTemplateStore.Save · L51](2a560a9e3c003e7b.md#m853c3cfd96f490b9)（方法）
- [ColonyFeedingTemplateRegression.Check · L22](e3a5269cbeb35cff.md#m34b9c42c0fe6e75b)（方法）
- [EntityTemplateStore.Load · L34](2a560a9e3c003e7b.md#m8c4dc64a4aa5668b)（方法）

<a id="m79398cddef90eec8"></a>

## VerifyAfterRestart · L103

`public static void VerifyAfterRestart()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"expected=JsonUtility.FromJson&lt;EntityTemplateData&gt;(File.ReadAllText(Path.Combine(Folder,&quot;fixture.json&quot;)))<br/>actual=EntityTemplateManager.Templates.Single(t=&gt;t.templateName==expected.templateName)<br/>判断：JsonUtility.ToJson(expected)!=JsonUtility…"}
  b2["结果：new InvalidOperationException(&quot;重启后模板基因或名称变化&quot;)"]
  x2(["抛出异常 / 离开方法"])
  b3["File.WriteAllText(Path.Combine(Folder,&quot;restart.txt&quot;),&quot;PASS: 独立Unity进程从默认模板路径恢复名称、图标及全部基因数值/层级&quot;);<br/>EntityTemplateStore.Save(EntityTemplateManager.Templates.Where(t=&gt;t.templateName!=expected.templateName).ToList());<br/>Debug.Log(&quot;[TemplateRest…"]
  b4["(Exception ex)<br/>File.WriteAllText(Path.Combine(Folder,&quot;restart.txt&quot;),&quot;FAIL\n&quot;+ex);<br/>Debug.LogException(ex);<br/>EditorApplication.Exit(1);"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"Throw"| x2
  b3 --> b5
  b4 --> b5
  b1 -. "保护区可能异常（示意）" .-> b4

```

项目内调用/回调引用：

- [EntityTemplateManager.Templates.get · L23](ab5e6457e8a7aba0.md#m45cc5b075f7bf4ec)（访问器）
- [EntityTemplateStore.Save · L51](2a560a9e3c003e7b.md#m853c3cfd96f490b9)（方法）
- [ColonyFeedingTemplateRegression.回调@108 · L108](e3a5269cbeb35cff.md#m3e358a3ff0f59674)（回调/委托）
- [ColonyFeedingTemplateRegression.回调@112 · L112](e3a5269cbeb35cff.md#m662bea17524b18fa)（回调/委托）

<a id="m3e358a3ff0f59674"></a>

## 回调@108 · L108

`t … => 回调`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：t.templateName==expected.templateName"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m662bea17524b18fa"></a>

## 回调@112 · L112

`t … => 回调`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：t.templateName!=expected.templateName"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```
