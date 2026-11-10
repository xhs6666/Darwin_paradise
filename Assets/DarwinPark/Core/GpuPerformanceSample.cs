using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace DarwinPark.Core
{
    // ★ 新增：仅在用户主动采样时读取真实GPU帧时间；不支持或没有数据时明确缺省，不拿0当作GPU无开销。
    public sealed class GpuPerformanceSample
    {
        private readonly FrameTiming[] _latest=new FrameTiming[1];private readonly double[] _values;private int _count;private ulong _last;
        public bool Enabled {get;}=FrameTimingManager.IsFeatureEnabled();
        public GpuPerformanceSample(int capacity){_values=new double[capacity];}
        public void Record()
        {
            if(!Enabled||_count>=_values.Length)return;FrameTimingManager.CaptureFrameTimings();if(FrameTimingManager.GetLatestTimings(1,_latest)==0)return;var frame=_latest[0];
            if(frame.frameStartTimestamp!=0&&frame.frameStartTimestamp==_last)return;_last=frame.frameStartTimestamp;double time=frame.gpuFrameTime;if(!double.IsFinite(time)||time<=0)return;_values[_count++]=time;
        }
        public PerformanceMetric Finish()
        {
            var metric=new PerformanceMetric{name="GPU Frame Time",unit="ms",source=Enabled?"FrameTimingManager":"FrameTimingManager disabled/unsupported",available=_count>0,samples=_count};if(_count==0)return metric;
            Array.Sort(_values,0,_count);double sum=0;for(int i=0;i<_count;i++)sum+=_values[i];metric.mean=sum/_count;metric.p95=_values[Math.Min(_count-1,(int)(_count*.95))];metric.maximum=_values[_count-1];return metric;
        }
    }
    // ★ 新增：报告记录最慢帧的镜头，区分控制/拉近、裁剪面与模拟倍率，不要求用户凭记忆描述现场。
    [Serializable]public sealed class PerformanceViewState
    {
        public bool valid,entityView;public Vector3 position;public Quaternion rotation;public float nearClip,farClip,fieldOfView,renderScale=1,configuredSpeed,deliveredSpeed;public int width,height;public double pendingGameTime;public float deliveredGameDelta;
        public static PerformanceViewState Capture()
        {
            var camera=Camera.main;var value=new PerformanceViewState();if(camera==null)return value;value.valid=true;value.entityView=camera.GetComponent<OrbitCamera>()?.EntityViewActive==true;value.position=camera.transform.position;value.rotation=camera.transform.rotation;value.nearClip=camera.nearClipPlane;value.farClip=camera.farClipPlane;value.fieldOfView=camera.fieldOfView;value.width=camera.pixelWidth;value.height=camera.pixelHeight;
            var pipeline=GraphicsSettings.currentRenderPipeline;var scale=pipeline?.GetType().GetProperty("renderScale");if(scale?.GetValue(pipeline) is float renderScale)value.renderScale=renderScale;
            if(ServiceLocator.TryGet<SimulationClock>(out var clock)){value.configuredSpeed=clock.ConfiguredTimeScale;value.deliveredSpeed=clock.MotionTimeScale;value.pendingGameTime=clock.PendingGameTime;value.deliveredGameDelta=clock.GameDeltaTime;}return value;
        }
    }
}
