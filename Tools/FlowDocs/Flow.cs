using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

// ★ 新增：优先使用Roslyn基本块；无法单独创建的局部/匿名函数从外层CFG取出，仍不可用时保留完整语法结构并明确标记。
static class Flow
{
    public static GraphDoc Create(SyntaxNode node,SemanticModel model)
    {
        try{var cfg=Find(node,model);if(cfg!=null)return FromCfg(cfg);}catch(ArgumentException){}catch(InvalidOperationException){}
        return new SyntaxFlow().Build(node);
    }
    static ControlFlowGraph? Find(SyntaxNode node,SemanticModel model)
    {
        if(node is LocalFunctionStatementSyntax local)
        {var parent=node.Ancestors().FirstOrDefault(Atlas.IsCallable);var cfg=parent==null?null:Find(parent,model);var symbol=model.GetDeclaredSymbol(local) as IMethodSymbol;return cfg!=null&&symbol!=null?cfg.GetLocalFunctionControlFlowGraph(symbol):null;}
        if(node is AnonymousFunctionExpressionSyntax)
        {
            var parent=node.Ancestors().FirstOrDefault(Atlas.IsCallable);var cfg=parent==null?null:Find(parent,model);if(cfg==null)return null;
            IEnumerable<IOperation> Walk(IOperation op){yield return op;foreach(var child in op.ChildOperations)foreach(var nested in Walk(child))yield return nested;}
            var callback=cfg.Blocks.SelectMany(b=>b.Operations.Concat(b.BranchValue==null?[]:[b.BranchValue])).SelectMany(Walk).OfType<IFlowAnonymousFunctionOperation>().FirstOrDefault(o=>o.Syntax.Span==node.Span);
            return callback==null?null:cfg.GetAnonymousFunctionControlFlowGraph(callback);
        }
        if(node is PropertyDeclarationSyntax property&&property.ExpressionBody!=null)
        {
            var operation=model.GetOperation(property.ExpressionBody.Expression);while(operation?.Parent!=null)operation=operation.Parent;
            return operation is IMethodBodyOperation body?ControlFlowGraph.Create(body):null;
        }
        return ControlFlowGraph.Create(node,model);
    }
    static GraphDoc FromCfg(ControlFlowGraph cfg)
    {
        var graph=new GraphDoc{Mode="Roslyn编译器控制流"};
        foreach(var block in cfg.Blocks)
        {
            var lines=block.Operations.Select(o=>Atlas.One(o.Syntax.ToString())).Where(t=>t.Length>0).Distinct().ToList();
            bool decision=block.ConditionKind!=ControlFlowConditionKind.None;string kind=decision?"decision":block.Kind==BasicBlockKind.Entry?"entry":block.Kind==BasicBlockKind.Exit?"exit":"step";
            if(block.BranchValue!=null)lines.Add((decision?"判断：":"结果：")+Atlas.One(block.BranchValue.Syntax.ToString()));
            string label=block.Kind==BasicBlockKind.Entry?"入口":block.Kind==BasicBlockKind.Exit?"结束 / 返回":lines.Count==0?"连接 / 离开区域":string.Join("\n",lines);
            // ★ 修复（防Bug回归）：定义async回调不等于外层方法在此挂起；不能跨入匿名/局部函数搜索await。
            bool Suspends(IOperation op)=>op is not IFlowAnonymousFunctionOperation&&op is not ILocalFunctionOperation&&(op.Kind==OperationKind.Await||op.Syntax is YieldStatementSyntax||op.ChildOperations.Any(Suspends));
            if(!decision&&(block.Operations.Any(Suspends)||block.BranchValue!=null&&Suspends(block.BranchValue)))kind="suspend";
            int line=(block.BranchValue?.Syntax??block.Operations.FirstOrDefault()?.Syntax)?.GetLocation().GetLineSpan().StartLinePosition.Line+1??0;
            graph.Nodes.Add(new(){Id="b"+block.Ordinal,Text=label,Kind=kind,Line=line,Reachable=block.IsReachable});
            Add(block,block.FallThroughSuccessor,false);Add(block,block.ConditionalSuccessor,true);
        }
        void Add(BasicBlock block,ControlFlowBranch? branch,bool conditional)
        {
            if(branch==null)return;string label="";
            if(block.ConditionKind!=ControlFlowConditionKind.None)label=(conditional==(block.ConditionKind==ControlFlowConditionKind.WhenTrue))?"是":"否";
            if(branch.Semantics is ControlFlowBranchSemantics.Return)label=(label.Length>0?label+" · ":"")+"返回";
            if(branch.FinallyRegions.Length>0)label+=(label.Length>0?" · ":"")+"经过finally "+string.Join(",",branch.FinallyRegions.Select(r=>r.FirstBlockOrdinal));
            string? to=branch.Destination==null?null:"b"+branch.Destination.Ordinal;
            if(to==null&&branch.Semantics is ControlFlowBranchSemantics.Throw or ControlFlowBranchSemantics.Rethrow or ControlFlowBranchSemantics.ProgramTermination)
            {
                to="x"+block.Ordinal;graph.Nodes.Add(new(){Id=to,Text=branch.Semantics==ControlFlowBranchSemantics.Rethrow?"重新抛出异常":branch.Semantics==ControlFlowBranchSemantics.Throw?"抛出异常 / 离开方法":"终止",Kind="exit"});label=branch.Semantics.ToString();
            }
            if(to!=null)graph.Edges.Add(new(){From="b"+block.Ordinal,To=to,Label=label});
        }
        void Regions(ControlFlowRegion region)
        {
            if(region.Kind is ControlFlowRegionKind.Catch or ControlFlowRegionKind.Filter or ControlFlowRegionKind.Finally)
            {
                graph.Notes.Add($"{region.Kind} 区域：b{region.FirstBlockOrdinal}～b{region.LastBlockOrdinal} {region.ExceptionType?.ToDisplayString()}");
                var container=region.EnclosingRegion;var protectedRegion=container?.NestedRegions.FirstOrDefault(r=>r.Kind==ControlFlowRegionKind.Try);
                if(protectedRegion!=null)graph.Edges.Add(new(){From="b"+protectedRegion.FirstBlockOrdinal,To="b"+region.FirstBlockOrdinal,Label=region.Kind==ControlFlowRegionKind.Finally?"退出保护区时 finally（示意）":"保护区可能异常（示意）",Kind="exception"});
            }
            foreach(var child in region.NestedRegions)Regions(child);
        }
        Regions(cfg.Root);return graph;
    }
}

