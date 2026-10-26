using System.IO;
using DarwinPark.Core;
using UnityEngine;
using UnityEngine.Video;
namespace DarwinPark.UI
{
    // ★ 新增：播放提前录制的游戏内星球，菜单不依赖实时世界生成，首帧由同源封面兜底。
    public sealed class MainMenuBackground : MonoBehaviour
    {
        public string videoFile="Menu/earth-orbit.mp4",posterFile="Menu/earth-orbit.jpg";
        public int videoWidth=1280,videoHeight=720;
        private VideoPlayer _player;
        private RenderTexture _target;
        private Texture2D _poster;
        private GameMainMenu _menu;
        private bool _failed;
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
        private bool ShouldPlay=>_menu!=null&&_menu.Visible&&!WorldRuntimeGate.Ready&&!WorldRuntimeGate.Recording;
        private void OnPrepared(VideoPlayer player){if(ShouldPlay)player.Play();}
        private void Update()
        {
            if(_player==null||!_player.isPrepared||_failed)return;
            if(ShouldPlay&&!_player.isPlaying)_player.Play();else if(!ShouldPlay&&_player.isPlaying)_player.Pause();
        }
        private void OnError(VideoPlayer player,string message){_failed=true;Debug.LogWarning("[主界面背景] 视频播放失败："+message);}
        public void Draw(Rect rect)
        {
            Texture texture=!_failed&&_player!=null&&_player.isPrepared&&_player.frame>=0?_target:(Texture)_poster;
            if(texture!=null)GUI.DrawTexture(rect,texture,ScaleMode.ScaleAndCrop);
            else{GUI.color=new Color(.025f,.05f,.09f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        }
        private void OnDestroy()
        {
            if(_player!=null){_player.errorReceived-=OnError;_player.prepareCompleted-=OnPrepared;_player.Stop();}
            if(_target!=null){_target.Release();Destroy(_target);}if(_poster!=null)Destroy(_poster);
        }
    }
}
