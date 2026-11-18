using System;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ✦ 新增：构建时绑定权限控件，刷新时不重复沿 Transform 路径查找；重建 HUD 时重新绑定。
    internal sealed class SimulationAuthorityView
    {
        private readonly Button _next,_play;
        private readonly Transform _spawning,_parameters;
        public SimulationAuthorityView(Transform bottom,Transform page2,Button next)
        {
            _next=next;
            _spawning=page2!=null?page2.Find("EntitySpawnPanel"):null;
            _play=bottom!=null?bottom.Find("ContentArea/Page1/TimeSec/PlayBtn")?.GetComponent<Button>():null;
            _parameters=bottom!=null?bottom.Find("ContentArea/Page1/ParamSec"):null;
        }
        public void Apply(bool canEdit,Action closeMenus)
        {
            if(_next==null)return;
            _next.interactable=true;
            if(!canEdit)closeMenus();
            if(_spawning!=null)_spawning.gameObject.SetActive(canEdit);
            if(_play!=null)_play.interactable=canEdit;
            if(_parameters!=null)_parameters.gameObject.SetActive(canEdit);
        }
    }
}
