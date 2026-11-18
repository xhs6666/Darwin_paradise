using DarwinPark.Core;
using DarwinPark.Environment;
using DarwinPark.Planet;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ✦ 新增：独立维护Time视图；保持现有组件序列化与调用接口。
    public partial class SimulationControlPanel
    {
        private void BuildTimeSection(GameObject parent)
        {
            var btnGO = UIFactory.NewRect("PlayBtn", parent.transform, typeof(Image), typeof(Button), typeof(RightClickHandler));
            var brt = btnGO.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 0.5f);
            brt.anchorMax = new Vector2(0, 0.5f);
            brt.pivot = new Vector2(0, 0.5f);
            brt.sizeDelta = new Vector2(80, 80);
            brt.anchoredPosition = new Vector2(0, 0);

            var bg = btnGO.GetComponent<Image>();
            UIVisualTheme.Paint(bg,"button");

            btnGO.GetComponent<Button>().onClick.AddListener(() => _clock?.Toggle());

            var iconGO = UIFactory.NewRect("Icon", btnGO.transform, typeof(Image));
            UIFactory.Stretch(iconGO.GetComponent<RectTransform>(), 15, 15);
            _playIcon = iconGO.GetComponent<Image>();
            _playIcon.sprite = _iconPause;
            _playIcon.preserveAspect = true;
            _playIcon.color=UIVisualTheme.Accent;_playIcon.raycastTarget=false;

            // ★ 适配：没有图标资源时用文字兜底，保证播放/暂停按钮始终可辨认。
            if (_iconPause == null && _iconPlay == null)
            {
                _playFallback = UIFactory.NewText("PlayFallback", btnGO.transform, "II", 26, TextAnchor.MiddleCenter);
                UIFactory.Stretch(_playFallback.rectTransform);
                _playFallback.raycastTarget = false;
            }

            btnGO.GetComponent<RightClickHandler>().OnRightClick += () => ToggleSpeedMenu(brt);

            var timeTextGO = UIFactory.NewText("TimeText", parent.transform, "0-00-00\n(第 0 天) 00:00", 16, TextAnchor.MiddleLeft);
            var trt = timeTextGO.rectTransform;
            trt.anchorMin = new Vector2(0, 0.5f);
            trt.anchorMax = new Vector2(0, 0.5f);
            trt.pivot = new Vector2(0, 0.5f);
            trt.sizeDelta = new Vector2(200, 80);
            trt.anchoredPosition = new Vector2(100, 0);
            _timeText = timeTextGO;
        }
        private void ToggleSpeedMenu(RectTransform targetBtn)
        {
            if(!WorldAuthority.CanEdit)return;
            if (_activeSpeedMenu != null) { Destroy(_activeSpeedMenu); _activeSpeedMenu = null; return; }
            DestroyMenus();

            var root = ServiceLocator.Get<UIRoot>("需要 UIRoot").MainCanvas.transform;
            _activeSpeedMenu = UIFactory.NewRect("SpeedMenu", root, typeof(Image));
            var mrt = _activeSpeedMenu.GetComponent<RectTransform>();
            UIVisualTheme.Paint(_activeSpeedMenu.GetComponent<Image>());

            Vector3[] corners = new Vector3[4];
            targetBtn.GetWorldCorners(corners);
            mrt.position = corners[1] + new Vector3(0, 10, 0);
            mrt.pivot = new Vector2(0, 0);
            mrt.sizeDelta = new Vector2(120, 200);

            var vlg = _activeSpeedMenu.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 2;

            string[] speeds = { "暂停", "1x", "2x", "4x", "8x", "16x" };
            float[] values = { 0f, 1f, 2f, 4f, 8f, 16f };

            for (int i = 0; i < speeds.Length; i++)
            {
                float speedValue = values[i];
                var btn = UIFactory.NewButton("Speed_" + i, _activeSpeedMenu.transform, speeds[i], () => {
                    if (speedValue == 0) _clock.Pause();
                    else { _clock.SetTimeScale(speedValue); if (!_clock.IsRunning) _clock.Play(); }
                    DestroyMenus();
                }, new Vector2(100, 30));
                btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            }
        }
        private void OnRunningChanged(bool running)
        {
            if (_playIcon != null) _playIcon.sprite = running ? _iconPause : _iconPlay;
            if (_playFallback != null) _playFallback.text = running ? "II" : "▶";
        }
    }
}
