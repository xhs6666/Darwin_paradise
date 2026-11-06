# GeneValueResolver.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Genetics/Evaluation/GeneValueResolver.cs`

[源文件](../../../Assets/DarwinPark/Genetics/Evaluation/GeneValueResolver.cs) · 源码校验：`adaa177b4eaa015dbf5057b0550fd5901e46822c4e1fbd4913f9a1fe797066f3`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="mb9f01f6a053abe5c"></a>

## IsActive.get · L15

`public bool IsActive`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["EffectiveStrength &gt;= 10f"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="md4b9129afb709ddb"></a>

## Resolve · L30

`public static GenomeEvaluation Resolve(Genome genome, bool isGestating = false, float globalSuppression = 0f, string suppressExemptGeneId = null,IReadOnlyDictionary<string,float> additionalSuppression=null)`

<summary>  ★ 修复（防Bug回归）：默认把环境压力施加给所有基因；遗传值不受影响，  环境适应突变仍可发生。可选豁免参数仅供显式需要豁免的调用使用。  </summary>

### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"result = new GenomeEvaluation()<br/>判断：genome == null"}
  b2["结果：result"]
  b3["suppressions = new Dictionary&lt;GeneInstance, float&gt;()"]
  b4["genome.Genes"]
  b5{"判断：genome.Genes"}
  b6["var"]
  b7{"判断：gene.Enabled"}
  b8{"def = GeneDatabase.Get(gene.DefinitionId)<br/>判断：def == null"}
  b9{"判断：def.SuppressionRules == null"}
  b10{"判断：gene.DefinitionId == &quot;gene_reproduction&quot;"}
  b11{"判断：isGestating"}
  b12["def.SuppressionRules"]
  b13{"判断：def.SuppressionRules"}
  b14["var"]
  b15["genome.FindByDefinition(rule.TargetGeneId)"]
  b16{"判断：genome.FindByDefinition(rule.TargetGeneId)"}
  b17["var"]
  b18{"判断：target.Enabled"}
  b19["suppressions.TryGetValue(target, out var cur);<br/>suppressions[target] = cur + gene.Value * rule.Coefficient / 1000f;"]
  b20{"判断：genome.FindByDefinition(rule.TargetGeneId)"}
  b21["genome.FindByDefinition(rule.TargetGeneId)"]
  b22["连接 / 离开区域"]
  b23["def.SuppressionRules"]
  b24["genome.Genes"]
  b25["genome.Genes"]
  b26{"判断：genome.Genes"}
  b27["var"]
  b28{"判断：gene.Enabled"}
  b29{"def = GeneDatabase.Get(gene.DefinitionId)<br/>判断：def == null"}
  b30{"suppressions.TryGetValue(gene, out var sup);<br/>判断：additionalSuppression!=null"}
  b31{"判断：additionalSuppression.TryGetValue(gene.DefinitionId,out float extra)"}
  b32["sup+=Mathf.Max(0,extra);"]
  b33{"判断：string.IsNullOrEmpty(suppressExemptGeneId)"}
  b34["gene.DefinitionId == suppressExemptGeneId"]
  b35["!string.IsNullOrEmpty(suppressExemptGeneId)"]
  b36["exempt = !string.IsNullOrEmpty(suppressExemptGeneId) &amp;&amp; gene.DefinitionId == suppressExemptGeneId"]
  b37{"判断：exempt"}
  b38["0f"]
  b39["Mathf.Max(0f, globalSuppression)"]
  b40["global = exempt ? 0f : Mathf.Max(0f, globalSuppression)"]
  b41["effectiveValue = Mathf.Max(0f, gene.Value - sup - global)"]
  b42["new GeneEvaluation { Gene = gene, Definition = def, State = def.ResolveState(gene.Value, gene.MissingStateIds), // ★ 修复（防Bug回归）：面板同时显示环境抑制，保证有效值与抑制值一致。 SuppressionAmount = sup + global, EffectiveStrength = effectiveValue }<br/>Gene = gene<br/>De…"]
  b43["result.Genes.Add(eval);"]
  b44["genome.Genes"]
  b45["result.Attributes = GeneAttributeInheritance.Accumulate(result.Genes);<br/>结果：result"]
  b46(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b46
  b3 --> b4
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否 · 经过finally 24"| b25
  b6 --> b7
  b7 -->|"否"| b5
  b7 -->|"是"| b8
  b8 -->|"否"| b9
  b8 -->|"是"| b5
  b9 -->|"是"| b5
  b9 -->|"否"| b10
  b10 -->|"是"| b11
  b10 -->|"否"| b12
  b11 -->|"否"| b5
  b11 -->|"是"| b12
  b12 --> b13
  b13 -->|"是"| b14
  b13 -->|"否 · 经过finally 23"| b5
  b14 --> b15
  b15 --> b16
  b16 -->|"是"| b17
  b16 -->|"否 · 经过finally 20"| b13
  b17 --> b18
  b18 -->|"否"| b16
  b18 -->|"是"| b19
  b19 --> b16
  b20 -->|"否"| b21
  b20 -->|"是"| b22
  b21 --> b22
  b25 --> b26
  b26 -->|"是"| b27
  b26 -->|"否 · 经过finally 44"| b45
  b27 --> b28
  b28 -->|"否"| b26
  b28 -->|"是"| b29
  b29 -->|"是"| b26
  b29 -->|"否"| b30
  b30 -->|"是"| b31
  b30 -->|"否"| b33
  b31 -->|"是"| b32
  b31 -->|"否"| b33
  b32 --> b33
  b33 -->|"否"| b34
  b33 -->|"是"| b35
  b34 --> b36
  b35 --> b36
  b36 --> b37
  b37 -->|"是"| b38
  b37 -->|"否"| b39
  b38 --> b40
  b39 --> b40
  b40 --> b41
  b41 --> b42
  b42 --> b43
  b43 --> b26
  b45 -->|"返回"| b46
  b16 -. "退出保护区时 finally（示意）" .-> b20
  b13 -. "退出保护区时 finally（示意）" .-> b23
  b5 -. "退出保护区时 finally（示意）" .-> b24
  b26 -. "退出保护区时 finally（示意）" .-> b44

```

项目内调用/回调引用：

- [GeneDatabase.Get · L135](ec0a03483435a863.md#m8f3bbfb35f3ca397)（方法）
- [Genome.FindByDefinition · L14](a2ef23f865bc7ee8.md#m2a51173f07ba445c)（方法）
- [GeneDefinition.ResolveState · L83](07f99d5784e3dadb.md#mbaf2c5b1eb1045c5)（方法）
- [GeneAttributeInheritance.Accumulate · L39](cbba0870e05d6571.md#m94b9ccbc18db141d)（方法）
