using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DarwinPark.Core;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 修复（防Bug回归）：模板属于跨世界资料，必须独立落盘；同目录原子替换和备份避免退出/写入中断丢失已有模板。
    public static class EntityTemplateStore
    {
        [Serializable] private sealed class Document
        {
            public int version;
            public List<EntityTemplateData> templates;
        }
        public static string FilePath=>Path.Combine(GamePaths.Root,"templates","entities.json");
        private static void Validate(Document document)
        {
            if(document==null||document.version!=1||document.templates==null)throw new InvalidDataException("模板格式或版本无效");
            var names=new HashSet<string>();
            foreach(var template in document.templates)
            {
                if(template==null||string.IsNullOrWhiteSpace(template.templateName)||!names.Add(template.templateName)||template.genes==null)throw new InvalidDataException("模板名称重复或数据缺失");
                foreach(var gene in template.genes)
                    if(gene==null||string.IsNullOrWhiteSpace(gene.definitionId)||float.IsNaN(gene.value)||float.IsInfinity(gene.value))throw new InvalidDataException("模板基因无效："+template.templateName);
            }
        }
        private static List<EntityTemplateData> Read(string path)
        {
            var document=JsonUtility.FromJson<Document>(File.ReadAllText(path,Encoding.UTF8));Validate(document);return document.templates;
        }
        public static List<EntityTemplateData> Load(string path=null)
        {
            path=path??FilePath;
            if(File.Exists(path))
            {
                try{return Read(path);}
                catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException)
                {Debug.LogWarning("[Template] 模板读取失败，尝试备份："+path+"；"+ex.Message);}
            }
            if(File.Exists(path+".bak"))
            {
                try{return Read(path+".bak");}
                catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException)
                {Debug.LogWarning("[Template] 备份读取失败，原文件已保留："+ex.Message);}
            }
            return new List<EntityTemplateData>();
        }
        public static void Save(List<EntityTemplateData> templates,string path=null)
        {
            var document=new Document{version=1,templates=templates};Validate(document);path=path??FilePath;
            // ★ 修复（防Bug回归）：主文件损坏而从备份恢复时，保存不能用损坏主文件覆盖最后一份有效备份。
            bool validPrimary=false;
            if(File.Exists(path))try{Read(path);validPrimary=true;}catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException){Debug.LogWarning("[Template] 覆盖损坏主文件，保留备份："+ex.Message);}
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temporary=path+".tmp";
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false))){writer.Write(JsonUtility.ToJson(document,true));writer.Flush();stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,validPrimary?path+".bak":null);else File.Move(temporary,path);
        }
    }
}
