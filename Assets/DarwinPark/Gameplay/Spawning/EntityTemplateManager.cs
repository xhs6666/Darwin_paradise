namespace DarwinPark.UI
{
using DarwinPark.Entities;
using DarwinPark.Genetics;
using System.Collections.Generic;
using UnityEngine;


    [System.Serializable]
    public class EntityTemplateData
    {
        public string templateName;
        public string iconId;
        public string bodyPlanId="organism_basic";
        public List<PartOverride> bodyOverrides=new();
        public Genome ToGenome(){var genome=new Genome{BodyPlanId=bodyPlanId};foreach(var part in bodyOverrides)genome.BodyOverrides.Add(new PartOverride{slotId=part.slotId,scale=part.scale,position=part.position,rotation=part.rotation});foreach(var gene in genes){genome.Add(gene.CreateInstance());}return genome;}
        public List<GeneInstanceData> genes = new List<GeneInstanceData>();
    }

    public static class EntityTemplateManager
    {
        // ★ 修复（防Bug回归）：进程/播放会话重启后首次访问从磁盘读取，不能只保留静态内存列表。
        private static List<EntityTemplateData> _templates = new List<EntityTemplateData>();
        private static bool _loaded;
        public static string WorldId {get;private set;}
        // ★ 修复（防Bug回归）：模板归属当前世界；换档必须清空缓存，不能沿用上一个世界的模板列表。
        private static void EnsureLoaded(){if(_loaded)return;_templates=WorldId==null?new List<EntityTemplateData>():EntityTemplateStore.Load();_loaded=true;}
        public static void UseWorld(string id)
        {
            if(id!=null&&!System.Guid.TryParseExact(id,"N",out _))throw new System.ArgumentException("模板所属世界标识无效");
            if(WorldId==id)return;WorldId=id;_loaded=false;_templates=new List<EntityTemplateData>();NotifyChanged();
        }
        private static void NotifyChanged(){if(OnTemplatesChanged!=null)foreach(System.Action listener in OnTemplatesChanged.GetInvocationList())try{listener();}catch(System.Exception error){Debug.LogException(error);}}
        public static IReadOnlyList<EntityTemplateData> Templates {get{EnsureLoaded();return _templates;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){_templates=new List<EntityTemplateData>();_loaded=false;WorldId=null;OnTemplatesChanged=null;}

        // 保存事件，通知 UI 刷新
        public static event System.Action OnTemplatesChanged;

        // ★ 新增：先原子写盘再通知槽位刷新；仅删除模板资料，不删除已生成实体。
        public static bool DeleteTemplate(string name)
        {
            if(!DarwinPark.Core.WorldAuthority.CanEdit||WorldId==null)return false;
            EnsureLoaded();int index=_templates.FindIndex(t=>t.templateName==name);if(index<0)return false;
            var next=new List<EntityTemplateData>(_templates);next.RemoveAt(index);EntityTemplateStore.Save(next);_templates=next;
            // 写盘已成功，单个视图异常不能阻断其他视图，也不能把成功删除误报为失败。
            NotifyChanged();
            return true;
        }
        public static void SaveTemplate(DarwinEntity entity)
        {
            if(!DarwinPark.Core.WorldAuthority.CanEdit||WorldId==null)return;
            EnsureLoaded();
            if(entity==null)return;
            var genes = entity.GetComponent<EntityGeneController>();
            if (genes == null || genes.Genome == null) return;

            var data = new EntityTemplateData
            {
                templateName = entity.DisplayName,
                bodyPlanId=genes.Genome.BodyPlanId,bodyOverrides=genes.Genome.BodyOverrides.ConvertAll(p=>new PartOverride{slotId=p.slotId,scale=p.scale,position=p.position,rotation=p.rotation}),
                iconId = "entity_template_default", // 默认图标ID，你可以根据基因判断赋予不同ID
                genes = new List<GeneInstanceData>()
            };

            foreach (var g in genes.Genome.Genes)
            {
                data.genes.Add(new GeneInstanceData
                {
                    definitionId = g.DefinitionId,stateId=g.StateId,enabled=g.Enabled,copyStrength=g.CopyStrength,missingStateIds=new List<string>(g.MissingStateIds),
                    value = g.Value,
                    tier = g.Tier
                });
            }

            // ★ 修复（防Bug回归）：实际写盘成功后才更新列表/通知界面，失败不误报“保存成功”或丢掉原模板。
            var next=new List<EntityTemplateData>(_templates);
            int index=next.FindIndex(t=>t.templateName==data.templateName);
            if(index>=0)next[index]=data;else next.Add(data);
            try{EntityTemplateStore.Save(next);}
            catch(System.Exception ex) when(ex is System.IO.IOException||ex is System.UnauthorizedAccessException||ex is System.ArgumentException)
            {Debug.LogError("[Template] 模板保存失败，原模板保留："+ex.Message);return;}
            _templates=next;
            Debug.Log($"[Template] 已保存模板：{data.templateName}");

            NotifyChanged();
        }
    }
}

namespace DarwinPark.UI
{
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;


    // ★ 修复（防Bug回归）：模板与世界目录共存，升级游戏不改变其路径；同目录原子替换和备份避免中断丢失。
    public static class EntityTemplateStore
    {
        [Serializable] private sealed class Document
        {
            public int version;
            public List<EntityTemplateData> templates;
        }
        public static string ForWorld(string id){if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("模板所属世界无效");return Path.Combine(GamePaths.Saves,id,"templates","entities.json");}
        public static string FilePath=>ForWorld(EntityTemplateManager.WorldId);
        private static void Validate(Document document)
        {
            if(document==null||document.version!=2||document.templates==null)throw new InvalidDataException("模板格式或版本无效");
            var names=new HashSet<string>();
            foreach(var template in document.templates)
            {
                if(template==null||string.IsNullOrWhiteSpace(template.templateName)||!names.Add(template.templateName)||template.genes==null)throw new InvalidDataException("模板名称重复或数据缺失");
                // ★ 新增：模板部位参数和世界实体使用相同校验，导入不能绕过形状、位置、旋转边界。
                MorphologyRegistry.ValidateGenome(new Genome{BodyPlanId=template.bodyPlanId,BodyOverrides=template.bodyOverrides});
                foreach(var gene in template.genes)
                    if(gene==null||string.IsNullOrWhiteSpace(gene.definitionId)||float.IsNaN(gene.value)||float.IsInfinity(gene.value)||GeneDatabase.Get(gene.definitionId)==null||GeneDatabase.Get(gene.definitionId).ExplicitState&&!GeneDatabase.Get(gene.definitionId).States.Exists(state=>state.StateId==gene.stateId))throw new InvalidDataException("模板基因无效："+template.templateName);
            }
        }
        private static List<EntityTemplateData> Read(string path)
        {
            var document=JsonUtility.FromJson<Document>(File.ReadAllText(path,Encoding.UTF8));Validate(document);return document.templates;
        }
        public static List<EntityTemplateData> Load(string path=null)
        {
            if(path==null&&EntityTemplateManager.WorldId==null)return new List<EntityTemplateData>();
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
            var document=new Document{version=2,templates=templates};Validate(document);path=path??FilePath;
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
