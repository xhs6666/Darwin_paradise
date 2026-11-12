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
        // ★ 新增：报告记录真实缓存命中和批次数，不能仅由总耗时猜测优化是否生效。
        private long _foodHits,_foodSearches,_groundDirect,_groundReused,_resourceRebuilds,_instanceWrites;
        private PerformanceCaptureNotice _notice;private PerformanceReport _report;private bool _details;
        public PerformanceReport LastReport=>_report;
        private void Awake(){Instance=this;_menu=GetComponent<GameMainMenu>();_notice=GetComponent<PerformanceCaptureNotice>()??gameObject.AddComponent<PerformanceCaptureNotice>();}
        public static void Ensure(GameMainMenu owner){if(owner.GetComponent<RuntimePerformanceCapture>()==null)owner.gameObject.AddComponent<RuntimePerformanceCapture>();}
        public void Request(){if(_session!=null||_armed)return;_armed=true;ReportPath=null;Status="已准备，返回游戏后开始采样";}
        private void LateUpdate()
        {
            if(_armed&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading&&(_menu==null||!_menu.Visible))
            {_armed=false;try{_session=new PerformanceSampleSession();_foodHits=ColonyFoodQueryCache.Hits;_foodSearches=ColonyFoodQueryCache.Searches;_groundDirect=EntityGroundProbe.DirectSamples;_groundReused=EntityGroundProbe.CrossStepReused;_resourceRebuilds=ResourceCoordinates.Rebuilds;_instanceWrites=CorpseInstanceRenderer.InstanceWrites;_began=Time.unscaledTime;Status="正在采样，请正常操作游戏…";}catch(Exception ex){Status="采样无法启动："+ex.Message;_notice.Show(Status);}}
            if(_session==null)return;_session.RecordFrame(Time.unscaledDeltaTime*1000d);if(Time.unscaledTime-_began<sampleSeconds)return;
            try
            {
                var report=_session.Finish("entities="+EntityRegistry.All.Count+" resources="+ColonyResource.All.Count()+" resolution="+Screen.width+"x"+Screen.height+" graphics="+SystemInfo.graphicsDeviceType+" limit="+FramePacingSettings.Limit);
                report.context+=" foodCacheHits="+(ColonyFoodQueryCache.Hits-_foodHits)+" foodSearches="+(ColonyFoodQueryCache.Searches-_foodSearches)+" groundDirect="+(EntityGroundProbe.DirectSamples-_groundDirect)+" groundCrossReuse="+(EntityGroundProbe.CrossStepReused-_groundReused)+" resourceIndexRebuilds="+(ResourceCoordinates.Rebuilds-_resourceRebuilds)+" corpseSlotWrites="+(CorpseInstanceRenderer.InstanceWrites-_instanceWrites)+" corpseInstances="+CorpseInstanceRenderer.LastInstances+" corpseBatchesPreviousFrame="+CorpseInstanceRenderer.PreviousFrameBatches;
                string root=Path.Combine(string.IsNullOrEmpty(RuntimeFileLog.DirectoryPath)?Application.persistentDataPath:RuntimeFileLog.DirectoryPath,"performance");Directory.CreateDirectory(root);ReportPath=Path.Combine(root,"performance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));_report=report;File.WriteAllText(Path.ChangeExtension(ReportPath,".md"),RuntimePerformanceDetails.Markdown(report),new System.Text.UTF8Encoding(false));
                foreach(var old in new DirectoryInfo(root).GetFiles("performance-*.json").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(5)){string readable=Path.ChangeExtension(old.FullName,".md");old.Delete();if(File.Exists(readable))File.Delete(readable);}
                Status="采样完成："+ReportPath;_notice.Show("游戏性能采样完成\n"+RuntimePerformanceDetails.Summary(report)+"\n设置 → 日志与诊断可查看完整排行\n"+ReportPath);Debug.Log("[性能采样] "+Status);
            }
            catch(Exception ex){Status="性能报告保存失败："+ex.Message;_notice.Show(Status);Debug.LogWarning(Status);}
            finally{_session.Dispose();_session=null;}
        }
        // ★ 新增：重启后按需载入最近报告，不在每帧扫描日志目录；旧版报告明确缺少脚本明细。
        public bool LoadLatest()
        {
            try{string folder=Path.Combine(string.IsNullOrEmpty(RuntimeFileLog.DirectoryPath)?Application.persistentDataPath:RuntimeFileLog.DirectoryPath,"performance");var file=Directory.Exists(folder)?new DirectoryInfo(folder).GetFiles("performance-*.json").OrderByDescending(f=>f.LastWriteTimeUtc).FirstOrDefault():null;if(file==null){Status="暂无性能采样报告";return false;}
                var report=JsonUtility.FromJson<PerformanceReport>(File.ReadAllText(file.FullName));if(report==null||report.metrics==null)throw new InvalidDataException("报告格式不正确");report.scripts??=new();report.hotspots??=new();if(string.IsNullOrEmpty(report.coverageNote))report.coverageNote="旧版报告没有脚本/方法明细，请重新采样。";_report=report;ReportPath=file.FullName;_details=true;Status="已载入："+ReportPath;return true;}
            catch(Exception ex){Status="报告读取失败："+ex.Message;return false;}
        }
        private void OnDisable(){_armed=false;_session?.Dispose();_session=null;}
        private void OnDestroy(){if(Instance==this)Instance=null;}
        public static void Draw()
        {
            var capture=Instance;if(capture==null)return;bool enabled=GUI.enabled;GUI.enabled=enabled&&WorldRuntimeGate.Ready&&!capture._armed&&capture._session==null;
            if(RuntimeMenuTheme.Button("采样游戏性能",null,false,36))capture.Request();GUI.enabled=enabled;
            if(RuntimeMenuTheme.Button("载入最近性能报告",null,false,30))capture.LoadLatest();
            if(capture._report!=null&&RuntimeMenuTheme.Button(capture._details?"收起脚本与算法排行":"查看脚本与算法排行",null,false,34))capture._details=!capture._details;
            if(capture._details&&capture._report!=null)RuntimePerformanceDetails.Draw(capture._report);
            if(!string.IsNullOrEmpty(capture.Status))GUILayout.Label(capture.Status,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
        }
    }
}
