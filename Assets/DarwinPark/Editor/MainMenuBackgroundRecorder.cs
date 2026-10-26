using System;
using System.Collections;
using System.IO;
using DarwinPark.Core;
using DarwinPark.Planet;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Object;
namespace DarwinPark.EditorTools
{
    // ★ 新增：离线逐帧录制实际游戏场景，固定相位保证地球自转/太阳公转可以无缝循环。
    public sealed class MainMenuBackgroundRecorder
    {
        private static MainMenuBackgroundRecorder _active;
        public int width=1280,height=720,fps=24,seconds=12;
        public float cameraDistance=1900;
        private Camera _camera;private OrbitCamera _orbit;private bool _orbitEnabled,_hud,_worldHud,_blocked;
        private Vector3 _position;private Quaternion _rotation,_planetRotation;private Transform _planet;private SunSystem _sun;
        private float _sunAngle;private RenderTexture _target;private Texture2D _frame;private System.Diagnostics.Process _encoder;
        [MenuItem("Tools/Darwin Park/Record Menu Background %&F7")]
        public static void Record()
        {
            if(!Application.isPlaying||FindFirstObjectByType<PlanetTerrainMeshBuilder>()?.Data==null){Debug.LogError("请先加载一个世界");return;}
            if(_active!=null)return;
            _active=new MainMenuBackgroundRecorder();
            FindFirstObjectByType<DarwinPark.UI.WorldSessionManager>().StartCoroutine(RecordFrames());
        }
        private static IEnumerator RecordFrames()
        {
            var recorder=_active;
            var operation=recorder.Capture();
            while(true)
            {
                bool next;object current=null;
                try{next=operation.MoveNext();if(next)current=operation.Current;}
                catch(Exception ex){Debug.LogException(ex);recorder.Cleanup();_active=null;yield break;}
                if(!next)break;yield return current;
            }
            recorder.Cleanup();_active=null;Debug.Log("[MenuRecording] PASS: 预录背景完成");
        }
        private IEnumerator Capture()
        {
            _camera=Camera.main;_orbit=_camera.GetComponent<OrbitCamera>();_orbitEnabled=_orbit.enabled;_orbit.enabled=false;
            _position=_camera.transform.position;_rotation=_camera.transform.rotation;
            _planet=FindFirstObjectByType<PlanetTerrainMeshBuilder>().transform;_planetRotation=_planet.rotation;
            _sun=FindFirstObjectByType<SunSystem>();_sunAngle=_sun.OrbitAngle;
            var root=ServiceLocator.Get<UIRoot>();_hud=root.MainCanvas.enabled;_worldHud=root.WorldCanvas.enabled;root.MainCanvas.enabled=false;root.WorldCanvas.enabled=false;
            _blocked=GameInputGate.Blocked;GameInputGate.Blocked=true;WorldRuntimeGate.Recording=true;ServiceLocator.Get<SimulationClock>().Pause();
            _camera.transform.position=_planet.position+new Vector3(0,.72f,-1).normalized*cameraDistance;_camera.transform.LookAt(_planet.position);
            _target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);_target.Create();
            _frame=new Texture2D(width,height,TextureFormat.RGB24,false);
            string folder=Path.Combine(Application.streamingAssetsPath,"Menu");Directory.CreateDirectory(folder);
            // 编码器从 PATH 查找，不把本机工具路径写进项目。
            _encoder=new System.Diagnostics.Process{StartInfo=new System.Diagnostics.ProcessStartInfo("ffmpeg","-y -hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size "+width+"x"+height+" -framerate "+fps+" -i - -an -vf vflip -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 -movflags +faststart \""+Path.Combine(folder,"earth-orbit.mp4")+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true}};
            _encoder.Start();
            for(int i=0;i<fps*seconds;i++)
            {
                float phase=(float)i/(fps*seconds);_planet.rotation=Quaternion.AngleAxis(360*phase,Vector3.up)*_planetRotation;_sun.OrbitAngle=45+360*phase;
                yield return new WaitForEndOfFrame();
                var previousTarget=_camera.targetTexture;var previousActive=RenderTexture.active;
                try
                {
                    _camera.targetTexture=_target;_camera.Render();RenderTexture.active=_target;
                    _frame.ReadPixels(new Rect(0,0,width,height),0,0);_frame.Apply();
                    var bytes=_frame.GetRawTextureData<byte>().ToArray();_encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);
                    if(i==0)File.WriteAllBytes(Path.Combine(folder,"earth-orbit.jpg"),_frame.EncodeToJPG(90));
                }
                finally{_camera.targetTexture=previousTarget;RenderTexture.active=previousActive;}
                if(i%fps==0)File.WriteAllText("Artifacts/WorldFlowReview/recording.txt",i+"/"+(fps*seconds));
            }
            _encoder.StandardInput.Close();while(!_encoder.HasExited)yield return null;
            if(_encoder.ExitCode!=0)throw new InvalidOperationException("背景视频编码失败");
            File.WriteAllText("Artifacts/WorldFlowReview/recording.txt","PASS: "+width+"x"+height+", "+fps+"fps, "+seconds+"s");AssetDatabase.Refresh();
        }
        private void Cleanup()
        {
            if(_encoder!=null){if(!_encoder.HasExited)_encoder.StandardInput.Close();_encoder.Dispose();_encoder=null;}
            if(_camera!=null)_camera.transform.SetPositionAndRotation(_position,_rotation);if(_orbit!=null)_orbit.enabled=_orbitEnabled;
            if(_planet!=null)_planet.rotation=_planetRotation;if(_sun!=null)_sun.OrbitAngle=_sunAngle;
            if(ServiceLocator.TryGet<UIRoot>(out var root)){root.MainCanvas.enabled=_hud;root.WorldCanvas.enabled=_worldHud;}
            WorldRuntimeGate.Recording=false;GameInputGate.Blocked=_blocked;
            if(_target!=null){_target.Release();Destroy(_target);}if(_frame!=null)Destroy(_frame);
        }

    }
}
