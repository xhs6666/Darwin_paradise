using System;
using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：菜单只负责导航；世界生成、存档和背景视频各自独立，首次启动不生成世界。
    [DefaultExecutionOrder(-1100)]
    [RequireComponent(typeof(GeneTreeWorkbench), typeof(WorldSessionManager), typeof(MainMenuBackground))]
    public sealed class GameMainMenu : MonoBehaviour
    {
        private string _page = "main", _settingsBack = "main", _message = "";
        private bool _visible = true, _wasRunning = true, _hudCaptured;
        private GeneTreeWorkbench _tools;
        private WorldSessionManager _session;
        private MainMenuBackground _background;
        private UIRoot _root;
        private bool _canvasEnabled, _worldCanvasEnabled, _raycasterEnabled;
        private GraphicRaycaster _raycaster;
        private float _volume = 1, _land = .5f, _temperature = 20, _humidity = 44;
        private string _worldName = "", _seed = "";
        private List<WorldSaveEntry> _saves = new List<WorldSaveEntry>();
        private Vector2 _scroll;
        public bool Visible => _visible;

        private void Awake()
        {
            // ★ 修复（防Bug回归）：必须早于星球 Start 设置门控，菜单之前不能同步生成高精度地形。
            WorldRuntimeGate.Managed = true; WorldRuntimeGate.Ready = false;
            GameInputGate.Blocked = true;
            _tools = GetComponent<GeneTreeWorkbench>();
            _session = GetComponent<WorldSessionManager>();
            _background = GetComponent<MainMenuBackground>();
            _session.Loaded += OnWorldLoaded; _session.Failed += OnLoadFailed;
            // ★ 适配：局域网和RTS各用独立组件，菜单只提供入口。
            LanSession.Ensure(gameObject);
            _volume = PlayerPrefs.GetFloat("MasterVolume", 1); AudioListener.volume = _volume;
            if (PlayerPrefs.HasKey("FullScreen")) Screen.fullScreen = PlayerPrefs.GetInt("FullScreen") != 0;
            if (PlayerPrefs.HasKey("QualityLevel")) QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("QualityLevel"), 0, QualitySettings.names.Length - 1), true);
        }
        private void Start() { Capture(); }
        private void Capture()
        {
            if (ServiceLocator.TryGet<SimulationClock>(out var clock)) { _wasRunning = clock.IsRunning;if(LanSession.Instance?.Online!=true)clock.Pause(); }
            // ★ 修复（防Bug回归）：暂停保留实体视角，只释放鼠标和攻击输入，返回游戏仍操控原实体。
            GetComponent<EntityPossessionInput>()?.Suspend();
            EntityPlacementTool.Instance?.CancelPlacement();
            HideHud(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private void HideHud()
        {
            if (_hudCaptured || !ServiceLocator.TryGet<UIRoot>(out _root) || _root.MainCanvas == null) return;
            _hudCaptured = true; _canvasEnabled = _root.MainCanvas.enabled; _root.MainCanvas.enabled = false;
            if (_root.WorldCanvas != null) { _worldCanvasEnabled = _root.WorldCanvas.enabled; _root.WorldCanvas.enabled = false; }
            _raycaster = _root.MainCanvas.GetComponent<GraphicRaycaster>();
            if (_raycaster != null) { _raycasterEnabled = _raycaster.enabled; _raycaster.enabled = false; }
        }
        private void Update()
        {
            if (_visible)
            {
                HideHud();
                if (LanSession.Instance?.Online!=true&&ServiceLocator.TryGet<SimulationClock>(out var clock) && clock.IsRunning) clock.Pause();
            }
            // ★ 修复（防Bug回归）：Esc 由最早更新的菜单消费，同一帧不再触发退出操控或底层工具取消；F10 无动作。
            if (!Input.GetKeyDown(KeyCode.Escape) || _session.Busy || WorldRuntimeGate.Recording) return;
            if (!_visible && WorldRuntimeGate.Ready) ShowPause();
            else if (_visible && _page == "pause") Resume();
            else if (_visible && _page == "settings") _page = _settingsBack;
            else if (_visible && _page == "multiplayer" && LanSession.Instance?.Online==true) LanSession.Instance.LeaveToMenu("已取消连接");
            else if (_visible && (_page == "saves" || _page == "create" || _page == "multiplayer")) _page = "main";
            GameInputGate.ConsumeEscape();
        }
        public void ShowPause()
        {
            if (_session.Busy || !WorldRuntimeGate.Ready || _visible) return;
            _visible = true; _page = "pause"; _message = ""; GameInputGate.Blocked = true; Capture();
        }
        private void Resume()
        {
            if (!WorldRuntimeGate.Ready || _session.Busy) return;
            _visible = false; _tools.Close(); GameInputGate.Blocked = false;
            RestoreHud();
            if (_wasRunning && LanSession.Instance?.Online!=true&&ServiceLocator.TryGet<SimulationClock>(out var clock)) clock.Play();
        }
        private void RestoreHud()
        {
            if (!_hudCaptured) return;
            if (_root != null) { _root.MainCanvas.enabled = _canvasEnabled; if (_root.WorldCanvas != null) _root.WorldCanvas.enabled = _worldCanvasEnabled; }
            if (_raycaster != null) _raycaster.enabled = _raycasterEnabled;
            _hudCaptured = false;
        }
        private void OnWorldLoaded() { _wasRunning = _session.Current.running; Resume(); }
        private void OnLoadFailed(string error) { _page = "saves"; _message = error; RefreshSaves(); }
        private void RefreshSaves()
        {
            try { _saves = WorldSaveStore.List(); }
            catch (Exception ex) { _message = "无法读取存档目录：" + ex.Message; Debug.LogException(ex); }
        }
        private void NewWorldForm()
        {
            _seed = UnityEngine.Random.Range(1, int.MaxValue).ToString(); _worldName = "";
            _land = _session.defaultLand; _temperature = _session.defaultTemperature; _humidity = _session.defaultHumidity;
            _message = ""; _page = "create";
        }
        private void TryStart(Action action)
        {
            try { action(); if (_session.Busy) { _page = "loading"; _message = ""; } }
            catch (Exception ex) { _message = ex.Message; Debug.LogWarning("[世界] " + ex.Message); }
        }
        private void OnGUI()
        {
            if (WorldRuntimeGate.Recording || !_visible) return;
            GUI.depth = -1000; RuntimeMenuTheme.Apply();
            bool inGame = _session.Current != null && WorldRuntimeGate.Ready;
            if (!inGame) _background.Draw(new Rect(0, 0, Screen.width, Screen.height));
            // ★ 适配：保持实时/预录星球背景，只降低遮罩浓度，让新主题与样图的太空背景融合。
            GUI.color = new Color(.025f, .04f, .065f, inGame ? .62f : .12f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
            if (_page == "tools") { _tools.Draw(new Rect(10, 10, Screen.width - 20, Screen.height - 20), () => { _tools.Close(); _page = "main"; }); return; }
            float width = Mathf.Min(_page == "saves" ? 650 : _page == "main" ? 320 : 480, Screen.width - 32);
            float height = Mathf.Min(_page == "create" || _page == "saves" ? 680 : 560, Screen.height - 32);
            // ★ 适配：主菜单靠左留出星球动画，窄窗口保留居中与滚动，按钮不会裁掉。
            float left = _page == "main" && Screen.width >= 900 ? Screen.width * .06f : (Screen.width - width) / 2;
            // ★ 适配：严格沿用旧窗口的矩形；主菜单仅移除底板，不移动任何页面的屏幕位置。
            GUILayout.BeginArea(new Rect(left, (Screen.height - height) / 2, width, height), _page=="main"?GUIStyle.none:GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Space(18);
            DrawPageHeading(); GUILayout.Space(22);
            if (_page == "loading" || _session.Busy) DrawLoading();
            else if (_page == "main")
            {
                if (RuntimeMenuTheme.MenuButton("单人游戏 · 存档","play",true)) { RefreshSaves(); _page = "saves"; _message = ""; }
                if (RuntimeMenuTheme.MenuButton("多人游戏","people")) _page = "multiplayer";
                if (RuntimeMenuTheme.MenuButton("工具 · 基因树编辑器","dna")) { _tools.Open(); if (_tools.Visible) _page = "tools"; else _message = "基因配置加载失败，请检查 Console"; }
                if (RuntimeMenuTheme.MenuButton("设置","gear")) { _settingsBack = "main"; _page = "settings"; }
                if (RuntimeMenuTheme.MenuButton("退出游戏","exit")) Quit();
            }
            else if (_page == "saves") DrawSaves();
            else if (_page == "create") DrawCreate();
            else if (_page == "pause")
            {
                GUILayout.Label(_session.Current.name, new GUIStyle(RuntimeMenuTheme.Muted) { alignment = TextAnchor.MiddleCenter });
                if (RuntimeMenuTheme.Button("返回游戏","play",true,50)) Resume();
                if (RuntimeMenuTheme.Button("设置","gear",false,50)) { _settingsBack = "pause"; _page = "settings"; }
                GetComponent<LanLobbyUI>().DrawPause();
                if (RuntimeMenuTheme.Button(LanSession.Instance?.IsClient==true?"离开联机并返回主界面":"保存并返回主界面","home",false,50)) ReturnToMainMenu();
                if (RuntimeMenuTheme.Button("退出游戏",null,false,50,true)) Quit();
                GUILayout.Label(_session.SaveStatus);
                GUILayout.Label("Esc  返回游戏",new GUIStyle(RuntimeMenuTheme.Muted){alignment=TextAnchor.MiddleCenter});
            }
            else if (_page == "settings") DrawSettings();
            else if (_page == "multiplayer") {GetComponent<LanLobbyUI>().DrawJoin();Back("main");}
            if (!string.IsNullOrEmpty(_message)) GUILayout.Label(_message, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawPageHeading()
        {
            // ★ 新增：标题使用真实线条图标，页面标题与原品牌标题占用相同的区域。
            string title=_page=="main"?"达尔文乐园":_page=="pause"?(LanSession.Instance?.Online==true?"联机菜单":"游戏已暂停"):_page=="saves"?"选择存档":_page=="create"?"创建新世界":_page=="settings"?"设置":_page=="multiplayer"?"多人游戏":"正在准备世界";
            string icon=_page=="settings"?"gear":_page=="multiplayer"?"network":"planet";
            var style=new GUIStyle(RuntimeMenuTheme.Title){fontSize=_page=="main"?30:26,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(42,0,0,0)};
            GUILayout.Label(title,style,GUILayout.Height(38));var rect=GUILayoutUtility.GetLastRect();
            float titleWidth=style.CalcSize(new GUIContent(title)).x;
            RuntimeMenuTheme.Icon(new Rect(rect.center.x-titleWidth*.5f-4,rect.center.y-18,36,36),icon,UIVisualTheme.Accent);
            GUILayout.Label("D A R W I N   P A R A D I S E",new GUIStyle(RuntimeMenuTheme.Subtitle){alignment=TextAnchor.MiddleCenter},GUILayout.Height(26));
        }
        private void DrawSaves()
        {
            GUILayout.Label("选择一个世界，继续演化",RuntimeMenuTheme.Muted);
            if (RuntimeMenuTheme.Button("创建新世界",null,true,44)) NewWorldForm();
            if (_saves.Count == 0) GUILayout.Label("还没有存档，创建一个新世界开始游戏。");
            foreach (var entry in _saves)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(entry.name,new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold,padding=new RectOffset(30,0,0,0)});
                var nameRect=GUILayoutUtility.GetLastRect();RuntimeMenuTheme.Icon(new Rect(nameRect.x,nameRect.y,24,24),"planet",UIVisualTheme.Accent);
                if (entry.error != null) GUILayout.Label("无法读取：" + entry.error);
                else
                {
                    string seedText="种子 "+entry.data.seed;
                    var metadataStyle=new GUIStyle(RuntimeMenuTheme.Muted){padding=new RectOffset(26,0,0,0)};
                    GUILayout.Label(seedText+"        游戏时间 "+TimeSpan.FromSeconds(entry.data.gameTime).ToString(@"hh\:mm\:ss"),metadataStyle);
                    var metadataRect=GUILayoutUtility.GetLastRect();RuntimeMenuTheme.Icon(new Rect(metadataRect.x,metadataRect.y,20,20),"leaf",UIVisualTheme.Accent);
                    float clockX=metadataRect.x+new GUIStyle(RuntimeMenuTheme.Muted).CalcSize(new GUIContent(seedText)).x+32;
                    RuntimeMenuTheme.Icon(new Rect(clockX,metadataRect.y,20,20),"clock",UIVisualTheme.Muted);
                    if (RuntimeMenuTheme.Button("进入世界","play",true,38)) TryStart(() => _session.Open(WorldSaveStore.Read(entry.path)));
                }
                GUILayout.EndVertical();
            }
            if (RuntimeMenuTheme.Button("刷新列表","refresh",false,36)) RefreshSaves();
            Back("main");
        }
        private void DrawCreate()
        {
            GUILayout.Label("世界参数",RuntimeMenuTheme.Muted); GUILayout.Label("世界种子（整数或文字）"); _seed = GUILayout.TextField(_seed, 128);
            if (RuntimeMenuTheme.Button("随机种子","dice",false,32)) _seed = UnityEngine.Random.Range(1, int.MaxValue).ToString();
            GUILayout.Label("世界名字（留空使用默认名字）"); _worldName = GUILayout.TextField(_worldName, 128);
            GUILayout.Label("默认：" + WorldSaveStore.DefaultName(WorldSaveStore.SeedFromText(_seed)), new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label("陆地比例 " + Mathf.RoundToInt(_land * 100) + "%"); _land = RuntimeMenuTheme.Slider(_land, _session.landRange.x, _session.landRange.y);
            GUILayout.Label("平均温度 " + _temperature.ToString("F1") + " °C"); _temperature = RuntimeMenuTheme.Slider(_temperature, _session.temperatureRange.x, _session.temperatureRange.y);
            GUILayout.Label("平均湿度 " + _humidity.ToString("F0") + "%"); _humidity = RuntimeMenuTheme.Slider(_humidity, _session.humidityRange.x, _session.humidityRange.y);
            if (RuntimeMenuTheme.Button("创建并进入","play",true,46)) TryStart(() => _session.Create(_worldName, WorldSaveStore.SeedFromText(_seed), _land, _temperature, _humidity));
            Back("saves");
        }
        private void DrawLoading()
        {
            GUILayout.Space(50); GUILayout.Label(_session.Status, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            var rect = GUILayoutUtility.GetRect(100, 30, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(new Rect(rect.x,rect.y+9,rect.width-56,10),UIVisualTheme.SurfaceSprite("track").texture,ScaleMode.StretchToFill,true,0,Color.white,0,5);
            GUI.DrawTexture(new Rect(rect.x,rect.y+9,(rect.width-56)*_session.Progress,10),UIVisualTheme.SurfaceSprite("fill").texture,ScaleMode.StretchToFill,true,0,Color.white,0,5);
            GUI.Label(new Rect(rect.xMax-50,rect.y,50,30), Mathf.RoundToInt(_session.Progress * 100) + "%");
            GUILayout.Label("正在准备世界" + new string('·', (int)(Time.unscaledTime * 2) % 4));
            // ★ 适配：联机加载使用已验证的取消/清场入口，用户不必等待网络超时才能返回。
            if(LanSession.Instance?.IsClient==true&&RuntimeMenuTheme.Button("取消加入",null,false,38))LanSession.Instance.LeaveToMenu("已取消加入");
        }
        private void DrawSettings()
        {
            GUILayout.Label("主音量 " + Mathf.RoundToInt(_volume * 100) + "%"); _volume = RuntimeMenuTheme.Slider(_volume, 0, 1); AudioListener.volume = _volume;
            GUILayout.BeginHorizontal();GUILayout.Label("全屏显示");GUILayout.FlexibleSpace();
            bool full=Screen.fullScreen;if(GUILayout.Button(full?"●  开":"○  关",full?RuntimeMenuTheme.Primary:GUI.skin.button,GUILayout.Width(80),GUILayout.Height(30)))full=!full;
            GUILayout.EndHorizontal();if (full != Screen.fullScreen) Screen.fullScreen = full;
            GUILayout.Label("画质"); var names = QualitySettings.names; int quality=QualitySettings.GetQualityLevel();
            GUILayout.BeginHorizontal();for(int i=0;i<names.Length;i++)if(GUILayout.Button(names[i],i==quality?RuntimeMenuTheme.Primary:GUI.skin.button,GUILayout.Height(36)))quality=i;GUILayout.EndHorizontal();
            if (quality != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(quality, true);
            if (RuntimeMenuTheme.Button("保存设置","save",true,40)) { PlayerPrefs.SetFloat("MasterVolume", _volume); PlayerPrefs.SetInt("FullScreen", Screen.fullScreen ? 1 : 0); PlayerPrefs.SetInt("QualityLevel", QualitySettings.GetQualityLevel()); PlayerPrefs.Save(); _message = "设置已保存"; }
            // ★ 新增：测试日志只在设置中提供定位入口，不新增遮挡游戏的日志面板。
            if(!string.IsNullOrEmpty(RuntimeFileLog.DirectoryPath))
            {
                GUILayout.Label("日志目录："+RuntimeFileLog.DirectoryPath,new GUIStyle(RuntimeMenuTheme.Muted){wordWrap=true});
                if(RuntimeMenuTheme.Button("打开日志文件夹",null,false,36))RuntimeFileLog.OpenDirectory();
            }
            Back(_settingsBack);
        }
        private void Back(string page) { if (RuntimeMenuTheme.Button("返回","back",false,38)) { if(_page=="multiplayer"&&LanSession.Instance?.Online==true)LanSession.Instance.LeaveToMenu("已取消连接");else{_page = page; _message = "";} } }
        // ★ 修复（防Bug回归）：暂停页原先只有退出程序，补齐保存并卸载世界的导航路径。
        public void ReturnToMainMenu()
        {
            if(!_visible||_page!="pause"||_session.Busy)return;
            if(LanSession.Instance?.IsClient==true){LanSession.Instance.LeaveToMenu();return;}
            if(LanSession.Instance?.IsHost==true)LanSession.Instance.CloseHost();
            try
            {
                _session.ReturnToMenu(_wasRunning);
                _page="main";_settingsBack="main";_message="世界已保存";_scroll=Vector2.zero;
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            }
            catch(Exception ex){_message="保存失败，尚未返回主界面："+ex.Message;Debug.LogException(ex);}
        }
        // ★ 适配：远端断线/离开只卸载副本，不显示“世界已保存”或误写管理员存档。
        public void ShowNetworkMain(string message)
        {_visible=true;_page="main";_message=message;_scroll=Vector2.zero;GameInputGate.Blocked=true;Capture();}
        private void Quit()
        {
            // ★ 修复（防Bug回归）：保存失败必须留在游戏内显示原因，避免退出时静默丢失进度。
            try { if (_session.Current != null) _session.SaveNow(_visible ? _wasRunning : (bool?)null); }
            catch (Exception ex) { _message = "保存失败，尚未退出：" + ex.Message; Debug.LogException(ex); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OnDestroy()
        {
            if (_session != null) { _session.Loaded -= OnWorldLoaded; _session.Failed -= OnLoadFailed; }
            GameInputGate.Blocked = false; RestoreHud();
        }
    }
}
