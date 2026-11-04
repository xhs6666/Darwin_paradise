using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：筛选列表按页复用固定按钮，不为上千个实体建立上千个UI对象；只定位或查看，不改变实体与权限。
    public sealed class EntityFilterResultsUI:MonoBehaviour
    {
        private MapEntityGeneFilterUI _owner;private RectTransform _rect;
        private Text _title,_pageLabel,_empty;private Button _previous,_next;
        private readonly List<Button> _rows=new List<Button>();
        private readonly Vector3[] _corners=new Vector3[4];private int _perPage=1;
        public int Page {get;private set;}
        public int PageCount=>Mathf.Max(1,Mathf.CeilToInt((_owner?.Matches.Count??0)/(float)_perPage));
        public int VisibleRowCount {get;private set;}
        public bool Visible=>gameObject.activeSelf;
        public static EntityFilterResultsUI Create(MapEntityGeneFilterUI owner)
        {
            if(!ServiceLocator.TryGet<UIRoot>(out var root)||root.MainCanvas==null)return null;
            var go=UIFactory.NewRect("EntityFilterResults",root.MainCanvas.transform,typeof(Image));
            var view=go.AddComponent<EntityFilterResultsUI>();view.Build(owner);return view;
        }
        private void Build(MapEntityGeneFilterUI owner)
        {
            _owner=owner;_rect=(RectTransform)transform;UIVisualTheme.Paint(GetComponent<Image>());
            _rect.anchorMin=_rect.anchorMax=Vector2.zero;_rect.pivot=Vector2.zero;
            _title=Label("Heading","筛选实体列表",8,24,15);Place(_title.rectTransform,8,24,0,.85f);
            Label("Hint","点击定位 · 右键查看属性",35,18,12).color=UIVisualTheme.Muted;
            var close=UIFactory.NewButton("Close",transform,"×",Hide,new Vector2(26,24));Place(close.GetComponent<RectTransform>(),8,24,.86f,1);
            for(int i=0;i<Mathf.Clamp(owner.resultsPerPage,1,64);i++)
            {
                // ★ 修复（防Bug回归）：工厂对空标题不创建Text，复用行必须预建文字组件再填充实体名称。
                int row=i;var button=UIFactory.NewButton("EntityRow"+i,transform,"实体",()=>LocateRow(row),new Vector2(280,owner.resultRowHeight));
                button.GetComponentInChildren<Text>().resizeTextForBestFit=true;button.GetComponentInChildren<Text>().resizeTextMinSize=10;button.GetComponentInChildren<Text>().resizeTextMaxSize=13;
                button.gameObject.AddComponent<RightClickHandler>().OnRightClick+=()=>LocateRow(row,true);_rows.Add(button);
            }
            _empty=Label("Empty","没有符合条件的实体",66,32,14);_empty.color=UIVisualTheme.Muted;
            _previous=UIFactory.NewButton("Previous",transform,"上一页",()=>ChangePage(-1),new Vector2(80,26));
            _next=UIFactory.NewButton("Next",transform,"下一页",()=>ChangePage(1),new Vector2(80,26));_pageLabel=Label("Page","",0,26,12);
            foreach(var button in new[]{_previous,_next})button.GetComponentInChildren<Text>().fontSize=12;
            Reposition();Refresh();
        }
        private Text Label(string name,string text,float y,float height,int size)
        {var result=UIFactory.NewText(name,transform,text,size);Place(result.rectTransform,y,height);return result;}
        private static void Place(RectTransform rect,float y,float height,float left=0,float right=1)
        {rect.anchorMin=new Vector2(left,1);rect.anchorMax=new Vector2(right,1);rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(10,-y-height);rect.offsetMax=new Vector2(-10,-y);}
        public void Show(){gameObject.SetActive(true);transform.SetAsLastSibling();Reposition();Refresh();}
        public void Hide()=>gameObject.SetActive(false);
        public void ChangePage(int delta){Page=Mathf.Clamp(Page+delta,0,PageCount-1);Refresh();}
        public bool LocateRow(int row,bool inspect=false)
        {
            int index=Page*_perPage+row;if(row<0||row>=VisibleRowCount||index>=_owner.Matches.Count)return false;
            return _owner.Locate(_owner.Matches[index],inspect);
        }
        public void Refresh()
        {
            if(_owner==null||_title==null)return;
            float rowHeight=Mathf.Max(20,_owner.resultRowHeight);
            _perPage=Mathf.Clamp(Mathf.FloorToInt((_rect.rect.height-92)/rowHeight),1,_rows.Count);Page=Mathf.Clamp(Page,0,PageCount-1);
            int count=_owner.Matches.Count;_title.text="筛选实体列表  "+count;VisibleRowCount=Mathf.Clamp(count-Page*_perPage,0,_perPage);
            for(int i=0;i<_rows.Count;i++)
            {
                bool visible=i<VisibleRowCount;_rows[i].gameObject.SetActive(visible);if(!visible)continue;
                var entity=_owner.Matches[Page*_perPage+i];var label=_rows[i].GetComponentInChildren<Text>();
                label.text=(Page*_perPage+i+1)+". "+(entity!=null?entity.DisplayName:"实体已移除");label.color=entity==_owner.LocatedEntity?UIVisualTheme.Accent:UIVisualTheme.Text;
                Place(_rows[i].GetComponent<RectTransform>(),60+i*rowHeight,rowHeight-3);
            }
            _empty.gameObject.SetActive(count==0);_pageLabel.text=(Page+1)+" / "+PageCount+" 页";
            float y=_rect.rect.height-34;Place(_previous.GetComponent<RectTransform>(),y,26,0,.31f);Place(_pageLabel.rectTransform,y,26,.31f,.69f);Place(_next.GetComponent<RectTransform>(),y,26,.69f,1);
            _previous.interactable=Page>0;_next.interactable=Page+1<PageCount;
        }
        private void LateUpdate(){if(_owner==null||!_owner.isActiveAndEnabled||!WorldRuntimeGate.Ready){Hide();return;}if(Reposition())Refresh();}
        private bool Reposition()
        {
            // ★ 新增：少量结果时缩短列表，避免只有一两个个体也留下整块空白。
            float height=Mathf.Min(_owner.resultWindowSize.y,Mathf.Max(_owner.minimumResultHeight,92+Mathf.Min(_owner.Matches.Count,_owner.resultsPerPage)*Mathf.Max(20,_owner.resultRowHeight)));
            var canvas=(RectTransform)transform.parent;var size=Vector2.Min(new Vector2(_owner.resultWindowSize.x,height),new Vector2(Mathf.Max(120,canvas.rect.width-24),Mathf.Max(120,canvas.rect.height-24)));
            var source=_owner.WideLayout&&_owner.Card!=null?_owner.Card:(RectTransform)_owner.transform;source.GetWorldCorners(_corners);
            var corner=(Vector2)canvas.InverseTransformPoint(_corners[1])-canvas.rect.min;
            var point=new Vector2(Mathf.Clamp(corner.x,12,Mathf.Max(12,canvas.rect.width-size.x-12)),Mathf.Clamp(corner.y+_owner.gap,12,Mathf.Max(12,canvas.rect.height-size.y-12)));
            bool changed=_rect.sizeDelta!=size;if(changed)_rect.sizeDelta=size;if(_rect.anchoredPosition!=point)_rect.anchoredPosition=point;return changed;
        }
    }
}
