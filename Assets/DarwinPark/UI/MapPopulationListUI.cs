using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：种群统计卡与前两张卡共用尺寸，列表只读取存活成员，出生/死亡/灭绝后自动刷新。
    [DefaultExecutionOrder(100)] public sealed class MapPopulationListUI:MonoBehaviour
    {
        public float cardWidth=260,cardHeight=164,gap=10,refreshInterval=.5f;
        public int rowsPerPage=8;
        public int GroupCount=>_groups.Count;
        public int MemberCount {get;private set;}
        public RectTransform Card {get;private set;}
        public sealed class GroupRow {public string Id,Name,Mode;public int Count;public DarwinEntity Leader;}
        public IReadOnlyList<GroupRow> Groups=>_groups;
        private readonly List<GroupRow> _groups=new List<GroupRow>();
        private MapEntityGeneFilterUI _filter;private Text _count,_members;private Dropdown _select;
        private RectTransform _window;private readonly List<Button> _rows=new List<Button>();private Text _pageText;
        private Button _previous,_next;private float _refresh;private int _page;
        private Button _launch;private string _signature;
        private void Start()
        {
            _filter=GetComponent<MapEntityGeneFilterUI>();Card=UIFactory.NewRect("PopulationList",transform.parent,typeof(Image)).GetComponent<RectTransform>();UIVisualTheme.Paint(Card.GetComponent<Image>());
            _launch=UIFactory.NewButton("OpenPopulationList",transform,"种群",OpenList,new Vector2(46,22));var launchRect=_launch.GetComponent<RectTransform>();launchRect.anchorMin=launchRect.anchorMax=launchRect.pivot=Vector2.one;launchRect.anchoredPosition=new Vector2(-89,-3);_launch.GetComponentInChildren<Text>().fontSize=12;
            Label("Heading","种群列表",4,18,13).color=UIVisualTheme.Muted;_count=Label("GroupCount","种群 0",23,25,20);_count.color=UIVisualTheme.Accent;
            _members=Label("Members","成员 0",51,20,12);
            var dropdown=DefaultControls.CreateDropdown(new DefaultControls.Resources());dropdown.name="ChoosePopulation";dropdown.transform.SetParent(Card,false);_select=dropdown.GetComponent<Dropdown>();Place(dropdown.GetComponent<RectTransform>(),77,26);
            foreach(var text in dropdown.GetComponentsInChildren<Text>(true)){text.font=UIFactory.DefaultFont;text.fontSize=12;text.color=UIVisualTheme.Text;}
            UIVisualTheme.Paint(dropdown.GetComponent<Image>(),"field");RuntimeDropdownSkin.Apply(_select);
            // ★ 修复（防Bug回归）：DefaultControls无箭头资源时会显示白块，使用现有主题箭头。
            var arrow=dropdown.transform.Find("Arrow").GetComponent<Image>();arrow.sprite=UIIconRegistry.Get("ui_icon_next");arrow.color=UIVisualTheme.Accent;arrow.preserveAspect=true;arrow.rectTransform.sizeDelta=new Vector2(14,14);arrow.rectTransform.localRotation=Quaternion.Euler(0,0,-90);
            var locate=UIFactory.NewButton("LocatePopulation",Card,"定位种群",()=>Locate(_select.value),new Vector2(100,26));Place(locate.GetComponent<RectTransform>(),129,26,0,.48f);
            var list=UIFactory.NewButton("ShowPopulations",Card,"查看列表",OpenList,new Vector2(100,26));Place(list.GetComponent<RectTransform>(),129,26,.5f,1);
            foreach(var button in new[]{locate,list})button.GetComponentInChildren<Text>().fontSize=12;
            RefreshNow();
        }
        private Text Label(string name,string text,float y,float h,int size){var value=UIFactory.NewText(name,Card,text,size);Place(value.rectTransform,y,h);return value;}
        private static void Place(RectTransform rt,float y,float h,float left=0,float right=1)
        {rt.anchorMin=new Vector2(left,1);rt.anchorMax=new Vector2(right,1);rt.pivot=new Vector2(.5f,1);rt.offsetMin=new Vector2(10,-y-h);rt.offsetMax=new Vector2(-10,-y);}
        public void RefreshNow()
        {
            _refresh=Time.unscaledTime+refreshInterval;string selected=_select!=null&&_select.value<_groups.Count?_groups[_select.value].Id:null;
            _groups.Clear();MemberCount=0;
            if(WorldRuntimeGate.Ready)foreach(var group in EntityRegistry.All.Where(EcologyQueries.Alive).Select(e=>e.GetComponent<PopulationBehavior>()).Where(p=>p!=null&&p.Active&&!string.IsNullOrEmpty(p.GroupId)).GroupBy(p=>p.GroupId).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key))
            {
                var first=group.First();var leader=(first.Leader??first).GetComponent<DarwinEntity>();int count=group.Count();MemberCount+=count;
                _groups.Add(new GroupRow{Id=group.Key,Name=leader.DisplayName,Leader=leader,Count=count,Mode=first.Mode==EcologySettings.Load().antState?"蚁群":"狼群"});
            }
            if(_select==null)return;
            _count.text="种群  "+GroupCount;_members.text="成员 "+MemberCount+"  ·  蚁群 "+_groups.Count(g=>g.Mode=="蚁群")+"  狼群 "+_groups.Count(g=>g.Mode=="狼群");
            string signature=string.Join("|",_groups.Select(g=>g.Id+g.Name+g.Count));
            if(signature!=_signature){_signature=signature;_select.Hide();_select.ClearOptions();_select.AddOptions(_groups.Count==0?new List<string>{"暂无存活种群"}:_groups.Select(g=>g.Name+" · "+g.Count+"个体").ToList());_select.SetValueWithoutNotify(Mathf.Max(0,_groups.FindIndex(g=>g.Id==selected)));}_select.interactable=GroupCount>0;
            RefreshRows();
        }
        public bool Locate(int index)
        {
            if(index<0||index>=_groups.Count||!EcologyQueries.Alive(_groups[index].Leader)||GameInputGate.Blocked)return false;
            EntityViewController.Instance?.ExitView();Camera.main?.GetComponent<OrbitCamera>()?.FocusOn(_groups[index].Leader.transform.position);return true;
        }
        public void OpenList()
        {
            if(_window==null)
            {
                var root=ServiceLocator.Get<UIRoot>().MainCanvas.transform;_window=UIFactory.NewRect("PopulationListWindow",root,typeof(Image)).GetComponent<RectTransform>();UIVisualTheme.Paint(_window.GetComponent<Image>());
                _window.anchorMin=_window.anchorMax=_window.pivot=new Vector2(.5f,.5f);_window.sizeDelta=new Vector2(320,330);
                var title=UIFactory.NewText("Heading",_window,"全部种群",17);Place(title.rectTransform,8,26);
                var close=UIFactory.NewButton("Close",_window,"关闭",()=>_window.gameObject.SetActive(false),new Vector2(60,24));Place(close.GetComponent<RectTransform>(),8,24,.75f,1);
                close.GetComponentInChildren<Text>().fontSize=12;
                for(int i=0;i<rowsPerPage;i++){int row=i;var button=UIFactory.NewButton("GroupRow"+i,_window,"种群",()=>Locate(_page*rowsPerPage+row),new Vector2(280,28));Place(button.GetComponent<RectTransform>(),44+i*30,27);button.GetComponentInChildren<Text>().fontSize=13;_rows.Add(button);}
                _previous=UIFactory.NewButton("Previous",_window,"上一页",()=>ChangePage(-1),new Vector2(80,26));Place(_previous.GetComponent<RectTransform>(),296,26,0,.3f);
                _next=UIFactory.NewButton("Next",_window,"下一页",()=>ChangePage(1),new Vector2(80,26));Place(_next.GetComponent<RectTransform>(),296,26,.7f,1);
                _previous.GetComponentInChildren<Text>().fontSize=_next.GetComponentInChildren<Text>().fontSize=12;
                _pageText=UIFactory.NewText("Page",_window,"",12,TextAnchor.MiddleCenter);Place(_pageText.rectTransform,296,26,.3f,.7f);
            }
            _window.gameObject.SetActive(true);_window.SetAsLastSibling();RefreshNow();
        }
        private void ChangePage(int delta){_page+=delta;RefreshRows();}
        private void RefreshRows()
        {
            if(_window==null)return;int pages=Mathf.Max(1,Mathf.CeilToInt(GroupCount/(float)rowsPerPage));_page=Mathf.Clamp(_page,0,pages-1);
            float height=Mathf.Clamp(88+Mathf.Min(rowsPerPage,GroupCount)*30,150,330);var size=new Vector2(320,height);if(_window.sizeDelta!=size)_window.sizeDelta=size;
            Place(_previous.GetComponent<RectTransform>(),height-34,26,0,.3f);Place(_pageText.rectTransform,height-34,26,.3f,.7f);Place(_next.GetComponent<RectTransform>(),height-34,26,.7f,1);
            for(int i=0;i<_rows.Count;i++){int index=_page*rowsPerPage+i;_rows[i].gameObject.SetActive(index<GroupCount);if(index<GroupCount){var row=_groups[index];_rows[i].GetComponentInChildren<Text>().text=row.Mode+" · "+row.Name+"  "+row.Count+"个体";}}
            _pageText.text=GroupCount==0?"暂无种群":(_page+1)+" / "+pages;_previous.interactable=_page>0;_next.interactable=_page+1<pages;
        }
        private void LateUpdate()
        {
            if(Card==null||_filter?.Card==null)return;var parent=(RectTransform)Card.parent;var source=_filter.Card;float x=source.anchoredPosition.x+source.rect.width+gap;
            bool wide=_filter.WideLayout&&parent.rect.width-x>=cardWidth+12;Card.gameObject.SetActive(wide);
            _launch.gameObject.SetActive(!wide);
            // ★ 修复（防Bug回归）：稳定尺寸不反复标脏整条Canvas，新增卡片不能引入逐帧布局重建。
            var anchor=new Vector2(0,1);if(Card.anchorMin!=anchor||Card.anchorMax!=anchor||Card.pivot!=anchor)Card.anchorMin=Card.anchorMax=Card.pivot=anchor;
            var position=new Vector2(x,0);var size=new Vector2(cardWidth,cardHeight);if(Card.anchoredPosition!=position)Card.anchoredPosition=position;if(Card.sizeDelta!=size)Card.sizeDelta=size;
            if(Time.unscaledTime>=_refresh)RefreshNow();
        }
        private void OnDisable(){if(Card!=null)Card.gameObject.SetActive(false);if(_window!=null)_window.gameObject.SetActive(false);}
        private void OnDestroy(){if(Card!=null)Destroy(Card.gameObject);if(_window!=null)Destroy(_window.gameObject);}
    }
}
