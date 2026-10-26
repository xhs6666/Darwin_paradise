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
            _volume = PlayerPrefs.GetFloat("MasterVolume", 1); AudioListener.volume = _volume;
            if (PlayerPrefs.HasKey("FullScreen")) Screen.fullScreen = PlayerPrefs.GetInt("FullScreen") != 0;
            if (PlayerPrefs.HasKey("QualityLevel")) QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("QualityLevel"), 0, QualitySettings.names.Length - 1), true);
        }
        private void Start() { Capture(); }
        private void Capture()
        {
            if (ServiceLocator.TryGet<SimulationClock>(out var clock)) { _wasRunning = clock.IsRunning; clock.Pause(); }
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
                if (ServiceLocator.TryGet<SimulationClock>(out var clock) && clock.IsRunning) clock.Pause();
            }
            // ★ 修复（防Bug回归）：Esc 由最早更新的菜单消费，同一帧不再触发退出操控或底层工具取消；F10 无动作。
            if (!Input.GetKeyDown(KeyCode.Escape) || _session.Busy || WorldRuntimeGate.Recording) return;
            if (!_visible && WorldRuntimeGate.Ready) ShowPause();
            else if (_visible && _page == "pause") Resume();
            else if (_visible && _page == "settings") _page = _settingsBack;
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
            if (_wasRunning && ServiceLocator.TryGet<SimulationClock>(out var clock)) clock.Play();
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
            GUI.color = new Color(.025f, .04f, .065f, inGame ? .83f : .42f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
            if (_page == "tools") { _tools.Draw(new Rect(10, 10, Screen.width - 20, Screen.height - 20), () => { _tools.Close(); _page = "main"; }); return; }
            float width = Mathf.Min(_page == "saves" ? 650 : _page == "main" ? 420 : 480, Screen.width - 32);
            float height = Mathf.Min(_page == "create" || _page == "saves" ? 680 : 560, Screen.height - 32);
            // ★ 适配：主菜单靠左留出星球动画，窄窗口保留居中与滚动，按钮不会裁掉。
            float left = _page == "main" && Screen.width >= 900 ? Screen.width * .06f : (Screen.width - width) / 2;
            GUILayout.BeginArea(new Rect(left, (Screen.height - height) / 2, width, height), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Space(18);
            GUILayout.Label("DARWIN PARADISE", new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("达尔文乐园", new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter }); GUILayout.Space(22);
            if (_page == "loading" || _session.Busy) DrawLoading();
            else if (_page == "main")
            {
                if (GUILayout.Button("单人游戏 · 存档", GUILayout.Height(48))) { RefreshSaves(); _page = "saves"; _message = ""; }
                if (GUILayout.Button("多人游戏", GUILayout.Height(48))) _page = "multiplayer";
                if (GUILayout.Button("工具 · 基因树编辑器", GUILayout.Height(48))) { _tools.Open(); if (_tools.Visible) _page = "tools"; else _message = "基因配置加载失败，请检查 Console"; }
                if (GUILayout.Button("设置", GUILayout.Height(48))) { _settingsBack = "main"; _page = "settings"; }
                if (GUILayout.Button("退出游戏", GUILayout.Height(48))) Quit();
            }
            else if (_page == "saves") DrawSaves();
            else if (_page == "create") DrawCreate();
            else if (_page == "pause")
            {
                GUILayout.Label(_session.Current.name, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("返回游戏", GUILayout.Height(50))) Resume();
                if (GUILayout.Button("设置", GUILayout.Height(50))) { _settingsBack = "pause"; _page = "settings"; }
                if (GUILayout.Button("退出游戏", GUILayout.Height(50))) Quit();
                GUILayout.Label(_session.SaveStatus);
            }
            else if (_page == "settings") DrawSettings();
            else if (_page == "multiplayer") { GUILayout.Label("当前尚未接入联网同步与房间服务。"); Back("main"); }
            if (!string.IsNullOrEmpty(_message)) GUILayout.Label(_message, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawSaves()
        {
            GUILayout.Label("选择存档");
            if (GUILayout.Button("创建新世界", GUILayout.Height(44))) NewWorldForm();
            if (_saves.Count == 0) GUILayout.Label("还没有存档，创建一个新世界开始游戏。");
            foreach (var entry in _saves)
            {
                GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label(entry.name);
                if (entry.error != null) GUILayout.Label("无法读取：" + entry.error);
                else
                {
                    GUILayout.Label("种子 " + entry.data.seed + "  ·  游戏时间 " + TimeSpan.FromSeconds(entry.data.gameTime).ToString(@"hh\:mm\:ss"));
                    if (GUILayout.Button("进入世界", GUILayout.Height(38))) TryStart(() => _session.Open(WorldSaveStore.Read(entry.path)));
                }
                GUILayout.EndVertical();
            }
            if (GUILayout.Button("刷新列表", GUILayout.Height(36))) RefreshSaves();
            Back("main");
        }
        private void DrawCreate()
        {
            GUILayout.Label("创建新世界"); GUILayout.Label("世界种子（整数或文字）"); _seed = GUILayout.TextField(_seed, 128);
            if (GUILayout.Button("随机种子")) _seed = UnityEngine.Random.Range(1, int.MaxValue).ToString();
            GUILayout.Label("世界名字（留空使用默认名字）"); _worldName = GUILayout.TextField(_worldName, 128);
            GUILayout.Label("默认：" + WorldSaveStore.DefaultName(WorldSaveStore.SeedFromText(_seed)), new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label("陆地比例 " + Mathf.RoundToInt(_land * 100) + "%"); _land = GUILayout.HorizontalSlider(_land, _session.landRange.x, _session.landRange.y);
            GUILayout.Label("平均温度 " + _temperature.ToString("F1") + " °C"); _temperature = GUILayout.HorizontalSlider(_temperature, _session.temperatureRange.x, _session.temperatureRange.y);
            GUILayout.Label("平均湿度 " + _humidity.ToString("F0") + "%"); _humidity = GUILayout.HorizontalSlider(_humidity, _session.humidityRange.x, _session.humidityRange.y);
            if (GUILayout.Button("创建并进入", GUILayout.Height(46))) TryStart(() => _session.Create(_worldName, WorldSaveStore.SeedFromText(_seed), _land, _temperature, _humidity));
            Back("saves");
        }
        private void DrawLoading()
        {
            GUILayout.Space(50); GUILayout.Label(_session.Status, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            var rect = GUILayoutUtility.GetRect(100, 30, GUILayout.ExpandWidth(true)); GUI.Box(rect, GUIContent.none);
            GUI.color = new Color(.2f, .75f, .8f); GUI.DrawTexture(new Rect(rect.x + 3, rect.y + 3, (rect.width - 6) * _session.Progress, rect.height - 6), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(rect, Mathf.RoundToInt(_session.Progress * 100) + "%", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("正在准备世界" + new string('·', (int)(Time.unscaledTime * 2) % 4));
        }
        private void DrawSettings()
        {
            GUILayout.Label("主音量 " + Mathf.RoundToInt(_volume * 100) + "%"); _volume = GUILayout.HorizontalSlider(_volume, 0, 1); AudioListener.volume = _volume;
            bool full = GUILayout.Toggle(Screen.fullScreen, "全屏显示"); if (full != Screen.fullScreen) Screen.fullScreen = full;
            GUILayout.Label("画质"); var names = QualitySettings.names; int quality = GUILayout.SelectionGrid(QualitySettings.GetQualityLevel(), names, 2);
            if (quality != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(quality, true);
            if (GUILayout.Button("保存设置", GUILayout.Height(40))) { PlayerPrefs.SetFloat("MasterVolume", _volume); PlayerPrefs.SetInt("FullScreen", Screen.fullScreen ? 1 : 0); PlayerPrefs.SetInt("QualityLevel", QualitySettings.GetQualityLevel()); PlayerPrefs.Save(); _message = "设置已保存"; }
            Back(_settingsBack);
        }
        private void Back(string page) { if (GUILayout.Button("返回", GUILayout.Height(38))) { _page = page; _message = ""; } }
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
