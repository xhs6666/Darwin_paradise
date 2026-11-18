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
            _button=UIFactory.NewButton("Landforms",transform,"升级地貌和水深（先备份）",Apply,new Vector2(376,28)).GetComponent<Button>();var rect=(RectTransform)_button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(12,78);_button.GetComponentInChildren<Text>().fontSize=13;Refresh();
        }
        private void Apply(){if(_builder==null)return;LanEditActions.Submit(new LanCommand{kind="parameter",parameter="landforms",name=_builder.landforms.enabled&&_builder.landforms.version==LandformSettings.CurrentVersion?"0":"1"});}
        private void Refresh(){if(_button==null||_builder==null)return;_button.interactable=WorldAuthority.CanEdit&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading;_button.GetComponentInChildren<Text>().text=_builder.landforms.enabled&&_builder.landforms.version==LandformSettings.CurrentVersion?"关闭新地貌并重建（先备份）":"升级地貌和水深（先备份）";}
        private void Update()=>Refresh();
    }
}
