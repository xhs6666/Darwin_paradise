using DarwinPark.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：统计卡右上角为本存档设置，底部一键清除全部实体；筛选条件不改变清除范围。
    public sealed class MapEntityActions:MonoBehaviour
    {
        private Button _gear,_clear;
        private void Start()
        {
            _gear=UIFactory.NewButton("WorldSettings",transform,"",()=>WorldGameSettingsPanel.Ensure().Open(),new Vector2(26,24));
            var rt=_gear.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=Vector2.one;rt.pivot=Vector2.one;rt.anchoredPosition=new Vector2(-7,-3);
            UIVisualTheme.AddIcon(_gear.transform,"gear",new Vector2(18,18),Vector2.zero,new Vector2(.5f,.5f));
            _clear=UIFactory.NewButton("ClearAllEntities",transform,"清除全部实体",()=>{if(LanEditActions.Submit(new LanCommand{kind="clearEntities"}))GetComponent<MapEntityStatisticsUI>().RefreshNow();},new Vector2(236,24));
            var rect=_clear.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(12,-159);rect.offsetMax=new Vector2(-12,-135);
            _clear.GetComponentInChildren<Text>().fontSize=12;RefreshPermission();
        }
        private void Update(){if(_gear!=null)RefreshPermission();}
        private void RefreshPermission(){bool allowed=WorldAuthority.CanEdit;_gear.gameObject.SetActive(allowed);_clear.gameObject.SetActive(allowed);}
    }
}
