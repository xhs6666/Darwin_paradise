using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DarwinPark.Genetics;
using DarwinPark.UI;
using DarwinPark.Core;
using UnityEngine;
using UnityEditor;

namespace DarwinPark.EditorTools
{
    // ★ 新增：读写往返、进化环拒绝、运行抑制/促进映射，以及真实第一人称攻击的综合验收。
    public static class GeneTreeWorkbenchRegression
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Darwin Park/Validate First Person and Gene Tree")]
        public static void Validate()
        {
            Check(Application.isPlaying,"请先播放并等待星球生成");
            var menu=UnityEngine.Object.FindFirstObjectByType<GameMainMenu>();
            typeof(GameMainMenu).GetMethod("Resume",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,null);
            Check(!GameInputGate.Blocked,"单人游戏未释放输入");
            var catalog=GeneTreeStorage.ReadCatalog();var ids=catalog.Keys.OrderBy(x=>x).Take(3).ToArray();Check(ids.Length==3,"测试需要三个基因");
            var form=new GeneValueForm();form.Load(catalog[ids[0]]);var formExport=JsonUtility.FromJson<GeneDefinitionJson>(form.Export());
            Check(formExport.id==ids[0]&&formExport.attributeCurves.Count==JsonUtility.FromJson<GeneDefinitionJson>(catalog[ids[0]]).attributeCurves.Count,"数值表单丢失基因属性");
            var doc=new GeneTreeDocument();for(int i=0;i<3;i++)doc.nodes.Add(new GeneTreeNode{id=ids[i],position=new Vector2(i*200,100),error=i==2});
            doc.edges.Add(new GeneTreeEdge{from=ids[0],to=ids[1],kind="evolution",value=10});
            doc.edges.Add(new GeneTreeEdge{from=ids[0],to=ids[2],kind="error",value=20});
            doc.edges.Add(new GeneTreeEdge{from=ids[1],to=ids[2],kind="suppress",value=300});
            doc.edges.Add(new GeneTreeEdge{from=ids[2],to=ids[1],kind="promote",value=100});
            string folder=Path.GetFullPath("Artifacts/GeneTreeReview/RoundTrip");
            GeneTreeStorage.SaveTo(folder,doc,catalog);var loaded=GeneTreeStorage.LoadFrom(folder);
            Check(JsonUtility.ToJson(doc)==JsonUtility.ToJson(loaded),"基因树读写不一致");
            Check(GeneTreeStorage.ReadCatalog(Path.Combine(folder,"Genes")).Count==catalog.Count,"基因 JSON 未完整保存");
            doc.edges.Add(new GeneTreeEdge{from=ids[1],to=ids[0],kind="evolution"});bool rejected=false;
            try{GeneTreeStorage.Validate(doc,catalog);}catch(InvalidDataException){rejected=true;}Check(rejected,"未拒绝进化环");doc.edges.RemoveAt(doc.edges.Count-1);
            var active=GeneTreeStorage.Active;var old=JsonUtility.ToJson(active);
            try
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(doc),active);
                var def=new GeneDefinition{Id=ids[1]};GeneTreeStorage.ApplyInfluences(def);Check(def.SuppressionRules.Single().Coefficient==300,"红线抑制映射失败");
                def=new GeneDefinition{Id=ids[2]};GeneTreeStorage.ApplyInfluences(def);Check(def.SuppressionRules.Single().Coefficient==-100,"绿线促进映射失败");
                Check(GeneTreeStorage.Successor(ids[0],"evolution")==ids[1]&&GeneTreeStorage.Successor(ids[0],"error")==ids[2],"进化/出错目标未读取");
            }
            finally{JsonUtility.FromJsonOverwrite(old,active);}
            LifeCycleCombatRegression.Validate();
            File.WriteAllText("Artifacts/GeneTreeReview/runtime-checks.txt","PASS: graph and JSON folder round trip; all four edge types retained; cycle rejected; red/green coefficients mapped; evolution/error runtime target lookup; first-person lifecycle/combat regression completed.");
            Debug.Log("[GeneTreeReview] PASS: first-person combat, persistence and runtime relations");
        }
    }
}
