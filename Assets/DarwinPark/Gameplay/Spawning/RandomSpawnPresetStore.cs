using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：基因条件独立为可外部编辑的JSON文件，跨世界复用；临时文件替换避免保存中断损坏旧条件。
    public static class RandomSpawnPresetStore
    {
        [Serializable] public sealed class Preset
        { public int version=1;public string id;public RandomSpawnRecipe source; }
        public static string DirectoryPath=>Path.Combine(GamePaths.Root,"templates","spawn-conditions");
        public static Preset Save(RandomSpawnRecipe source,string id=null,string directory=null)
        {
            var preset=new Preset{id=id??Guid.NewGuid().ToString("N"),source=JsonUtility.FromJson<RandomSpawnRecipe>(JsonUtility.ToJson(source))};
            Validate(preset);string folder=directory??DirectoryPath;Directory.CreateDirectory(folder);string path=Path.Combine(folder,preset.id+".json"),temporary=path+".tmp";
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false))){writer.Write(JsonUtility.ToJson(preset,true));writer.Flush();stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);return preset;
        }
        public static List<Preset> Load(string directory=null)
        {
            var result=new List<Preset>();string folder=directory??DirectoryPath;if(!Directory.Exists(folder))return result;
            foreach(var file in Directory.GetFiles(folder,"*.json").OrderBy(p=>p,StringComparer.Ordinal))
            {
                try
                {
                    var preset=JsonUtility.FromJson<Preset>(File.ReadAllText(file,Encoding.UTF8));Validate(preset);
                    if(Path.GetFileNameWithoutExtension(file)!=preset.id||result.Any(p=>p.id==preset.id))throw new InvalidDataException("条件文件名与编号不一致或重复");result.Add(preset);
                }
                catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException)
                {Debug.LogWarning("[生成条件] 无法加载，已保留原文件："+file+"；"+ex.Message);}
            }
            return result.OrderBy(p=>p.source.name,StringComparer.Ordinal).ToList();
        }
        private static void Validate(Preset preset)
        {
            if(preset==null||preset.version!=1||!Guid.TryParseExact(preset.id,"N",out _)||preset.source==null||preset.source.method!=RandomSpawnMethod.Genes)throw new InvalidDataException("基因条件JSON格式或版本无效");
            preset.source.Validate(GeneDatabase.All.Select(g=>g.Id).ToArray());
        }
    }
}
