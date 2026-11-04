using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 修复（防Bug回归）：第二页展示完整种群档案入口，不再用单个种群下拉框冒充全部列表。
    public sealed class MapPopulationListUI:MonoBehaviour
    {
        public float cardWidth=260,cardHeight=164,leftOffset=700,refreshInterval=.5f;
        public int GroupCount=>_groups.Count;
        public int LivingCount=>_groups.Count(g=>g.Count>0);
        public int MemberCount=>_groups.Sum(g=>g.Count);
        public RectTransform Card=>(RectTransform)transform;
        public sealed class GroupRow {public string Id,Name,Mode;public int Count;public DarwinEntity Leader;}
        private readonly List<GroupRow> _groups=new List<GroupRow>();
        public IReadOnlyList<GroupRow> Groups=>_groups;
        private Text _count,_members;private float _next;
        private void Start()
        {
            UIVisualTheme.Paint(GetComponent<Image>());Label("Title","种群档案",4,18,13).color=UIVisualTheme.Muted;
            _count=Label("Count","",25,25,20);_count.color=UIVisualTheme.Accent;_members=Label("Members","",53,20,12);
            Label("Description","基因 · 年龄 · 历史 · 衍生种群",78,22,12);
            var list=UIFactory.NewButton("AllPopulations",transform,"全部种群列表",OpenList,new Vector2(150,28));Place(list.GetComponent<RectTransform>(),128,28,0,.66f);
            var settings=UIFactory.NewButton("PopulationSettings",transform,"显示配置",()=>PopulationWorkbench.Ensure().Open(null,6),new Vector2(80,28));Place(settings.GetComponent<RectTransform>(),128,28,.67f,1);
            foreach(var button in new[]{list,settings})button.GetComponentInChildren<Text>().fontSize=12;RefreshNow();
        }
        private Text Label(string name,string value,float y,float height,int font){var label=UIFactory.NewText(name,transform,value,font);Place(label.rectTransform,y,height);return label;}
        private static void Place(RectTransform rt,float y,float height,float left=0,float right=1){rt.anchorMin=new Vector2(left,1);rt.anchorMax=new Vector2(right,1);rt.pivot=new Vector2(.5f,1);rt.offsetMin=new Vector2(10,-y-height);rt.offsetMax=new Vector2(-10,-y);}
        public void RefreshNow()
        {
            _next=Time.unscaledTime+refreshInterval;if(WorldRuntimeGate.Ready&&!WorldAuthority.IsReplica)PopulationArchive.Observe();_groups.Clear();
            // ★ 适配：历史种群再多也只扫描一次存活实体，避免每个已灭绝档案都重新遍历全地图。
            var leaders=EntityRegistry.All.Where(EcologyQueries.Alive).Select(e=>e.GetComponent<PopulationBehavior>()).Where(p=>p!=null&&p.Active&&!string.IsNullOrEmpty(p.GroupId)).GroupBy(p=>p.GroupId).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var record in PopulationArchive.All.OrderByDescending(r=>r.current).ThenBy(r=>r.firstSeen))
            {
                leaders.TryGetValue(record.id,out var pop);
                _groups.Add(new GroupRow{Id=record.id,Name=record.name,Mode=record.mode,Count=record.current,Leader=pop!=null?(pop.Leader??pop).GetComponent<DarwinEntity>():null});
            }
            if(_count!=null){_count.text="存活种群  "+LivingCount;_members.text="成员 "+MemberCount+" · 历史种群 "+GroupCount;}
        }
        public void OpenList()=>PopulationWorkbench.Ensure().Open();
        public bool Locate(int index)
        {
            if(index<0||index>=_groups.Count||!EcologyQueries.Alive(_groups[index].Leader)||GameInputGate.Blocked)return false;
            EntityViewController.Instance?.ExitView();Camera.main?.GetComponent<OrbitCamera>()?.FocusOn(_groups[index].Leader.transform.position);return true;
        }
        private void LateUpdate()
        {
            var parent=(RectTransform)transform.parent;var anchor=new Vector2(0,1);if(Card.anchorMin!=anchor||Card.anchorMax!=anchor||Card.pivot!=anchor)Card.anchorMin=Card.anchorMax=Card.pivot=anchor;
            var point=new Vector2(Mathf.Min(leftOffset,Mathf.Max(12,parent.rect.width-cardWidth-12)),0);var size=new Vector2(cardWidth,cardHeight);if(Card.anchoredPosition!=point)Card.anchoredPosition=point;if(Card.sizeDelta!=size)Card.sizeDelta=size;
            if(Time.unscaledTime>=_next)RefreshNow();
        }
    }
}
