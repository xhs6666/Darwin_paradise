using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：世界资源提供只读属性，尸体明确标为已死亡，不伪装成能编辑/繁殖/操控的活实体。
    public sealed class WorldResourceInspector:MonoBehaviour
    {
        public static WorldResourceInspector Instance {get;private set;}public ColonyResource Current {get;private set;}
        public Vector2 panelSize=new Vector2(360,206),cornerOffset=new Vector2(14,-16);public float refreshInterval=.5f;
        private GameObject _panel;private Text _title,_body;private float _next;
        private void Awake()=>Instance=this;
        public static WorldResourceInspector Ensure(){if(Instance!=null)return Instance;return ServiceLocator.Get<UIRoot>().gameObject.AddComponent<WorldResourceInspector>();}
        public void Show(ColonyResource resource)
        {
            if(resource==null)return;Object.FindFirstObjectByType<GeneInspectorUI>()?.HideEntityInfo();
            if(_panel==null)
            {
                _panel=UIFactory.NewRect("WorldResourcePanel",ServiceLocator.Get<UIRoot>().MainCanvas.transform,typeof(Image));UIVisualTheme.Paint(_panel.GetComponent<Image>());
                _title=UIFactory.NewText("Title",_panel.transform,"",18);Place(_title.rectTransform,12,10,34,44);
                _body=UIFactory.NewText("Properties",_panel.transform,"",14,TextAnchor.UpperLeft);Place(_body.rectTransform,12,52,panelSize.y-64,12);
                var close=UIFactory.NewButton("Close",_panel.transform,"×",Hide,new Vector2(28,26));var rect=close.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.one;rect.pivot=Vector2.one;rect.anchoredPosition=new Vector2(-8,-8);
            }
            Current=resource;_panel.SetActive(true);_panel.transform.SetAsLastSibling();Refresh();Layout();
        }
        private static void Place(RectTransform rect,float left,float top,float height,float right)
        {rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,-top-height);rect.offsetMax=new Vector2(-right,-top);}
        private void Layout()
        {
            var rect=_panel.GetComponent<RectTransform>();var canvas=_panel.GetComponentInParent<Canvas>();var anchor=new Vector2(0,1);var point=FrameRateDisplay.ReserveTop(cornerOffset,canvas);var size=new Vector2(Mathf.Min(panelSize.x,((RectTransform)rect.parent).rect.width-cornerOffset.x-14),panelSize.y);
            // ★ 修复（防Bug回归）：稳定的只读面板不逐帧标脏Canvas。
            if(rect.anchorMin!=anchor||rect.anchorMax!=anchor||rect.pivot!=anchor){rect.anchorMin=rect.anchorMax=rect.pivot=anchor;}if(rect.anchoredPosition!=point)rect.anchoredPosition=point;if(rect.sizeDelta!=size)rect.sizeDelta=size;
        }
        public void Refresh()
        {
            if(Current==null)return;var state=Current.State;bool corpse=state.kind==ColonyResourceKind.Corpse;
            _title.text=corpse?"尸体 · 已死亡":state.kind==ColonyResourceKind.Nursery?"王后及幼体补给":state.kind==ColonyResourceKind.Supply?"成年种群补给":"能量块";
            var queen=AntColonyMember.All.FirstOrDefault(m=>m.Role==AntRole.Queen&&m.GroupId==state.group);
            string group=queen!=null?queen.Entity.DisplayName:"原种群已不存在",claim=string.IsNullOrEmpty(state.claim)?"等待工匠搬运":"已由工匠领取";
            _body.text=corpse?$"状态：已死亡，可搬运资源\n生前名称：{(string.IsNullOrEmpty(state.sourceName)?"旧存档未记录":state.sourceName)}\n生前体型：{(state.sourceSize>0?state.sourceSize.ToString("F2"):"旧存档未记录")}\n剩余营养：{state.energy:F1}\n归属王后：{group}\n搬运状态：{claim}":$"类型：{_title.text}\n剩余能量：{state.energy:F1}\n归属王后：{group}\n可由符合条件的个体自动吸收";
            _next=Time.unscaledTime+refreshInterval;
        }
        private void LateUpdate()
        {
            if(_panel==null||!_panel.activeSelf)return;
            if(!WorldRuntimeGate.Ready||Current==null||!Current.isActiveAndEnabled||Current.State.energy<=0){Hide();return;}
            Layout();if(Time.unscaledTime>=_next)Refresh();if(!GameInputGate.Blocked&&Input.GetKeyDown(KeyCode.Escape))Hide();
        }
        public void Hide(){if(_panel!=null)_panel.SetActive(false);Current=null;}
        private void OnDestroy(){if(_panel!=null)Destroy(_panel);if(Instance==this)Instance=null;}
    }
}
