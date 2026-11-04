using System.IO;
using DarwinPark.Core;
using UnityEngine;
using UnityEngine.Video;
namespace DarwinPark.UI
{
    // ★ 新增：播放提前录制的游戏内星球，菜单不依赖实时世界生成，首帧由同源封面兜底。
    public sealed class MainMenuBackground : MonoBehaviour
    {
        // ★ 修复（防Bug回归）：VP8软件解码避开Windows Media Foundation硬件解码的Flush/Unreadable Track故障。
        public string videoFile="Menu/earth-orbit.webm",posterFile="Menu/earth-orbit.jpg";
        public int videoWidth=1280,videoHeight=720;
        private VideoPlayer _player;
        private RenderTexture _target;
        private Texture2D _poster;
        private GameMainMenu _menu;
        private bool _failed,_playRequested;
        private void Start()
        {
            _menu=GetComponent<GameMainMenu>();
            string poster=Path.Combine(Application.streamingAssetsPath,posterFile);
            if(File.Exists(poster)){_poster=new Texture2D(2,2);_poster.LoadImage(File.ReadAllBytes(poster));}
            string video=Path.Combine(Application.streamingAssetsPath,videoFile);
            if(!File.Exists(video)){Debug.LogWarning("[主界面背景] 缺少预录视频："+video);_failed=true;return;}
            _target=new RenderTexture(videoWidth,videoHeight,0);_target.Create();
            _player=gameObject.AddComponent<VideoPlayer>();_player.playOnAwake=false;
            _player.source=VideoSource.Url;_player.url=video;_player.isLooping=true;
            _player.renderMode=VideoRenderMode.RenderTexture;_player.targetTexture=_target;
            _player.audioOutputMode=VideoAudioOutputMode.None;_player.waitForFirstFrame=true;
            _player.errorReceived+=OnError;_player.prepareCompleted+=OnPrepared;_player.Prepare();
        }
        private bool ShouldPlay=>isActiveAndEnabled&&_menu!=null&&_menu.Visible&&!WorldRuntimeGate.Ready&&!WorldRuntimeGate.Recording;
        private void OnPrepared(VideoPlayer player){SetPlayback(ShouldPlay);}
        // ★ 修复（防Bug回归）：只在播放意图变化时调用解码器，循环/首帧尚未输出时不能每帧重复Play。
        private void SetPlayback(bool play)
        {
            if(_player==null||!_player.isPrepared||_failed||_playRequested==play)return;
            _playRequested=play;if(play)_player.Play();else _player.Pause();
        }
        private void Update()
        {
            if(_player==null||!_player.isPrepared||_failed)return;
            SetPlayback(ShouldPlay);
        }
        private void OnError(VideoPlayer player,string message){_failed=true;Debug.LogWarning("[主界面背景] 视频播放失败："+message);}
        public void Draw(Rect rect)
        {
            Texture texture=!_failed&&_player!=null&&_player.isPrepared&&_player.frame>=0?_target:(Texture)_poster;
            if(texture!=null)GUI.DrawTexture(rect,texture,ScaleMode.ScaleAndCrop);
            else{GUI.color=new Color(.025f,.05f,.09f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        }
        private void OnDisable(){SetPlayback(false);}
        private void OnDestroy()
        {
            // ★ 适配：先停止并解除视频输出，再释放RT；解码器不能继续写入已释放的画面资源。
            if(_player!=null){_player.errorReceived-=OnError;_player.prepareCompleted-=OnPrepared;_player.Stop();_player.targetTexture=null;Destroy(_player);}
            if(_target!=null){_target.Release();Destroy(_target);}if(_poster!=null)Destroy(_poster);
        }
    }
}
