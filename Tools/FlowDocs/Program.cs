using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

// ★ 新增：维护文档独立工具，读取全部Assets源码；使用编译器语法/语义与控制流，不改变游戏代码或玩家数据。
if(args.Contains("--self-test")){FlowTests.Run();return;}
var root=Path.GetFullPath(args.FirstOrDefault()??Path.Combine(AppContext.BaseDirectory,"../../../.."));
if(!Directory.Exists(Path.Combine(root,"Assets")))throw new ArgumentException("请传入Unity工程根目录");
new Atlas(root).Run();

sealed class Atlas(string root)
{
    public readonly string Root=root;
    readonly List<ScriptDoc> scripts=[];
    readonly Dictionary<string,MethodDoc> methods=[];
    readonly Dictionary<string,string> declarations=[];
    readonly List<(MethodDoc doc,SyntaxNode syntax,SemanticModel model,string profile)> work=[];
    readonly List<object> diagnostics=[];
    readonly JsonSerializerOptions json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=false,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    public static string Id(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16].ToLowerInvariant();
    public static string One(string s)=>Regex.Replace(s,@"\s+"," ").Trim();
    public string Relative(string p)=>Path.GetRelativePath(Root,p).Replace('\\','/');
    public void Run()
    {
        var files=Directory.GetFiles(Path.Combine(Root,"Assets"),"*.cs",SearchOption.AllDirectories).OrderBy(Relative,StringComparer.Ordinal).ToArray();
        foreach(var file in files){var text=File.ReadAllText(file).Replace("\r\n","\n");scripts.Add(new(){Id=Id(Relative(file)),Path=Relative(file),Module=Module(Relative(file)),Scope=Scope(Relative(file)),Source=text,Hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()});}
        var projects=Directory.GetFiles(Root,"*.csproj").OrderBy(p=>Path.GetFileName(p),StringComparer.Ordinal).ToDictionary(p=>Path.GetFileName(p)!,p=>XDocument.Load(p),StringComparer.OrdinalIgnoreCase);
        if(projects.Count==0)throw new InvalidOperationException("缺少Unity生成的.csproj引用。请先在Unity重新生成项目文件。");
        foreach(var profile in new[]{"Editor","Player"})
        {
            // ★ 修复（防Bug回归）：保留Unity程序集边界；合并为一个程序集会制造Environment命名遮蔽和netstandard重复类型的假错误。
            var cache=new Dictionary<string,CSharpCompilation>(StringComparer.OrdinalIgnoreCase);
            CSharpCompilation Build(string name)
            {
                if(cache.TryGetValue(name,out var cached))return cached;var project=projects[name];
                var symbols=project.Descendants("DefineConstants").SelectMany(x=>x.Value.Split(';')).Where(x=>x.Length>0).Distinct().ToArray();
                var defines=profile=="Editor"?symbols:symbols.Where(x=>!x.StartsWith("UNITY_EDITOR",StringComparison.Ordinal)).Append("UNITY_STANDALONE_WIN").ToArray();
                var parse=new CSharpParseOptions(LanguageVersion.Latest,preprocessorSymbols:defines);
                var includes=project.Descendants("Compile").Select(x=>x.Attribute("Include")?.Value).Where(x=>x!=null).Select(x=>Relative(Path.GetFullPath(Path.Combine(Root,x!)))).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var trees=scripts.Where(s=>includes.Contains(s.Path)&&(profile=="Editor"||s.Scope!="Editor")).Select(s=>CSharpSyntaxTree.ParseText(s.Source,parse,Path.Combine(Root,s.Path))).ToArray();
                var references=new List<MetadataReference>();
                foreach(string file in project.Descendants("HintPath").Select(x=>Path.GetFullPath(Path.Combine(Root,x.Value))).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))references.Add(MetadataReference.CreateFromFile(file));
                foreach(var reference in project.Descendants("ProjectReference"))
                {string? file=Path.GetFileName(reference.Attribute("Include")?.Value);if(file!=null&&projects.ContainsKey(file))references.Add(Build(file).ToMetadataReference());}
                var result=CSharpCompilation.Create(project.Descendants("AssemblyName").FirstOrDefault()?.Value??Path.GetFileNameWithoutExtension(name),trees,references,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));cache[name]=result;return result;
            }
            foreach(string name in projects.Keys)Build(name);
            var errors=cache.SelectMany(c=>c.Value.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).Select(d=>c.Key+": "+d)).ToArray();
            diagnostics.Add(new{profile,errors=errors.Length,details=errors.Take(40).ToArray()});foreach(var error in errors.Take(10))Console.WriteLine(error);
            int treeCount=0;
            foreach(var compilation in cache.Values)foreach(var tree in compilation.SyntaxTrees)
            {
                treeCount++;var script=scripts.Single(s=>s.Path==Relative(tree.FilePath));var model=compilation.GetSemanticModel(tree,true);
                var syntaxErrors=tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(syntaxErrors.Length>0)throw new InvalidDataException(string.Join("\n",syntaxErrors.Select(e=>e.ToString())));
                foreach(var type in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {string name=model.GetDeclaredSymbol(type)?.ToDisplayString()??type.Identifier.Text;if(!script.Types.Contains(name))script.Types.Add(name);}
                foreach(var node in tree.GetRoot().DescendantNodes().Where(IsCallable))
                {
                    var symbol=Symbol(model,node);string key=Key(node);string id=Id(script.Path+":"+key);
                    if(!methods.TryGetValue(id,out var doc))
                    {
                        string name=Name(node),owner=Owner(node);int line=node.GetLocation().GetLineSpan().StartLinePosition.Line+1;
                        doc=new(){Id=id,ScriptId=script.Id,Name=name,Owner=owner,Signature=Signature(node),Kind=Kind(node),Line=line,EndLine=node.GetLocation().GetLineSpan().EndLinePosition.Line+1,Summary=Summary(node),Source=node.ToString(),Entry=Entry(node,name),Symbol=symbol?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)??owner+"."+name};
                        methods.Add(id,doc);script.Methods.Add(id);
                    }
                    doc.Profiles.Add(profile);declarations[script.Path+":"+key]=id;work.Add((doc,node,model,profile));
                }
            }
            Console.WriteLine($"{profile}: {treeCount} scripts, semantic errors={errors.Length}");
        }
        foreach(var item in work)
        {
            var graph=Flow.Create(item.syntax,item.model);
            item.doc.Graphs[item.profile]=graph;
            item.doc.Calls.AddRange(Calls(item.doc,item.syntax,item.model,item.profile));
        }
        Shaders();
        Attachments();
        Implementations();
        foreach(var doc in methods.Values)
        {
            doc.Calls=doc.Calls.GroupBy(c=>(c.Line,c.Text,c.Kind,string.Join(",",c.Targets),c.Symbol,c.Status)).Select(g=>{var first=g.First();first.Profiles=g.Select(c=>c.Profile).Distinct().OrderBy(p=>p).ToList();return first;}).ToList();
            if(doc.Graphs.TryGetValue("Editor",out var editor)&&doc.Graphs.TryGetValue("Player",out var player)&&JsonSerializer.Serialize(editor,json)==JsonSerializer.Serialize(player,json)){doc.Graphs.Remove("Player");doc.GraphRefs["Player"]="Editor";}
            foreach(var call in doc.Calls)foreach(string target in call.Targets)if(methods.TryGetValue(target,out var called))called.Incoming.Add(new(){Method=doc.Id,Line=call.Line,Kind=call.Kind,Profile=call.Profile,Profiles=call.Profiles});
        }
        Write();
    }
    public static bool IsCallable(SyntaxNode n)=>n is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax||n is PropertyDeclarationSyntax {ExpressionBody:not null}||n is IndexerDeclarationSyntax {ExpressionBody:not null};
    static ISymbol? Symbol(SemanticModel m,SyntaxNode n)=>n is AnonymousFunctionExpressionSyntax?m.GetSymbolInfo(n).Symbol:m.GetDeclaredSymbol(n);
    static string Key(SyntaxNode n)=>n.SpanStart+":"+n.RawKind;
    static string Name(SyntaxNode n)=>n switch{MethodDeclarationSyntax m=>m.Identifier.Text,ConstructorDeclarationSyntax c=>c.Identifier.Text+".ctor",DestructorDeclarationSyntax d=>d.Identifier.Text+".finalize",LocalFunctionStatementSyntax l=>l.Identifier.Text,AccessorDeclarationSyntax a=>(a.Parent?.Parent switch{PropertyDeclarationSyntax p=>p.Identifier.Text,IndexerDeclarationSyntax=>"this[]",EventDeclarationSyntax e=>e.Identifier.Text,_=>"accessor"})+"."+a.Keyword.Text,PropertyDeclarationSyntax p=>p.Identifier.Text+".get",IndexerDeclarationSyntax=>"this[].get",OperatorDeclarationSyntax o=>"operator "+o.OperatorToken.Text,ConversionOperatorDeclarationSyntax c=>c.ImplicitOrExplicitKeyword.Text+" "+c.Type,AnonymousFunctionExpressionSyntax=>"回调@"+(n.GetLocation().GetLineSpan().StartLinePosition.Line+1),_=>n.Kind().ToString()};
    static string Kind(SyntaxNode n)=>n switch{LocalFunctionStatementSyntax=>"局部函数",AnonymousFunctionExpressionSyntax=>"回调/委托",AccessorDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax=>"访问器",ConstructorDeclarationSyntax=>"构造函数",DestructorDeclarationSyntax=>"析构函数",OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax=>"运算符",_=>"方法"};
    static string Owner(SyntaxNode n)=>string.Join(".",n.Ancestors().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(t=>t.Identifier.Text));
    static string Signature(SyntaxNode n){var body=n.ChildNodes().FirstOrDefault(c=>c is BlockSyntax or ArrowExpressionClauseSyntax);string text=body==null?n.ToString():n.ToString()[..Math.Max(0,body.SpanStart-n.SpanStart)];if(n is AnonymousFunctionExpressionSyntax)text=n switch{LambdaExpressionSyntax l=>l.AsyncKeyword+" "+l.GetFirstToken().Text+" … => 回调",_=>"delegate 回调"};return One(text).TrimEnd(';');}
    static string Summary(SyntaxNode n)=>One(string.Join(" ",n.GetLeadingTrivia().Where(t=>t.IsKind(SyntaxKind.SingleLineCommentTrivia)||t.IsKind(SyntaxKind.MultiLineCommentTrivia)||t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)).Select(t=>t.ToString()))).Replace("///","").Replace("//","").Trim();
    static string Module(string path)=>path.StartsWith("Assets/DarwinPark/",StringComparison.Ordinal)?path.Split('/')[2]:"Tutorial";
    static string Scope(string path)=>path.Contains("/Editor/",StringComparison.Ordinal)?"Editor":"Runtime";
    static readonly HashSet<string> UnityNames=["Awake","Start","Update","FixedUpdate","LateUpdate","OnEnable","OnDisable","OnDestroy","OnGUI","OnValidate","Reset","OnApplicationQuit","OnApplicationPause","OnApplicationFocus","OnCollisionEnter","OnCollisionStay","OnCollisionExit","OnTriggerEnter","OnTriggerStay","OnTriggerExit","OnDrawGizmos","OnDrawGizmosSelected"];
    static string Entry(SyntaxNode n,string name)
    {
        if(n is AnonymousFunctionExpressionSyntax)return "回调";
        if(n is LocalFunctionStatementSyntax)return "局部函数";
        if(n.ToString().Split('{')[0].Contains("RuntimeInitializeOnLoadMethod"))return "Unity运行初始化";
        if(n.ToString().Split('{')[0].Contains("MenuItem"))return "编辑器菜单";
        if(UnityNames.Contains(name)&&n.Ancestors().OfType<TypeDeclarationSyntax>().Any(t=>t.BaseList?.ToString().Contains("MonoBehaviour")==true||t.BaseList?.ToString().Contains("Editor")==true))return "Unity消息";
        return "普通调用";
    }
    IEnumerable<CallDoc> Calls(MethodDoc owner,SyntaxNode node,SemanticModel model,string profile)
    {
        foreach(var expression in node.DescendantNodes(n=>n==node||!IsCallable(n)).OfType<ExpressionSyntax>())
        {
            ISymbol? symbol=null,secondary=null;string kind="";
            if(expression is InvocationExpressionSyntax){symbol=model.GetSymbolInfo(expression).Symbol;kind=symbol is IMethodSymbol {MethodKind:MethodKind.DelegateInvoke}?"委托调用（运行时绑定）":"方法调用";}
            else if(expression is BinaryExpressionSyntax or PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax or CastExpressionSyntax)
            {if(model.GetSymbolInfo(expression).Symbol is IMethodSymbol op){symbol=op;kind="自定义运算符/转换";}}
            else if(expression is BaseObjectCreationExpressionSyntax){symbol=model.GetSymbolInfo(expression).Symbol;kind="构造";}
            else if(expression is MemberAccessExpressionSyntax or MemberBindingExpressionSyntax or IdentifierNameSyntax)
            {
                if(expression.Parent is MemberAccessExpressionSyntax parent&&parent.Name==expression||expression.Parent is MemberBindingExpressionSyntax||expression.Parent is InvocationExpressionSyntax inv&&inv.Expression==expression)continue;
                if(model.GetSymbolInfo(expression).Symbol is IPropertySymbol property)
                {
                    bool assignment=expression.Parent is AssignmentExpressionSyntax a&&a.Left==expression;
                    bool increment=expression.Parent is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax&&(expression.Parent.IsKind(SyntaxKind.PreIncrementExpression)||expression.Parent.IsKind(SyntaxKind.PreDecrementExpression)||expression.Parent.IsKind(SyntaxKind.PostIncrementExpression)||expression.Parent.IsKind(SyntaxKind.PostDecrementExpression));
                    bool compound=assignment&&!expression.Parent!.IsKind(SyntaxKind.SimpleAssignmentExpression)||increment;bool write=assignment||increment;
                    symbol=write?property.SetMethod:property.GetMethod;secondary=compound?property.GetMethod:null;kind=compound?"属性读写":write?"属性写入":"属性读取";
                }
            }
            if(kind.Length==0)continue;
            var targets=Resolve(symbol).Concat(Resolve(secondary)).Distinct().ToList();
            if(symbol is IMethodSymbol method&&(method.IsVirtual||method.IsAbstract||method.ContainingType?.TypeKind==TypeKind.Interface))kind+="（静态声明；动态分派）";
            string status=targets.Count>0?"项目源码":symbol==null?"未解析/动态":"外部API";
            yield return new(){Line=expression.GetLocation().GetLineSpan().StartLinePosition.Line+1,Text=One(expression.ToString()),Kind=kind,Symbol=symbol?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)??"",Targets=targets,Status=status,Profile=profile};
        }
        if(node is ConstructorDeclarationSyntax {Initializer:not null} constructor)
        {
            var target=model.GetSymbolInfo(constructor.Initializer).Symbol;yield return new(){Line=constructor.Initializer.GetLocation().GetLineSpan().StartLinePosition.Line+1,Text=One(constructor.Initializer.ToString()),Kind="构造链 base/this",Symbol=target?.ToDisplayString()??"",Targets=Resolve(target).ToList(),Status=Resolve(target).Any()?"项目源码":"外部API",Profile=profile};
        }
        // ★ 新增：委托注册是延后执行，单独登记回调，不能把AddListener当作此刻已执行回调。
        foreach(var callback in node.DescendantNodes(n=>n==node||!IsCallable(n)).Where(IsCallable))
        {string path=Relative(callback.SyntaxTree.FilePath);if(declarations.TryGetValue(path+":"+Key(callback),out var target))yield return new(){Line=callback.GetLocation().GetLineSpan().StartLinePosition.Line+1,Text=Signature(callback),Kind="回调/局部函数定义（并非立即调用）",Targets=[target],Status="延后绑定",Profile=profile};}
        foreach(var reference in node.DescendantNodes(n=>n==node||!IsCallable(n)).OfType<ExpressionSyntax>())
        {
            if(reference is not (IdentifierNameSyntax or MemberAccessExpressionSyntax)||reference.Parent is InvocationExpressionSyntax invocation&&invocation.Expression==reference||reference.Parent is MemberAccessExpressionSyntax)continue;
            if(model.GetSymbolInfo(reference).Symbol is not IMethodSymbol method||!Resolve(method).Any())continue;
            if(reference.Ancestors().TakeWhile(a=>a!=node).Any(a=>a is ArgumentSyntax||a is AssignmentExpressionSyntax))yield return new(){Line=reference.GetLocation().GetLineSpan().StartLinePosition.Line+1,Text=One(reference.ToString()),Kind="方法组/回调引用（非执行顺序）",Targets=Resolve(method).Distinct().ToList(),Status="延后绑定",Profile=profile};
        }
    }
    IEnumerable<string> Resolve(ISymbol? symbol)
    {
        if(symbol is IMethodSymbol m)symbol=(m.ReducedFrom??m).OriginalDefinition;
        if(symbol==null)yield break;
        foreach(var reference in symbol.DeclaringSyntaxReferences)
        {var syntax=reference.GetSyntax();string path=Relative(syntax.SyntaxTree.FilePath);if(declarations.TryGetValue(path+":"+Key(syntax),out var id))yield return id;}
    }
    void Shaders()
    {
        // ★ 新增：GPU脚本不伪装成C#语义。保留原始着色器/函数，控制结构按兼容C语法解析，函数名称链接明确标注为候选。
        foreach(var file in Directory.GetFiles(Path.Combine(Root,"Assets"),"*",SearchOption.AllDirectories).Where(f=>f.EndsWith(".shader")||f.EndsWith(".hlsl")).OrderBy(Relative))
        {
            string source=File.ReadAllText(file).Replace("\r\n","\n");var script=new ScriptDoc{Id=Id(Relative(file)),Path=Relative(file),Module="GPU",Scope="GPU",Source=source,Hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant()};scripts.Add(script);
            var pattern=new Regex(@"(?m)^\s*(?:(?:inline|static|const)\s+)*(?:[A-Za-z_]\w*)\s+(\w+)\s*\([^;{}]*\)\s*(?::\s*\w+)?\s*\{");
            foreach(Match match in pattern.Matches(source))
            {
                int start=source.IndexOf('{',match.Index+match.Length-1),end=Balanced(source,start);if(end<0)throw new InvalidDataException("GPU函数括号不平衡："+file);
                string body=source[start..(end+1)],clean=Regex.Replace(body,@"\[(?:loop|unroll|branch|flatten)(?:\([^\]]*\))?\]","");
                var tree=CSharpSyntaxTree.ParseText("class ShaderFlow { object Function() "+clean+" }",new CSharpParseOptions(LanguageVersion.Latest));
                var compilation=CSharpCompilation.Create("ShaderFlow",[tree],options:new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));var model=compilation.GetSemanticModel(tree,true);var syntax=tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
                int line=source[..(match.Index+match.Value.IndexOf(match.Groups[1].Value,StringComparison.Ordinal))].Count(c=>c=='\n')+1;int bodyLine=source[..start].Count(c=>c=='\n')+1;
                var doc=new MethodDoc{Id=Id(script.Path+":"+match.Index),ScriptId=script.Id,Name=match.Groups[1].Value,Owner=Path.GetFileNameWithoutExtension(file),Kind="GPU函数",Signature=One(source[match.Index..start]),Line=line,EndLine=source[..end].Count(c=>c=='\n')+1,Source=source[match.Index..(end+1)],Entry="GPU着色阶段/函数",Symbol=match.Groups[1].Value,Profiles=["GPU"]};
                var graph=Flow.Create(syntax,model);graph.Mode="GPU控制结构（名称链接，不作C#类型推断）";foreach(var n in graph.Nodes)n.Line+=bodyLine-1;doc.Graphs["GPU"]=graph;methods.Add(doc.Id,doc);script.Methods.Add(doc.Id);
            }
        }
        foreach(var script in scripts.Where(s=>s.Scope=="GPU"))foreach(var id in script.Methods)
            {
                var doc=methods[id];foreach(Match call in Regex.Matches(doc.Source,@"\b(\w+)\s*\("))
                {
                    if(call.Index<doc.Source.IndexOf('{'))continue;var targets=methods.Values.Where(m=>m.Kind=="GPU函数"&&m.Name==call.Groups[1].Value&&m.Id!=doc.Id).Select(m=>m.Id).ToList();if(targets.Count==0)continue;
                    doc.Calls.Add(new(){Line=doc.Line+doc.Source[..call.Index].Count(c=>c=='\n'),Text=call.Groups[1].Value,Kind="GPU函数名称候选",Targets=targets,Status="名称匹配，非类型证明",Profile="GPU"});
                }
            }
    }
    void Attachments()
    {
        // ★ 新增：从实际场景/预制体映射脚本GUID，区别静态挂载、运行时AddComponent与仅仅声明的消息函数。
        var guids=new Dictionary<string,ScriptDoc>(StringComparer.Ordinal);
        foreach(var script in scripts.Where(s=>s.Scope!="GPU")){string meta=Path.Combine(Root,script.Path)+".meta";if(File.Exists(meta)){var guid=Regex.Match(File.ReadAllText(meta),@"(?m)^guid: ([0-9a-f]{32})");if(guid.Success)guids[guid.Groups[1].Value]=script;}}
        foreach(var file in Directory.GetFiles(Path.Combine(Root,"Assets"),"*",SearchOption.AllDirectories).Where(f=>f.EndsWith(".unity")||f.EndsWith(".prefab")))
        {
            string yaml=File.ReadAllText(file);var blocks=Regex.Matches(yaml,@"(?ms)^--- !u!([0-9]+) &(-?[0-9]+).*?\r?\n(.*?)(?=^--- !u!|\z)").Cast<Match>().ToDictionary(m=>m.Groups[2].Value,m=>m.Groups[3].Value);
            foreach(var block in blocks.Values)
            {
                var guid=Regex.Match(block,@"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})");if(!guid.Success||!guids.TryGetValue(guid.Groups[1].Value,out var script))continue;
                string objectId=Regex.Match(block,@"m_GameObject: \{fileID: (-?[0-9]+)").Groups[1].Value;string name=blocks.TryGetValue(objectId,out var gameObject)?Regex.Match(gameObject,@"(?m)^  m_Name: (.*)").Groups[1].Value.Trim():objectId;
                script.Attachments.Add(new(){Asset=Relative(file),Object=name,Enabled=!Regex.IsMatch(block,@"(?m)^  m_Enabled: 0")});
            }
        }
    }
    void Implementations()
    {
        var declared=work.Where(w=>w.profile=="Editor").Select(w=>(w.doc,symbol:Symbol(w.model,w.syntax) as IMethodSymbol)).Where(w=>w.symbol!=null).ToArray();
        foreach(var candidate in declared)
        {
            var symbol=candidate.symbol!;
            for(var parent=symbol.OverriddenMethod;parent!=null;parent=parent.OverriddenMethod)foreach(string contract in Resolve(parent)){if(!methods[contract].Implementations.Contains(candidate.doc.Id))methods[contract].Implementations.Add(candidate.doc.Id);if(!candidate.doc.Contracts.Contains(contract))candidate.doc.Contracts.Add(contract);}
            foreach(var contractType in symbol.ContainingType.AllInterfaces)foreach(var member in contractType.GetMembers(symbol.Name).OfType<IMethodSymbol>())
            {
                var implementation=symbol.ContainingType.FindImplementationForInterfaceMember(member);if(implementation==null||!Resolve(implementation).Contains(candidate.doc.Id))continue;
                foreach(string contract in Resolve(member)){if(!methods[contract].Implementations.Contains(candidate.doc.Id))methods[contract].Implementations.Add(candidate.doc.Id);if(!candidate.doc.Contracts.Contains(contract))candidate.doc.Contracts.Add(contract);}
            }
        }
    }
    static int Balanced(string s,int start){int depth=0;bool line=false,block=false,quoted=false;for(int i=start;i<s.Length;i++){char c=s[i],next=i+1<s.Length?s[i+1]:'\0';if(line){if(c=='\n')line=false;continue;}if(block){if(c=='*'&&next=='/'){block=false;i++;}continue;}if(quoted){if(c=='\\'){i++;continue;}if(c=='"')quoted=false;continue;}if(c=='/'&&next=='/'){line=true;i++;continue;}if(c=='/'&&next=='*'){block=true;i++;continue;}if(c=='"'){quoted=true;continue;}if(c=='{')depth++;if(c=='}'&&--depth==0)return i;}return -1;}
    void Write()
    {
        string output=Path.Combine(Root,"Docs/运行流程图");Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"脚本"));
        var overviewPath=Path.Combine(Root,"Tools/FlowDocs/overview.json");var overview=File.Exists(overviewPath)?JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(overviewPath)):JsonSerializer.Deserialize<JsonElement>("[]");
        var manifest=new{version=1,scripts,methods=methods.Values.OrderBy(m=>m.ScriptId).ThenBy(m=>m.Line).ToArray(),diagnostics,overview,limits=new[]{"静态结构与调用关系，不等于运行时性能或完整执行轨迹。","Unity消息的跨组件顺序由引擎/场景/执行序配置决定，普通同名方法不自动认定为Unity入口。","接口/虚方法指向编译时声明；委托、事件、反射、字符串入口保留动态边界，不伪造实际目标。","异常边标注潜在处理入口；精确抛出位置、finally延后传播和协程/async恢复需结合源码与运行情况。","自动属性仅有隐式存储，图中标为声明；属性/字段初始化表达式保留在脚本源码视图。","GPU按兼容控制结构展示，名称匹配仅为候选。"}};
        string serialized=JsonSerializer.Serialize(manifest,json);File.WriteAllText(Path.Combine(output,"atlas-data.js"),"window.FLOW_ATLAS="+serialized+";\n",new UTF8Encoding(false));File.WriteAllText(Path.Combine(output,"manifest.json"),serialized,new UTF8Encoding(false));
        var pagesRoot=Path.GetFullPath(Path.Combine(output,"脚本"));var keepPages=scripts.Select(s=>s.Id+".md").ToHashSet(StringComparer.Ordinal);
        // ★ 新增：只清理本生成器hash命名的旧分图，删除/重命名脚本后不能留下误导维护者的过期页。
        foreach(var page in Directory.GetFiles(pagesRoot,"*.md"))if(Path.GetDirectoryName(Path.GetFullPath(page))==pagesRoot&&Regex.IsMatch(Path.GetFileName(page),@"^[a-f0-9]{16}\.md$")&&!keepPages.Contains(Path.GetFileName(page)))File.Delete(page);
        foreach(var script in scripts)
        {
            var md=new StringBuilder($"# {Path.GetFileName(script.Path)}\n\n[返回总索引](../README.md) · `{script.Scope}` · `{script.Path}`\n\n[源文件](../../../{script.Path}) · 源码校验：`{script.Hash}`\n\n");
            md.AppendLine("方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。\n");
            foreach(var id in script.Methods)
            {
                var method=methods[id];md.AppendLine($"<a id=\"m{method.Id}\"></a>\n\n## {method.Name} · L{method.Line}\n\n`{method.Signature.Replace("`","'")}`\n\n{method.Summary}\n");
                foreach(var pair in method.Graphs){md.AppendLine($"### {string.Join(" / ",method.Profiles.Where(p=>p==pair.Key||method.GraphRefs.GetValueOrDefault(p)==pair.Key))} · {pair.Value.Mode}\n\n```mermaid\n{Mermaid(pair.Value)}\n```\n");}
                var targets=method.Calls.SelectMany(c=>c.Targets).Distinct().Select(t=>methods[t]).ToArray();if(targets.Length>0){md.AppendLine("项目内调用/回调引用：\n");foreach(var target in targets){var file=scripts.Single(s=>s.Id==target.ScriptId);md.AppendLine($"- [{target.Owner}.{target.Name} · L{target.Line}]({file.Id}.md#m{target.Id})（{target.Kind}）");}md.AppendLine();}
            }
            if(script.Methods.Count==0)md.AppendLine("本文件没有显式方法体，属于数据/枚举/标记声明；字段与类型定义请在离线源码视图查看。\n");
            File.WriteAllText(Path.Combine(output,"脚本",script.Id+".md"),md.ToString().Replace("\r\n","\n").TrimEnd()+"\n",new UTF8Encoding(false));
        }
        var main=new StringBuilder("# 游戏业务主流程\n\n[返回总索引](README.md) · [离线可交互版本](index.html)\n\n这些图用于跨模块业务导航；虚线表示回调、异步传输或数据依赖，不把它们伪装成直接调用。精确的分支/循环/返回和真实调用目标请打开方法分图。\n\n");
        foreach(var flow in overview.EnumerateArray())
        {
            main.AppendLine("## "+flow.GetProperty("title").GetString()+"\n\n"+flow.GetProperty("description").GetString()+"\n");var graph=new GraphDoc();
            foreach(var node in flow.GetProperty("nodes").EnumerateArray())graph.Nodes.Add(new(){Id=node.GetProperty("id").GetString()!,Text=node.GetProperty("text").GetString()!,Kind=node.TryGetProperty("kind",out var kind)?kind.GetString()!:"step"});
            foreach(var edge in flow.GetProperty("edges").EnumerateArray())graph.Edges.Add(new(){From=edge[0].GetString()!,To=edge[1].GetString()!,Label=edge.GetArrayLength()>2?edge[2].GetString()!:"",Kind=edge.GetArrayLength()>3?edge[3].GetString()!:"flow"});
            main.AppendLine("```mermaid\n"+Mermaid(graph)+"\n```\n");
            foreach(var node in flow.GetProperty("nodes").EnumerateArray())if(node.TryGetProperty("target",out var target))
            {
                var method=methods.Values.FirstOrDefault(m=>m.Owner+"."+m.Name==target.GetString());if(method==null)throw new InvalidDataException("主流程方法不存在："+target.GetString());
                main.AppendLine($"- [{method.Owner}.{method.Name} · L{method.Line}](脚本/{method.ScriptId}.md#m{method.Id})");
            }
            if(flow.TryGetProperty("note",out var note))main.AppendLine("\n"+note.GetString()+"\n");
        }
        File.WriteAllText(Path.Combine(output,"主流程.md"),main.ToString().Replace("\r\n","\n").TrimEnd()+"\n",new UTF8Encoding(false));
        var stats=new{scripts=scripts.Count,csharp=scripts.Count(s=>s.Scope!="GPU"),gpu=scripts.Count(s=>s.Scope=="GPU"),methods=methods.Count,graphs=methods.Values.Sum(m=>m.Graphs.Count),profileGraphs=methods.Values.Sum(m=>m.Profiles.Count),nodes=methods.Values.Sum(m=>m.Graphs.Values.Sum(g=>g.Nodes.Count)),calls=methods.Values.Sum(m=>m.Calls.Count),internalLinks=methods.Values.Sum(m=>m.Calls.Sum(c=>c.Targets.Count)),unresolved=methods.Values.Sum(m=>m.Calls.Count(c=>c.Status=="未解析/动态")),hash=Id(string.Join("",scripts.Select(s=>s.Hash)))};
        File.WriteAllText(Path.Combine(output,"coverage.json"),JsonSerializer.Serialize(new{stats,diagnostics,scripts=scripts.Select(s=>new{s.Id,s.Path,s.Scope,methods=s.Methods.Count,s.Hash}),modes=methods.Values.SelectMany(m=>m.Graphs.Values).GroupBy(g=>g.Mode).Select(g=>new{mode=g.Key,count=g.Count()})},new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(false));
        var readme=new StringBuilder("# 游戏运行逻辑流程图手册\n\n**推荐打开 [离线交互手册](index.html)**：无需网络，可搜索脚本/方法、筛选运行时/编辑器/GPU、查看方法控制流程、调用/被调用关系、源码及条件编译分支，复制Mermaid。\n\n");
        readme.AppendLine($"[{overview.GetArrayLength()}条业务主流程](主流程.md) · [生成工具与维护说明](../../Tools/FlowDocs/README.md)\n\n覆盖 **{stats.csharp} 个C#脚本、{stats.gpu} 个Shader/HLSL文件、{stats.methods} 个方法/构造/访问器/局部函数/回调/GPU函数**。没有显式方法的数据脚本也在索引中。未扫描Unity Packages/Library生成文件；Tools为离线生成器，不进入游戏运行图。\n\n");
        readme.AppendLine("## 阅读与维护\n\n1. 先在首页阅读业务主流程，点击节点进入相关方法。\n2. 按模块或脚本查找方法。菱形为判断，圆角为入口/出口；蓝色为普通步骤，紫色为挂起/异步，虚线为异常或动态边界。\n3. 调用页显示编译时可确定的目标；事件注册、委托、接口/虚方法不是已证明的运行顺序。源码按钮显示完整原文与行号。\n4. Editor/Player分别分析；同一方法可切换条件编译视图。Shader/HLSL单独标记，不与CPU方法混淆。\n5. 更改源码后运行 `powershell -ExecutionPolicy Bypass -File Tools/FlowDocs/Generate.ps1`，再运行 `python Tools/FlowDocs/validate.py`。不要手工改生成数据或脚本分图。\n\n");
        readme.AppendLine("## 分析边界\n\n- 编译器控制流块反映局部分支/循环/返回；不测量运行时间，也不规定不同MonoBehaviour的全部先后顺序。\n- catch入口用潜在异常边标注，finally与异步/协程恢复保留区域和语句，仍需结合源码看异常传播。\n- 无方法体的接口/自动访问器标为声明；动态调用和外部引擎/系统API明确列出，不凭名称伪造实现。\n- GPU函数通过兼容语法抽取控制结构；名称链接只表示候选，着色器宏/include的完整执行需结合渲染管线。\n- 精确覆盖与源码SHA256见 [coverage.json](coverage.json)。语义分析诊断一并公开，不能把未解析调用当作没有调用。\n\n## 全部脚本\n\n|模块|脚本|范围|方法/回调|\n|---|---|---|---|\n");
        foreach(var script in scripts)readme.AppendLine($"|{script.Module}|[{script.Path}](脚本/{script.Id}.md)|{script.Scope}|{script.Methods.Count}|");
        File.WriteAllText(Path.Combine(output,"README.md"),readme.ToString().Replace("\r\n","\n").TrimEnd()+"\n",new UTF8Encoding(false));Console.WriteLine(JsonSerializer.Serialize(stats));
    }
    public static string Mermaid(GraphDoc graph)
    {
        string Label(string s)=>s.Replace("&","&amp;").Replace("\"","&quot;").Replace("<","&lt;").Replace(">","&gt;").Replace("|","&#124;").Replace("\n","<br/>");
        var sb=new StringBuilder("flowchart TD\n");foreach(var n in graph.Nodes){string label=Label(n.Text.Length>240?n.Text[..237]+"…":n.Text);sb.AppendLine(n.Kind=="decision"?$"  {n.Id}{{\"{label}\"}}":n.Kind is "entry" or "exit"?$"  {n.Id}([\"{label}\"])":$"  {n.Id}[\"{label}\"]");}foreach(var e in graph.Edges){string label=Label(e.Label);sb.AppendLine(e.Label.Length==0?$"  {e.From} {(e.Kind=="exception"?"-.->":"-->")} {e.To}":e.Kind=="exception"?$"  {e.From} -. \"{label}\" .-> {e.To}":$"  {e.From} -->|\"{label}\"| {e.To}");}return sb.ToString();
    }
}

