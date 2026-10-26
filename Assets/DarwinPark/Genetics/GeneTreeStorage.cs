using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：编辑布局与运行关系一起保存；默认写入游戏目录 mods/Default，不覆盖内置资源。
    [Serializable] public class GeneTreeNode { public string id; public Vector2 position; public bool error; }
    [Serializable] public class GeneTreeEdge { public string from, to, kind="evolution"; public float value=100; }
    [Serializable] public class GeneTreeDocument
    {
        public int version=1;
        public List<GeneTreeNode> nodes=new List<GeneTreeNode>();
        public List<GeneTreeEdge> edges=new List<GeneTreeEdge>();
    }
    public static class GeneTreeStorage
    {
        public static string Folder => Path.Combine(DarwinPark.Core.GamePaths.Mods,"Default");
        public static string GenesFolder => Path.Combine(Folder,"Genes");
        public static GeneTreeDocument Active {get;private set;} = new GeneTreeDocument();
        public static Dictionary<string,string> ReadCatalog(string directory=null)
        {
            var result=new Dictionary<string,string>();
            if(string.IsNullOrWhiteSpace(directory))
            {
                foreach(var asset in Resources.LoadAll<TextAsset>("Genes")) Add(result,asset.text,asset.name);
                directory=GenesFolder;
            }
            if(Directory.Exists(directory)) foreach(var file in Directory.GetFiles(directory,"*.json",SearchOption.AllDirectories))
                Add(result,File.ReadAllText(file),Path.GetFileNameWithoutExtension(file));
            else if(directory!=GenesFolder) throw new IOException("文件夹不存在："+directory);
            return result;
        }
        private static void Add(Dictionary<string,string> map,string text,string name)
        {
            var def=JsonUtility.FromJson<GeneDefinitionJson>(text);
            if(def==null || def.id!=name || !System.Text.RegularExpressions.Regex.IsMatch(def.id??"","^[A-Za-z0-9_-]+$"))
                throw new InvalidDataException("文件名与基因 id 必须一致："+name);
            if(!Enum.TryParse<GeneCategory>(def.category,true,out _)) throw new InvalidDataException("无效 category："+name);
            if(def.attributeCurves==null||def.states==null||def.suppressionRules==null||def.attributeLinkages==null)throw new InvalidDataException("基因列表字段不能为 null："+name);
            foreach(var curve in def.attributeCurves)if(float.IsNaN(curve.minValue)||float.IsNaN(curve.maxValue)||float.IsInfinity(curve.minValue)||float.IsInfinity(curve.maxValue))throw new InvalidDataException("基因数值必须有限："+name);
            map[def.id]=text;
        }
        public static GeneTreeDocument Load() => Active=LoadFrom(Folder);
        public static GeneTreeDocument LoadFrom(string folder)
        {
            string file=Path.Combine(folder,"tree.json");
            if(!File.Exists(file)) return new GeneTreeDocument();
            return JsonUtility.FromJson<GeneTreeDocument>(File.ReadAllText(file)) ?? throw new InvalidDataException("基因树为空");
        }
        public static void Validate(GeneTreeDocument doc,Dictionary<string,string> catalog)
        {
            if(doc==null || doc.nodes==null || doc.edges==null) throw new InvalidDataException("无效基因树");
            var ids=new HashSet<string>();
            foreach(var n in doc.nodes) if(!catalog.ContainsKey(n.id)||!ids.Add(n.id)) throw new InvalidDataException("缺失或重复节点："+n.id);
            foreach(var e in doc.edges)
            {
                if(!ids.Contains(e.from)||!ids.Contains(e.to)||e.from==e.to) throw new InvalidDataException("连线端点无效");
                if(!new[]{"evolution","error","suppress","promote"}.Contains(e.kind)||float.IsNaN(e.value)||float.IsInfinity(e.value)||e.value<0) throw new InvalidDataException("关系类型或数值无效");
            }
            foreach(var id in ids) Visit(id,doc,new HashSet<string>(),new HashSet<string>());
        }
        private static void Visit(string id,GeneTreeDocument doc,HashSet<string> path,HashSet<string> done)
        {
            if(done.Contains(id))return;
            if(!path.Add(id))throw new InvalidDataException("进化关系不能成环");
            foreach(var e in doc.edges.Where(e=>e.from==id&&e.kind=="evolution"))Visit(e.to,doc,path,done);
            path.Remove(id);done.Add(id);
        }
        public static void Save(GeneTreeDocument doc,Dictionary<string,string> catalog)
        {
            SaveTo(Folder,doc,catalog); Active=doc;
        }
        public static void SaveTo(string folder,GeneTreeDocument doc,Dictionary<string,string> catalog)
        {
            Validate(doc,catalog);
            // 先校验整批配置，失败时不写盘；临时文件替换避免单文件写到一半。
            foreach(var item in catalog) Add(new Dictionary<string,string>(),item.Value,item.Key);
            string genes=Path.Combine(folder,"Genes");
            Directory.CreateDirectory(genes);
            foreach(var item in catalog) Write(Path.Combine(genes,item.Key+".json"),item.Value);
            Write(Path.Combine(folder,"tree.json"),JsonUtility.ToJson(doc,true));
        }
        private static void Write(string path,string text)
        { File.WriteAllText(path+".tmp",text,new System.Text.UTF8Encoding(false)); if(File.Exists(path))File.Delete(path);File.Move(path+".tmp",path); }
        public static IEnumerable<TextAsset> RuntimeAssets()
        {
            Dictionary<string,string> catalog;
            try { if(GeneRuntimeProfile.Catalog!=null){catalog=GeneRuntimeProfile.Catalog;Active=GeneRuntimeProfile.Tree;}else{catalog=ReadCatalog();Load();}Validate(Active,catalog); }
            catch(Exception ex){ Debug.LogError("[GeneTree] 自定义配置加载失败："+ex.Message);Active=new GeneTreeDocument();return Resources.LoadAll<TextAsset>("Genes"); }
            return catalog.Select(k=>new TextAsset(k.Value){name=k.Key}).ToArray();
        }
        public static void ApplyInfluences(GeneDefinition definition)
        {
            foreach(var edge in Active.edges.Where(e=>e.from==definition.Id&&(e.kind=="suppress"||e.kind=="promote")))
                definition.SuppressionRules.Add(new GeneSuppressionRule{TargetGeneId=edge.to,Coefficient=(edge.kind=="promote"?-1:1)*edge.value});
        }
        public static string Successor(string id,string kind)
        {
            var edges=Active.edges.Where(e=>e.from==id&&e.kind==kind&&e.value>0).ToArray();
            float sum=edges.Sum(e=>e.value); if(sum<=0)return null;
            float roll=UnityEngine.Random.value*sum;
            foreach(var edge in edges){roll-=edge.value;if(roll<=0)return edge.to;}return edges.Last().to;
        }
    }
}
