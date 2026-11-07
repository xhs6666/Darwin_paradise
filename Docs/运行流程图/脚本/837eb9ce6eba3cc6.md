# GeneValueResolver.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/Genetics/Evaluation/GeneValueResolver.cs`

[源文件](../../../Assets/DarwinPark/Genetics/Evaluation/GeneValueResolver.cs) · 源码校验：`b79f319d6f79699ade8463e1421139b6c7d6fb7256ba3f763aa08ad92bfcf443`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m677371b155e67c41"></a>

## IsActive.get · L17

`public bool IsActive`



### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["EffectiveStrength &gt;= MinimumActiveStrength"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m063abac8252cd4e8"></a>

## Resolve · L32

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
  b11{"判断：def.SuppressionOnlyWhileGestating"}
  b12{"判断：isGestating"}
  b13["def.SuppressionRules"]
  b14{"判断：def.SuppressionRules"}
  b15["var"]
  b16["genome.FindByDefinition(rule.TargetGeneId)"]
  b17{"判断：genome.FindByDefinition(rule.TargetGeneId)"}
  b18["var"]
  b19{"判断：target.Enabled"}
  b20["suppressions.TryGetValue(target, out var cur);<br/>suppressions[target] = cur + gene.Value * rule.Coefficient / 1000f;"]
  b21{"判断：genome.FindByDefinition(rule.TargetGeneId)"}
  b22["genome.FindByDefinition(rule.TargetGeneId)"]
  b23["连接 / 离开区域"]
  b24["def.SuppressionRules"]
  b25["genome.Genes"]
  b26["genome.Genes"]
  b27{"判断：genome.Genes"}
  b28["var"]
  b29{"判断：gene.Enabled"}
  b30{"def = GeneDatabase.Get(gene.DefinitionId)<br/>判断：def == null"}
  b31{"suppressions.TryGetValue(gene, out var sup);<br/>role=0<br/>判断：additionalSuppression!=null"}
  b32{"判断：additionalSuppression.TryGetValue(gene.DefinitionId,out float extra)"}
  b33["role=Mathf.Max(0,extra);<br/>sup+=role;"]
  b34{"判断：string.IsNullOrEmpty(suppressExemptGeneId)"}
  b35["gene.DefinitionId == suppressExemptGeneId"]
  b36["!string.IsNullOrEmpty(suppressExemptGeneId)"]
  b37["exempt = !string.IsNullOrEmpty(suppressExemptGeneId) &amp;&amp; gene.DefinitionId == suppressExemptGeneId"]
  b38{"判断：exempt"}
  b39["0f"]
  b40["Mathf.Max(0f, globalSuppression)"]
  b41["global = exempt ? 0f : Mathf.Max(0f, globalSuppression)"]
  b42["effectiveValue = Mathf.Max(0f, gene.Value - sup - global)"]
  b43["new GeneEvaluation { Gene = gene, Definition = def, State = def.ResolveState(gene.Value, gene.MissingStateIds), // ★ 修复（防Bug回归）：面板同时显示环境抑制，保证有效值与抑制值一致。 SuppressionAmount = sup + global, // ★ 适配：仅记录来源数值供界面解释，不额外拼接字符串或改变求值。 EnvironmentSupp…"]
  b44["result.Genes.Add(eval);"]
  b45["genome.Genes"]
  b46["result.Attributes = GeneAttributeInheritance.Accumulate(result.Genes);<br/>结果：result"]
  b47(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b47
  b3 --> b4
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否 · 经过finally 25"| b26
  b6 --> b7
  b7 -->|"否"| b5
  b7 -->|"是"| b8
  b8 -->|"否"| b9
  b8 -->|"是"| b5
  b9 -->|"是"| b5
  b9 -->|"否"| b10
  b10 -->|"否"| b11
  b10 -->|"是"| b12
  b11 -->|"是"| b12
  b11 -->|"否"| b13
  b12 -->|"否"| b5
  b12 -->|"是"| b13
  b13 --> b14
  b14 -->|"是"| b15
  b14 -->|"否 · 经过finally 24"| b5
  b15 --> b16
  b16 --> b17
  b17 -->|"是"| b18
  b17 -->|"否 · 经过finally 21"| b14
  b18 --> b19
  b19 -->|"否"| b17
  b19 -->|"是"| b20
  b20 --> b17
  b21 -->|"否"| b22
  b21 -->|"是"| b23
  b22 --> b23
  b26 --> b27
  b27 -->|"是"| b28
  b27 -->|"否 · 经过finally 45"| b46
  b28 --> b29
  b29 -->|"否"| b27
  b29 -->|"是"| b30
  b30 -->|"是"| b27
  b30 -->|"否"| b31
  b31 -->|"是"| b32
  b31 -->|"否"| b34
  b32 -->|"是"| b33
  b32 -->|"否"| b34
  b33 --> b34
  b34 -->|"否"| b35
  b34 -->|"是"| b36
  b35 --> b37
  b36 --> b37
  b37 --> b38
  b38 -->|"是"| b39
  b38 -->|"否"| b40
  b39 --> b41
  b40 --> b41
  b41 --> b42
  b42 --> b43
  b43 --> b44
  b44 --> b27
  b46 -->|"返回"| b47
  b17 -. "退出保护区时 finally（示意）" .-> b21
  b14 -. "退出保护区时 finally（示意）" .-> b24
  b5 -. "退出保护区时 finally（示意）" .-> b25
  b27 -. "退出保护区时 finally（示意）" .-> b45

```

项目内调用/回调引用：

- [GeneDatabase.Get · L136](ec0a03483435a863.md#me6a5fefe6c3492f9)（方法）
- [Genome.FindByDefinition · L14](a2ef23f865bc7ee8.md#m2a51173f07ba445c)（方法）
- [GeneDefinition.ResolveState · L85](07f99d5784e3dadb.md#mf00205d71765b81f)（方法）
- [GeneAttributeInheritance.Accumulate · L39](cbba0870e05d6571.md#m94b9ccbc18db141d)（方法）
