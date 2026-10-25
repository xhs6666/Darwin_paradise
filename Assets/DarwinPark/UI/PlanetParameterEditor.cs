using System.Globalization;
using DarwinPark.Core;
using DarwinPark.Environment;
using DarwinPark.Planet;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    /// <summary>★ 新增：参数使用草稿值，滑条与输入框联动，Shift 确认后才重建星球。</summary>
    public sealed class PlanetParameterEditor : MonoBehaviour
    {
        public int seedSliderMin = 0, seedSliderMax = 1000000;
        public float temperatureMin = -30, temperatureMax = 60;
        public System.Action CancelRequested;
        private string _id;
        private InputField _input;
        private Slider _slider;
        private Text _status;
        private PlanetTerrainMeshBuilder _builder;
        public void Initialize(string id, string label)
        {
            _id = id; _builder = Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            bool seed = id.Contains("seed"), temp = id.Contains("temp");
            // ★ 适配：压缩空白，标题、编辑行和状态行各占一行。
            GetComponent<RectTransform>().sizeDelta = new Vector2(400, 102);
            transform.Find("Title").GetComponent<Text>().text = "设定" + label + (seed ? "" : "（全年平均）");
            string initial = seed ? (_builder != null ? _builder.seed : 0).ToString() :
                (temp ? ClimateDataHub.BaseTemperature : ClimateDataHub.BaseHumidity).ToString("F1", CultureInfo.InvariantCulture);
            var inputGO = UIFactory.NewRect("ValueInput", transform, typeof(Image), typeof(InputField));
            var irt = inputGO.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0, 0); irt.pivot = Vector2.zero;
            irt.anchoredPosition = new Vector2(12, 38); irt.sizeDelta = new Vector2(112, 28);
            inputGO.GetComponent<Image>().color = new Color(.12f,.18f,.24f);
            _input = inputGO.GetComponent<InputField>();
            var text = UIFactory.NewText("Text", inputGO.transform, initial, 16);
            UIFactory.Stretch(text.rectTransform, 8, 2); _input.textComponent = text;
            _input.contentType = seed ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;
            _input.text = initial;
            float min = seed ? seedSliderMin : temp ? temperatureMin : 0;
            float max = seed ? seedSliderMax : temp ? temperatureMax : 100;
            float.TryParse(initial, NumberStyles.Float, CultureInfo.InvariantCulture, out float current);
            _slider = UIFactory.NewSlider("Parameter", transform, "", min, max, current, 252, out _);
            var row = _slider.transform.parent.GetComponent<RectTransform>();
            row.anchorMin = row.anchorMax = Vector2.zero; row.pivot = Vector2.zero;
            row.anchoredPosition = new Vector2(136, 36);
            var sliderRect = _slider.GetComponent<RectTransform>();
            sliderRect.offsetMin = new Vector2(4,4); sliderRect.offsetMax = new Vector2(-4,-4);
            _slider.wholeNumbers = seed;
            _slider.onValueChanged.AddListener(v => {
                _input.SetTextWithoutNotify(v.ToString(seed ? "F0" : "F1", CultureInfo.InvariantCulture));
                // ★ 修复（防Bug回归）：应用后再次改值必须恢复待提交状态，不能仍显示“已应用”。
                if (_status != null) _status.text = "待应用（Shift）";
            });
            _input.onValueChanged.AddListener(v => {
                if (_status != null) _status.text = "待应用（Shift）";
                if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && !float.IsNaN(f) && !float.IsInfinity(f))
                    _slider.SetValueWithoutNotify(f);
            });
            var apply = UIFactory.NewButton("Apply", transform, "应用（Shift）", Apply, new Vector2(112,25));
            apply.GetComponentInChildren<Text>().fontSize = 14;
            var art = apply.GetComponent<RectTransform>(); art.anchorMin = art.anchorMax = Vector2.zero;
            art.pivot = Vector2.zero; art.anchoredPosition = new Vector2(276,7);
            _status = UIFactory.NewText("Status", transform, "修改后按 Shift 应用", 12);
            var srt = _status.rectTransform; srt.anchorMin = srt.anchorMax = Vector2.zero;
            srt.pivot = Vector2.zero; srt.anchoredPosition = new Vector2(12,7); srt.sizeDelta = new Vector2(255,25);
        }
        private void Update()
        {
            // ★ 修复（防Bug回归）：右键取消独立处理，拖动滑条或点击内容不会销毁未提交草稿。
            if(Input.GetMouseButtonDown(1)){CancelRequested?.Invoke();return;}
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) Apply();
        }
        private void OnGUI()
        {
            // ★ 修复（防Bug回归）：输入框聚焦或按键在低帧率内快速按下/抬起时，逐个键盘事件也要接收 Shift。
            var key = Event.current;
            if (key.type == EventType.KeyDown && (key.shift || key.keyCode == KeyCode.LeftShift || key.keyCode == KeyCode.RightShift))
                Apply();
        }
        public void Apply()
        {
            if (_id.Contains("seed"))
            {
                if (!int.TryParse(_input.text, out int seed)) { _status.text = "请输入有效的 32 位整数种子"; return; }
                if (_builder == null) { _status.text = "星球尚未就绪"; return; }
                if (_builder.seed != seed) { _builder.seed = seed; _builder.Build(); PlanetClimateMap.Instance?.MarkDirty(); }
            }
            else
            {
                if (!float.TryParse(_input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || float.IsNaN(v) || float.IsInfinity(v))
                { _status.text = "请输入有效数字"; return; }
                bool temp = _id.Contains("temp"); v = Mathf.Clamp(v, _slider.minValue, _slider.maxValue);
                if (ServiceLocator.TryGet<EnvironmentSystem>(out var env))
                { if (temp) env.BaseTemperature = v; else env.BaseHumidity = v; }
                if (temp) ClimateDataHub.BaseTemperature = v; else ClimateDataHub.BaseHumidity = v;
                _input.SetTextWithoutNotify(v.ToString("F1", CultureInfo.InvariantCulture));
                PlanetClimateMap.Instance?.MarkDirty();
            }
            _status.text = "已应用";
        }
    }
}
