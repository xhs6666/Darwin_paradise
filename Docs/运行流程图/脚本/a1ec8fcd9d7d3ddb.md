# EntitySaveState.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Entities/EntitySaveState.cs`

[源文件](../../../Assets/DarwinPark/Entities/EntitySaveState.cs) · 源码校验：`eff3b07a82ee727f6e7f7ff021dc769a74bc87f7f58171bb91542566e18c0140`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="mc67502b381bc2751"></a>

## Capture · L21

`public static EntitySaveState Capture(DarwinEntity entity)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["genes=entity.Genes"]
  b2["new EntitySaveState{name=entity.DisplayName,position=entity.transform.position,rotation=entity.transform.rotation,scale=entity.transform.localScale, genome=JsonUtility.FromJson&lt;Genome&gt;(JsonUtility.ToJson(genes.Genome)),age=genes.CurrentA…"]
  b3{"entity.GetComponent&lt;EntityLifeCycle&gt;()<br/>判断：entity.GetComponent&lt;EntityLifeCycle&gt;()"}
  b4[".Progress"]
  b5["entity.GetComponent&lt;EntityLifeCycle&gt;()"]
  b6{"判断：entity.GetComponent&lt;EntityLifeCycle&gt;()?.Progress"}
  b7["entity.GetComponent&lt;EntityLifeCycle&gt;()?.Progress"]
  b8["0"]
  b9["lifeProgress=entity.GetComponent&lt;EntityLifeCycle&gt;()?.Progress??0"]
  b10["mutationChance=genes.mutationChance<br/>mutatedStages=genes.MutatedStageMask<br/>stageChecks=genes.StageMutationChecks<br/>gestating=genes.IsGestating<br/>energy=entity.Energy<br/>traits=new List&lt;string&gt;(entity.Traits)<br/>stats=entity.GetComponent&lt;EntityStats&gt;…"]
  b11["genes.Genome.Genes"]
  b12{"判断：genes.Genome.Genes"}
  b13["var"]
  b14{"判断：genes.GetLogic(gene) is ReproductionGeneLogic reproduction"}
  b15["state.reproduction.Add(reproduction.CaptureState());"]
  b16["genes.Genome.Genes"]
  b17["state.ecology=Ecology.EcologySaveState.Capture(entity);<br/>结果：state"]
  b18(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"否"| b4
  b3 -->|"是"| b5
  b4 --> b6
  b5 --> b6
  b6 -->|"否"| b7
  b6 -->|"是"| b8
  b7 --> b9
  b8 --> b9
  b9 --> b10
  b10 --> b11
  b11 --> b12
  b12 -->|"是"| b13
  b12 -->|"否 · 经过finally 16"| b17
  b13 --> b14
  b14 -->|"是"| b15
  b14 -->|"否"| b12
  b15 --> b12
  b17 -->|"返回"| b18
  b12 -. "退出保护区时 finally（示意）" .-> b16

```

项目内调用/回调引用：

- [DarwinEntity.Genes.get · L14](70798dde169f2e8b.md#m622eb1adf401d431)（访问器）
- [EntityGeneController.CurrentAge.get · L35](7494d53178552f0e.md#mf5bc28691dd4a0f3)（访问器）
- [EntityLifeCycle.Progress.get · L10](9f4138bfbae654b7.md#m178185fd5f5a0d74)（访问器）
- [EntityGeneController.StageMutationChecks.get · L42](7494d53178552f0e.md#m7910c7079ddd1c25)（访问器）
- [EntityStats.CaptureValues · L86](75aa86a368efdf24.md#mbf54fc67f0d07a99)（方法）
- [EntityGeneController.GetLogic · L184](7494d53178552f0e.md#m4f4e58e1a4abadfc)（方法）
- [ReproductionGeneLogic.CaptureState · L33](ac319ea70bd8b723.md#mb3b414bac984dd2e)（方法）
- [EcologySaveState.Capture · L29](dd1dde4b4c192659.md#m25ad9f5dc9a5059e)（方法）

<a id="m047b691a146deabd"></a>

## Restore · L31

`public void Restore(DarwinEntity entity)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["entity.transform.SetPositionAndRotation(position,rotation);<br/>entity.transform.localScale=scale;<br/>entity.DisplayName=name;<br/>genes=entity.Genes<br/>genes.mutationChance=mutationChance;<br/>genes.IsGestating=gestating;<br/>genes.RestoreStageState(age,muta…"]
  b2["reproduction"]
  b3{"判断：reproduction"}
  b4["var"]
  b5{"gene=genes.Genome.Find(entry.gene)<br/>判断：genes.GetLogic(gene) is ReproductionGeneLogic logic"}
  b6["logic.RestoreState(entry);"]
  b7["reproduction"]
  b8["traits"]
  b9{"判断：traits"}
  b10["var<br/>entity.EnableTrait(trait);"]
  b11["traits"]
  b12{"entity.AddEnergy(energy-entity.Energy);<br/>判断：ecology!=null"}
  b13{"判断：ecology.IsEmpty"}
  b14["ecology.Restore(entity);"]
  b15(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否 · 经过finally 7"| b8
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否"| b3
  b6 --> b3
  b8 --> b9
  b9 -->|"是"| b10
  b9 -->|"否 · 经过finally 11"| b12
  b10 --> b9
  b12 -->|"是"| b13
  b12 -->|"否"| b15
  b13 -->|"否"| b14
  b13 -->|"是"| b15
  b14 --> b15
  b3 -. "退出保护区时 finally（示意）" .-> b7
  b9 -. "退出保护区时 finally（示意）" .-> b11

```

项目内调用/回调引用：

- [DarwinEntity.Genes.get · L14](70798dde169f2e8b.md#m622eb1adf401d431)（访问器）
- [EntityGeneController.RestoreStageState · L45](7494d53178552f0e.md#m49489af39061f1a1)（方法）
- [EntityLifeCycle.RestoreProgress · L48](9f4138bfbae654b7.md#mfc75121476d49679)（方法）
- [EntityGeneController.Recalculate · L90](7494d53178552f0e.md#m93744c8f3039b93c)（方法）
- [EntityStats.RestoreValues · L88](75aa86a368efdf24.md#m774f994eaa3183ca)（方法）
- [Genome.Find · L11](a2ef23f865bc7ee8.md#mfd5b2c58563def29)（方法）
- [EntityGeneController.GetLogic · L184](7494d53178552f0e.md#m4f4e58e1a4abadfc)（方法）
- [ReproductionGeneLogic.RestoreState · L35](ac319ea70bd8b723.md#m2e3c6341cef3b9c0)（方法）
- [DarwinEntity.EnableTrait · L34](70798dde169f2e8b.md#meef47c363e5995a8)（方法）
- [DarwinEntity.AddEnergy · L38](70798dde169f2e8b.md#m6d61b27a87e2f477)（方法）
- [EcologySaveState.Restore · L42](dd1dde4b4c192659.md#m91672bc75a4694f7)（方法）
