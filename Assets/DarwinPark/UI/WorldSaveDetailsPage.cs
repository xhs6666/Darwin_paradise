using System;
using System.IO;
using System.Linq;
using System.Text;
using DarwinPark.Entities;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：存档详情只查看快照，不加载或运行世界；删除必须在详情页二次确认。
    public sealed class WorldSaveDetailsPage
    {
        public WorldSaveEntry Entry {get;private set;}
        public bool Confirming {get;private set;}
        private string _summary="",_error="";
        private string _latestBackup,_status="";
        public void Open(WorldSaveEntry entry)
        {
            Entry=entry;Confirming=false;_error="";
            // ★ 修复（防Bug回归）：打开详情时刷新快照；损坏档仍能查看路径、错误并删除，不能要求成功读档。
            try{entry.data=WorldSaveStore.Read(entry.path);entry.name=entry.data.name;entry.error=null;}catch(Exception error){entry.data=null;entry.error=error.Message;}
            _summary=Describe(entry);
            _status="";try{_latestBackup=WorldSaveBackups.Latest(entry.path,entry.data?.id);}catch(Exception error){_latestBackup=null;_error="备份目录无法读取："+error.Message;}
        }
        public void CancelConfirmation()=>Confirming=false;
        public void RequestDelete()=>Confirming=true;
        public string ConfirmDelete(string activeId=null)
        {
            if(!Confirming||Entry==null)throw new InvalidOperationException("尚未确认删除存档");
            string archived=WorldSaveManagement.Archive(Entry.path,activeId);Confirming=false;Entry=null;return archived;
        }
        public static string Describe(WorldSaveEntry entry)
        {
            var text=new StringBuilder();var data=entry.data;
            if(data==null)text.AppendLine("存档状态：无法读取\n原因："+entry.error);
            else
            {
                var entities=data.entities;
                text.AppendLine("存档状态：可读取");
                text.AppendLine("实体数量："+entities.Count);
                text.AppendLine("幼体 "+entities.Count(e=>e.age==AgeState.Infant)+"   成年 "+entities.Count(e=>e.age==AgeState.Adult)+"   老年 "+entities.Count(e=>e.age==AgeState.Elder));
                text.AppendLine("孕育中："+entities.Count(e=>e.gestating)+"   收藏："+entities.Count(e=>e.favorite));
                var time=TimeSpan.FromSeconds(data.gameTime);text.AppendLine("游戏时间："+(int)time.TotalHours+time.ToString(@"\:mm\:ss"));
                text.AppendLine("世界种子："+data.seed);text.AppendLine("世界标识："+data.id);
                text.AppendLine("创建时间："+LocalTime(data.createdUtc));text.AppendLine("保存时间："+LocalTime(data.savedUtc));
                text.AppendLine("保存版本："+data.version+"   游戏版本："+(string.IsNullOrEmpty(data.savedWithRelease)?"旧版":data.savedWithRelease));
            }
            try{text.AppendLine("存档文件："+(new FileInfo(entry.path).Length/1048576d).ToString("F2")+" MB");}catch(Exception){text.AppendLine("存档文件：无法读取大小");}
            text.Append("位置："+entry.path);return text.ToString();
        }
        private static string LocalTime(string text)=>DateTimeOffset.TryParse(text,out var time)?time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"):"未记录";
        public void Draw(string activeId,Action back,Action deleted,Action restored=null)
        {
            if(Entry==null){back();return;}
            GUILayout.Label(Entry.name,new GUIStyle(GUI.skin.label){fontSize=19,fontStyle=FontStyle.Bold,wordWrap=true,richText=false});
            GUILayout.Label(_summary,new GUIStyle(GUI.skin.label){wordWrap=true,richText=false});GUILayout.Space(12);
            bool active=Entry.data?.id==activeId&&!string.IsNullOrEmpty(activeId);
            if(Confirming)
            {
                GUILayout.Label("确认删除这个存档？它将从列表移除，并保留完整恢复副本。",new GUIStyle(GUI.skin.label){wordWrap=true});
                if(RuntimeMenuTheme.Button("确认删除",null,false,40,true))try{ConfirmDelete(activeId);deleted();return;}catch(Exception error){_error=error.Message;}
                if(RuntimeMenuTheme.Button("取消",null,false,38))CancelConfirmation();
            }
            else
            {
                if(active)GUILayout.Label("请先保存并退出该世界，再备份或删除存档。",RuntimeMenuTheme.Muted);
                bool enabled=GUI.enabled;GUI.enabled=enabled&&!active;
                if(RuntimeMenuTheme.Button("备份存档","save",false,36))try{_latestBackup=WorldSaveBackups.Create(Entry.path,activeId);_status="完整备份已创建："+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");}catch(Exception error){_error="备份失败："+error.Message;}
                if(_latestBackup!=null&&RuntimeMenuTheme.Button("从最新备份恢复为新存档",null,false,36))try{WorldSaveBackups.RestoreCopy(_latestBackup);restored?.Invoke();return;}catch(Exception error){_error="恢复失败："+error.Message;}
                if(RuntimeMenuTheme.Button("删除存档",null,false,40,true))RequestDelete();GUI.enabled=enabled;
                if(RuntimeMenuTheme.Button("返回存档列表","back",false,38))back();
            }
            if(!string.IsNullOrEmpty(_error))GUILayout.Label(_error,new GUIStyle(GUI.skin.label){wordWrap=true});
            if(!string.IsNullOrEmpty(_status))GUILayout.Label(_status,new GUIStyle(GUI.skin.label){wordWrap=true});
        }
    }
}
