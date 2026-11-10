using System;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;
namespace DarwinPark.UI
{
    // ★ 新增：玩家返回游戏后按需采样，结束时一次写报告；空闲时不启动Recorder，不逐帧写盘或分配字符串。
    [DefaultExecutionOrder(1000)]public sealed class RuntimePerformanceCapture:MonoBehaviour
    {
        public static RuntimePerformanceCapture Instance{get;private set;}public float sampleSeconds=8;
        public string Status{get;private set;}="";public string ReportPath{get;private set;}
        private GameMainMenu _menu;private PerformanceSampleSession _session;private bool _armed;private float _began;
        private PerformanceCaptureNotice _notice;
        private void Awake(){Instance=this;_menu=GetComponent<GameMainMenu>();_notice=GetComponent<PerformanceCaptureNotice>()??gameObject.AddComponent<PerformanceCaptureNotice>();}
        public static void Ensure(GameMainMenu owner){if(owner.GetComponent<RuntimePerformanceCapture>()==null)owner.gameObject.AddComponent<RuntimePerformanceCapture>();}
        public void Request(){if(_session!=null||_armed)return;_armed=true;ReportPath=null;Status="已准备，返回游戏后开始采样";}
        private void LateUpdate()
        {
            if(_armed&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading&&(_menu==null||!_menu.Visible))
            {_armed=false;try{_session=new PerformanceSampleSession();_began=Time.unscaledTime;Status="正在采样，请正常操作游戏…";}catch(Exception ex){Status="采样无法启动："+ex.Message;_notice.Show(Status);}}
            if(_session==null)return;_session.RecordFrame(Time.unscaledDeltaTime*1000d);if(Time.unscaledTime-_began<sampleSeconds)return;
            try
            {
                var report=_session.Finish("entities="+EntityRegistry.All.Count+" resources="+ColonyResource.All.Count()+" resolution="+Screen.width+"x"+Screen.height+" graphics="+SystemInfo.graphicsDeviceType+" limit="+FramePacingSettings.Limit);
                string root=Path.Combine(string.IsNullOrEmpty(RuntimeFileLog.DirectoryPath)?Application.persistentDataPath:RuntimeFileLog.DirectoryPath,"performance");Directory.CreateDirectory(root);ReportPath=Path.Combine(root,"performance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
                foreach(var old in new DirectoryInfo(root).GetFiles("performance-*.json").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(5))old.Delete();
                Status="采样完成："+ReportPath;_notice.Show("游戏性能采样完成\n报告已保存：\n"+ReportPath);Debug.Log("[性能采样] "+Status);
            }
            catch(Exception ex){Status="性能报告保存失败："+ex.Message;_notice.Show(Status);Debug.LogWarning(Status);}
            finally{_session.Dispose();_session=null;}
        }
        private void OnDisable(){_armed=false;_session?.Dispose();_session=null;}
        private void OnDestroy(){if(Instance==this)Instance=null;}
        public static void Draw()
        {
            var capture=Instance;if(capture==null)return;bool enabled=GUI.enabled;GUI.enabled=enabled&&WorldRuntimeGate.Ready&&!capture._armed&&capture._session==null;
            if(RuntimeMenuTheme.Button("采样游戏性能",null,false,36))capture.Request();GUI.enabled=enabled;
            if(!string.IsNullOrEmpty(capture.Status))GUILayout.Label(capture.Status,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
        }
    }
}