sealed class SyntaxFlow
{
    readonly GraphDoc graph=new(){Mode="语法控制结构 / 声明（非CFG）"};int count;
    string Node(string text,string kind,SyntaxNode? source=null){string id="n"+count++;graph.Nodes.Add(new(){Id=id,Text=text,Kind=kind,Line=source?.GetLocation().GetLineSpan().StartLinePosition.Line+1??0});return id;}
    void Edge(string from,string to,string label="",string kind="flow")=>graph.Edges.Add(new(){From=from,To=to,Label=label,Kind=kind});
    public GraphDoc Build(SyntaxNode node)
    {
        string end=Node("结束 / 返回","exit");var block=node.ChildNodes().OfType<BlockSyntax>().FirstOrDefault();var arrow=node.ChildNodes().OfType<ArrowExpressionClauseSyntax>().FirstOrDefault();
        string first=block!=null?Statement(block,end,end,end):arrow!=null?Simple(arrow.Expression,end):node is LambdaExpressionSyntax lambda?lambda.Body is BlockSyntax b?Statement(b,end,end,end):Simple(lambda.Body,end):Node("声明 / 自动存储；没有显式方法体","step",node);
        if(block==null&&arrow==null&&node is not LambdaExpressionSyntax)Edge(first,end);string entry=Node("入口","entry",node);Edge(entry,first);
        graph.Nodes=graph.Nodes.OrderBy(n=>n.Kind=="entry"?0:n.Kind=="exit"?2:1).ToList();return graph;
    }
    string Simple(SyntaxNode statement,string next,string kind="step"){string id=Node(Atlas.One(statement.ToString()),kind,statement);Edge(id,next);return id;}
    string Statement(StatementSyntax statement,string next,string stop,string again)
    {
        switch(statement)
        {
            case BlockSyntax block:foreach(var child in block.Statements.Reverse())next=Statement(child,next,stop,again);return next;
            case IfStatementSyntax branch:
                string condition=Node("判断："+Atlas.One(branch.Condition.ToString()),"decision",branch.Condition);Edge(condition,Statement(branch.Statement,next,stop,again),"是");Edge(condition,branch.Else==null?next:Statement(branch.Else.Statement,next,stop,again),"否");return condition;
            case WhileStatementSyntax loop:
                string test=Node("循环判断："+Atlas.One(loop.Condition.ToString()),"decision",loop.Condition);Edge(test,Statement(loop.Statement,test,next,test),"是");Edge(test,next,"否");return test;
            case ForStatementSyntax loop:
                string check=Node("循环判断："+(loop.Condition==null?"true":Atlas.One(loop.Condition.ToString())),"decision",loop);string increment=loop.Incrementors.Count==0?check:Simple(loop.Incrementors.First(),check);Edge(check,Statement(loop.Statement,increment,next,increment),"是");Edge(check,next,"否");return loop.Declaration!=null?Simple(loop.Declaration,check):loop.Initializers.Count>0?Simple(loop.Initializers.First(),check):check;
            case CommonForEachStatementSyntax loop:
                string iterate=Node("遍历下一项："+Atlas.One(loop.Expression.ToString()),"decision",loop);Edge(iterate,Statement(loop.Statement,iterate,next,iterate),"有下一项");Edge(iterate,next,"遍历结束");return iterate;
            case DoStatementSyntax loop:
                string repeat=Node("循环判断："+Atlas.One(loop.Condition.ToString()),"decision",loop.Condition);string body=Statement(loop.Statement,repeat,next,repeat);Edge(repeat,body,"是");Edge(repeat,next,"否");return body;
            case SwitchStatementSyntax selection:
                string select=Node("分派："+Atlas.One(selection.Expression.ToString()),"decision",selection);foreach(var section in selection.Sections){string first=next;foreach(var child in section.Statements.Reverse())first=Statement(child,first,next,again);Edge(select,first,Atlas.One(string.Join(" / ",section.Labels)));}if(!selection.Sections.Any(s=>s.Labels.OfType<DefaultSwitchLabelSyntax>().Any()))Edge(select,next,"不匹配");return select;
            case TryStatementSyntax attempt:
                string after=attempt.Finally==null?next:Statement(attempt.Finally.Block,next,stop,again);string trial=Statement(attempt.Block,after,stop,again);foreach(var handler in attempt.Catches)Edge(trial,Statement(handler.Block,after,stop,again),"可能异常："+handler.Declaration,"exception");graph.Notes.Add("语法回退：异常/资源释放示意，精确传播请看源码。");return trial;
            case ReturnStatementSyntax:case ThrowStatementSyntax:return Simple(statement,graph.Nodes.First(n=>n.Kind=="exit").Id,statement is ThrowStatementSyntax?"exit":"step");
            case BreakStatementSyntax:return Simple(statement,stop);
            case ContinueStatementSyntax:return Simple(statement,again);
            case YieldStatementSyntax yield:return Simple(yield,yield.ReturnOrBreakKeyword.Text=="break"?graph.Nodes.First(n=>n.Kind=="exit").Id:next,"suspend");
            case UsingStatementSyntax use:return Simple(use.Declaration as SyntaxNode??use.Expression!,Statement(use.Statement,next,stop,again));
            case LockStatementSyntax locked:return Simple(locked.Expression,Statement(locked.Statement,next,stop,again));
            case CheckedStatementSyntax checkedBlock:return Statement(checkedBlock.Block,next,stop,again);
            case LocalFunctionStatementSyntax local:return Simple(local,next,"definition");
            default:return Simple(statement,next,statement.DescendantNodesAndSelf().Any(n=>n is AwaitExpressionSyntax)?"suspend":"step");
        }
    }
}
