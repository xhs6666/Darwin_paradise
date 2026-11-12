using DarwinPark.Core;
using DarwinPark.Planet;
using UnityEngine;
using UnityEngine.UI;
namespace DarwinPark.UI
{
    // ★ 新增：旧世界可显式重建新地貌，沿用后台更新、实体保留和保存链路。
    public sealed class LandformUpgradeControl:MonoBehaviour
    {
        private PlanetTerrainMeshBuilder _builder;private Button _button;
        public void Bind(PlanetTerrainMeshBuilder builder)
        {
            _builder=builder;var panel=(RectTransform)transform;panel.sizeDelta=new Vector2(panel.sizeDelta.x,panel.sizeDelta.y+40);
            _button=UIFactory.NewButton("Landforms",transform,"一键升级高原 / 河流 / 盆地（先备份）",Apply,new Vector2(376,28)).GetComponent<Button>();var rect=(RectTransform)_button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(12,78);_button.GetComponentInChildren<Text>().fontSize=13;Refresh();
        }
        private void Apply(){if(_builder==null)return;LanEditActions.Submit(new LanCommand{kind="parameter",parameter="landforms",name=_builder.landforms.enabled&&_builder.landforms.version==LandformSettings.CurrentVersion?"0":"1"});}
        private void Refresh(){if(_button==null||_builder==null)return;_button.interactable=WorldAuthority.CanEdit&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading;_button.GetComponentInChildren<Text>().text=_builder.landforms.enabled&&_builder.landforms.version==LandformSettings.CurrentVersion?"关闭新地貌并重建（先备份）":"一键升级高原 / 河流 / 盆地（先备份）";}
        private void Update()=>Refresh();
    }
    public static class WorldLandformEdits
    {
        public static string Kind(string id)=>id=="landforms"?"landforms":null;
        public static void Apply(PlanetTerrainConfig config,string kind,float value){if(kind=="landforms")
            {config.landforms??=new LandformSettings();config.landforms.enabled=value>0;
                // ★ 适配：旧档只在玩家明确启用时升级算法；读档和其他参数修改继续使用旧版本。
                if(value>0){if(config.landforms.version<2){var defaults=new LandformSettings();if(config.landforms.basinTransition<=0){config.landforms.basinTransition=defaults.basinTransition;config.landforms.basinThreshold=defaults.basinThreshold;}if(config.landforms.lakeMeshSubdivisions<1)config.landforms.lakeMeshSubdivisions=defaults.lakeMeshSubdivisions;}config.landforms.version=LandformSettings.CurrentVersion;}}}
    }
}
