using DarwinPark.Core;
using DarwinPark.Environment;
using DarwinPark.Planet;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ✦ 新增：独立维护Layers视图；保持现有组件序列化与调用接口。
    public partial class SimulationControlPanel
    {
        private void BuildLayerSection(GameObject parent)
        {
            // ★ 修复 1：先清空父节点下所有旧的子物体，防止重复生成
            foreach (Transform child in parent.transform) Destroy(child.gameObject);

            var grid = parent.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(60, 60);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            // ★ 修复 2：使用 HashSet 去重，防止扫描到相同名字的图片生成两遍
            HashSet<string> uniqueLayerIds = new HashSet<string>();

            foreach (var id in UIIconRegistry.GetAllIds())
            {
                if (id.StartsWith("layer_")) uniqueLayerIds.Add(id);
            }

            // 如果没扫到图片，塞入默认的三个图层
            if (uniqueLayerIds.Count == 0)
            {
                uniqueLayerIds.Add("layer_ore");
                uniqueLayerIds.Add("layer_terrain");
                uniqueLayerIds.Add("layer_climate");
            }

            // ★ 修复（防Bug回归）：补图标后 Resources 的扫描顺序会改变，显式保留旧版矿脉/地形/气候位置。
            var orderedIds=new List<string>();
            foreach(var layer in UIVisualTheme.Data.layerButtons)if(uniqueLayerIds.Contains(layer.id))orderedIds.Add(layer.id);
            foreach(var id in uniqueLayerIds)if(!orderedIds.Contains(id))orderedIds.Add(id);
            foreach (var id in orderedIds)
            {
                string displayName=id.Replace("layer_", "");
                foreach(var layer in UIVisualTheme.Data.layerButtons)if(layer.id==id)displayName=layer.label;
                CreateLayerIcon(parent.transform, id, displayName);
            }
        }
        private void CreateLayerIcon(Transform parent, string iconId, string fallbackText)
        {
            if(!_layerOrder.Contains(iconId))_layerOrder.Add(iconId);
            var btnGO = UIFactory.NewRect(iconId, parent, typeof(Image), typeof(Button));
            var img = btnGO.GetComponent<Image>();
            // ★ 适配：图标与按钮背景分离，选中时只更新面板表面，避免把线条图标当底色染暗。
            UIVisualTheme.Paint(img,"button");
            // ★ 修复（防Bug回归）：SpriteSwap的焦点/悬停图会覆盖取消后的普通底图，图层状态改由唯一选中值驱动。
            btnGO.GetComponent<Button>().transition=Selectable.Transition.None;
            var sprite = UIIconRegistry.Get(iconId);

            if (sprite != null)
            {
                UIVisualTheme.AddIcon(btnGO.transform,iconId,new Vector2(30,30),new Vector2(0,6),new Vector2(.5f,.5f)).color=UIVisualTheme.Muted;
            }
            var txt = UIFactory.NewText("Txt", btnGO.transform, fallbackText, 12, TextAnchor.LowerCenter);
            UIFactory.Stretch(txt.rectTransform,2,4);
            // ★ 适配：新按钮使用网格的下方空位，原三个按钮仍由原三列布局定位。
            if(iconId=="layer_population")
            {var element=btnGO.AddComponent<LayoutElement>();element.ignoreLayout=true;var rt=btnGO.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.sizeDelta=new Vector2(60,44);rt.anchoredPosition=new Vector2(0,-62);
             btnGO.AddComponent<RightClickHandler>().OnRightClick+=PopulationRangeLayer.ToggleStyle;}

            // 绑定点击事件
            btnGO.GetComponent<Button>().onClick.AddListener(() => OnLayerClicked(iconId));
        }
        private void OnLayerClicked(string layerId)
        {
            // ★ 核心修复：如果点击的是当前已选中的图层，则切回默认图层
            if (_currentSelectedLayer == layerId)
            {
                layerId = "default";
            }
            SetLayer(layerId);
        }
        public bool HandleLayerKey(KeyCode key,bool typing=false)
        {
            // ★ 新增：图层只读快捷键对普通成员开放，输入框及覆盖菜单不触发。
            var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if(typing||GameInputGate.Blocked||!WorldRuntimeGate.Ready||selected!=null&&selected.GetComponentInParent<InputField>() is InputField input&&input.isFocused)return false;
            if(key==ControlBindings.Get(ControlAction.ClearLayer)){SetLayer("default");return true;}
            if(key!=ControlBindings.Get(ControlAction.CycleLayer)||_layerOrder.Count==0)return false;
            int index=_layerOrder.IndexOf(_currentSelectedLayer);SetLayer(index+1>=_layerOrder.Count?"default":_layerOrder[index+1]);return true;
        }
        public void SetLayer(string layerId)
        {
            if(layerId!="default"&&!_layerOrder.Contains(layerId))return;
            _currentSelectedLayer = layerId;
            // ★ 适配：新图层独立绘制种群范围，原三个图层的按钮坐标与 Shader 模式保持不变。
            PopulationRangeLayer.SetVisible(layerId=="layer_population");

            Debug.Log($"[Layer] 切换到图层: {layerId}");

            // 1. 更新按钮高亮
            var images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject.name.StartsWith("layer_"))
                {
                    if (img.gameObject.name == layerId && layerId != "default")
                        UIVisualTheme.Paint(img,"selected");
                    else
                        UIVisualTheme.Paint(img,"button");
                }
            }

            // 2. 通知星球的 Shader 切换渲染模式
            var planet = GameObject.Find("Planet");
            if (planet != null)
            {
                var mr = planet.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var block = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(block);

                    if (layerId == "layer_ore")
                    {
                        block.SetFloat("_LayerMode", 3f);
                        if (DarwinPark.Planet.PlanetOreSystem.Instance != null)
                            block.SetTexture("_OreMap", DarwinPark.Planet.PlanetOreSystem.Instance.OreMapTexture);
                    }
                    else if (layerId == "layer_terrain") block.SetFloat("_LayerMode", 1f);
                    else if (layerId == "layer_climate")
                    {
                        // ★ 修复：切到气候图层时确保气候图已生成并绑定。
                        // 旧版没挂 PlanetClimateMap，Shader 只能采样默认灰图，表现为整颗星球发白。
                        var climateMap = DarwinPark.Planet.PlanetClimateMap.Instance;
                        if (climateMap == null)
                        {
                            Log.Error(Log.Tag.Planet, "找不到 PlanetClimateMap，无法显示气候图层");
                        }
                        else
                        {
                            if (!climateMap.IsReady) climateMap.GenerateClimateMap();
                            block.SetFloat("_LayerMode", 2f);
                            // ★ 修复：统一写回贴图 + _ClimateReady + 温度编码范围，
                            // 避免在生成后新建的 block 覆盖掉温度编码参数。
                            climateMap.ApplyToBlock(block);
                        }
                    }
                    else block.SetFloat("_LayerMode", 0f);

                    mr.SetPropertyBlock(block);
                }
            }
        }
    }
}
