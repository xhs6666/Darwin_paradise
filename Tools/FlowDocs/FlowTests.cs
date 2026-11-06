using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ★ 新增：用独立小型源码核对编译器分支/循环/异常/协程/回调与条件编译，而非让生成器自报成功。
static class FlowTests
{
    public static void Run()
    {
        const string code="""
        using System;using System.Collections.Generic;using System.Threading.Tasks;
        class Fixture {
          public int Branch(int n){if(n<0)return -1;int total=0;for(int i=0;i<n;i++){if(i==2)continue;if(i>8)break;total+=i;}switch(n){case 0:return 0;case 1:total++;break;default:total+=2;break;}return total;}
          public int Catch(int n){try{if(n==0)throw new Exception();return n;}catch(Exception){return -2;}finally{Console.WriteLine(n);}}
          public IEnumerable<int> Yield(){yield return 1;yield return 2;yield break;}
          public async Task<int> Await(){await Task.Delay(1);return 2;}
          public int Nested(){int Twice(int a){if(a<0)return -1;return a*2;}Func<int,int> cb=x=>x>0?Twice(x):0;return cb(3);}
          public Func<Task> Deferred(){return async ()=>{await Task.Delay(1);};}
          public int Auto {get;set;}
          public int Expression=>Auto>0?Auto:0;
          public int Profile(){
        #if UNITY_EDITOR
          return 1;
        #else
          return 2;
        #endif
          }
        }
        """;
        int count=0;void Check(bool value,string message){if(!value)throw new InvalidDataException(message);count++;}
        var refs=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p=>MetadataReference.CreateFromFile(p)).ToArray();
        var parsed=CSharpSyntaxTree.ParseText(code,new CSharpParseOptions(LanguageVersion.Latest,preprocessorSymbols:["UNITY_EDITOR"]));var compilation=CSharpCompilation.Create("Fixture",[parsed],refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Check(!compilation.GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error),"Fixture compilation");var model=compilation.GetSemanticModel(parsed);var root=parsed.GetRoot();
        GraphDoc Method(string name)=>Flow.Create(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n=>n.Identifier.Text==name),model);
        var branch=Method("Branch");Check(branch.Mode.StartsWith("Roslyn"),"Must use compiler CFG");Check(branch.Nodes.Count(n=>n.Kind=="decision")>=4,"Branches/loop/switch preserved");Check(branch.Edges.Count(e=>e.Label.Contains("返回"))>=2,"Early returns preserved");
        var catcher=Method("Catch");Check(catcher.Notes.Any(n=>n.Contains("Catch"))&&catcher.Notes.Any(n=>n.Contains("Finally")),"Exception/finally regions preserved");Check(catcher.Edges.Any(e=>e.Kind=="exception"),"Potential exception edge marked");
        Check(Method("Yield").Nodes.Any(n=>n.Kind=="suspend"),"Yield suspension preserved");Check(Method("Await").Nodes.Any(n=>n.Kind=="suspend"),"Await suspension preserved");
        var local=root.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single();Check(Flow.Create(local,model).Mode.StartsWith("Roslyn"),"Local function compiler CFG");var lambda=root.DescendantNodes().OfType<LambdaExpressionSyntax>().First(l=>l.Body is ConditionalExpressionSyntax);Check(Flow.Create(lambda,model).Mode.StartsWith("Roslyn"),"Callback compiler CFG");
        Check(!Method("Deferred").Nodes.Any(n=>n.Kind=="suspend"),"Defining async callback does not suspend outer method");
        foreach(var node in root.DescendantNodes().Where(Atlas.IsCallable))
        {var graph=Flow.Create(node,model);var ids=graph.Nodes.Select(n=>n.Id).ToHashSet();Check(graph.Nodes.Any(n=>n.Kind=="entry")&&graph.Edges.All(e=>ids.Contains(e.From)&&ids.Contains(e.To)),"Every callable has valid graph");}
        var player=CSharpSyntaxTree.ParseText(code,new CSharpParseOptions(LanguageVersion.Latest));var pc=CSharpCompilation.Create("FixturePlayer",[player],refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));var pn=player.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n=>n.Identifier.Text=="Profile");
        Check(Atlas.Mermaid(Method("Profile"))!=Atlas.Mermaid(Flow.Create(pn,pc.GetSemanticModel(player))),"Conditional compilation graphs differ");
        Console.WriteLine("FlowDocs self-test PASS: "+count);
    }
}