sealed class ScriptDoc {public string Id{get;set;}="";public string Path{get;set;}="";public string Module{get;set;}="";public string Scope{get;set;}="";public string Hash{get;set;}="";public string Source{get;set;}="";public List<string> Types{get;set;}=[];public List<string> Methods{get;set;}=[];public List<Attachment> Attachments{get;set;}=[];}
sealed class MethodDoc {public string Id{get;set;}="";public string ScriptId{get;set;}="";public string Name{get;set;}="";public string Owner{get;set;}="";public string Signature{get;set;}="";public string Kind{get;set;}="";public string Entry{get;set;}="";public string Symbol{get;set;}="";public int Line{get;set;}public int EndLine{get;set;}public string Summary{get;set;}="";[JsonIgnore] public string Source{get;set;}="";public List<string> Implementations{get;set;}=[];public List<string> Contracts{get;set;}=[];public Dictionary<string,string> GraphRefs{get;set;}=[];public List<string> Profiles{get;set;}=[];public Dictionary<string,GraphDoc> Graphs{get;set;}=[];public List<CallDoc> Calls{get;set;}=[];public List<Incoming> Incoming{get;set;}=[];}
sealed class Incoming {public string Method{get;set;}="";public int Line{get;set;}public string Kind{get;set;}="";public string Profile{get;set;}="";public List<string> Profiles{get;set;}=[];}
sealed class CallDoc {public int Line{get;set;}public string Text{get;set;}="";public string Kind{get;set;}="";public string Symbol{get;set;}="";public string Status{get;set;}="";public string Profile{get;set;}="";public List<string> Profiles{get;set;}=[];public List<string> Targets{get;set;}=[];}
sealed class GraphDoc {public string Mode{get;set;}="";public List<NodeDoc> Nodes{get;set;}=[];public List<EdgeDoc> Edges{get;set;}=[];public List<string> Notes{get;set;}=[];}
sealed class NodeDoc {public string Id{get;set;}="";public string Text{get;set;}="";public string Kind{get;set;}="step";public int Line{get;set;}public bool Reachable{get;set;}=true;}
sealed class EdgeDoc {public string From{get;set;}="";public string To{get;set;}="";public string Label{get;set;}="";public string Kind{get;set;}="flow";}

sealed class Attachment {public string Asset{get;set;}="";public string Object{get;set;}="";public bool Enabled{get;set;}}
