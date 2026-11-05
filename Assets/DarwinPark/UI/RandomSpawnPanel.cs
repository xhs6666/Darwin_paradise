using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Genetics;
using DarwinPark.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：第二页的全星球快速生成栏，与原有默认/模板的定点放置操作并列。
    public sealed class RandomSpawnPanel : MonoBehaviour
    {
        public int defaultCount=10;
        public float leftOffset=440,preferredWidth=240;
        private RandomPlanetSpawner _spawner;private Button _source;private InputField _count;private Button _range,_generate,_cancel;private Text _status;
        private bool _landOnly=true;
        public bool LandOnly=>_landOnly;
        public RandomSpawnRecipe Recipe {get;private set;}
        public static void Attach(Transform parent)
        {
            if(parent.Find("RandomSpawn")!=null)return;
            var go=UIFactory.NewRect("RandomSpawn",parent,typeof(Image));UIVisualTheme.Paint(go.GetComponent<Image>());go.AddComponent<RandomSpawnPanel>();
        }
        private void Start()
        {
            _spawner=RandomPlanetSpawner.Ensure(ServiceLocator.Get<SimulationClock>().gameObject);Recipe=new RandomSpawnRecipe{template=RandomSpawnRecipe.DefaultGenome()};Build();RefreshSources();EntityTemplateManager.OnTemplatesChanged+=RefreshSources;
        }
        private void OnDestroy(){EntityTemplateManager.OnTemplatesChanged-=RefreshSources;}
        private void Build()
        {
            var title=UIFactory.NewText("Title",transform,"随机生成 · 全星球",15);Place(title.rectTransform,0,-6,1,24);
            // ★ 新增：选择实体后打开模板/基因条件两列界面，两种来源互斥，不能把模板偷偷改成受约束随机基因。
            _source=UIFactory.NewButton("Source",transform,"选择实体",()=>RandomSpawnWorkbench.Ensure().Open(this),new Vector2(220,28));Place(_source.GetComponent<RectTransform>(),0,-32,1,28);_source.GetComponentInChildren<Text>().fontSize=12;
            // ★ 适配：栏宽减半后给双字标签保留完整字宽，输入框和范围按钮继续各占独立区域。
            var label=UIFactory.NewText("QuantityLabel",transform,"数量",13);Place(label.rectTransform,0,-66,.18f,28);
            var field=UIFactory.NewRect("Quantity",transform,typeof(Image),typeof(InputField));_count=field.GetComponent<InputField>();Place(field.GetComponent<RectTransform>(),.18f,-66,.43f,28);_count.contentType=InputField.ContentType.IntegerNumber;
            var value=UIFactory.NewText("Text",field.transform,defaultCount.ToString(),14,TextAnchor.MiddleCenter);UIFactory.Stretch(value.rectTransform);_count.textComponent=value;_count.text=defaultCount.ToString();
            _range=UIFactory.NewButton("Range",transform,"范围：仅陆地",()=>SetLandOnly(!_landOnly),new Vector2(160,28));Place(_range.GetComponent<RectTransform>(),.44f,-66,1,28);
            _generate=UIFactory.NewButton("Generate",transform,"随机生成",Generate,new Vector2(170,30));Place(_generate.GetComponent<RectTransform>(),0,-100,.65f,30);
            _cancel=UIFactory.NewButton("Cancel",transform,"取消本批",()=>_spawner.Cancel(),new Vector2(90,30));Place(_cancel.GetComponent<RectTransform>(),.68f,-100,1,30);
            foreach(var button in new[]{_range,_generate,_cancel})button.GetComponentInChildren<Text>().fontSize=13;
            var maintain=UIFactory.NewButton("Maintain",transform,"维持比例…",()=>RandomSpawnWorkbench.Ensure().Open(this,true),new Vector2(110,24));Place(maintain.GetComponent<RectTransform>(),0,-132,.48f,24);maintain.GetComponentInChildren<Text>().fontSize=12;
            _status=UIFactory.NewText("Status",transform,"",11);Place(_status.rectTransform,.48f,-132,1,24);_status.resizeTextForBestFit=true;_status.resizeTextMinSize=9;_status.resizeTextMaxSize=11;
        }
        private static void Place(RectTransform rt,float left,float top,float right,float height)
        {rt.anchorMin=new Vector2(left,1);rt.anchorMax=new Vector2(right,1);rt.pivot=new Vector2(.5f,1);rt.offsetMin=new Vector2(8,top-height);rt.offsetMax=new Vector2(-8,top);}
        private void RefreshSources()
        {
            if(_source==null)return;_source.GetComponentInChildren<Text>().text="选择实体："+(Recipe?.method==RandomSpawnMethod.Genes?"基因条件随机":Recipe?.name??"默认实体");
        }
        public void SetRecipe(RandomSpawnRecipe recipe){recipe.Validate(GeneDatabase.All.Select(d=>d.Id).ToArray());Recipe=JsonUtility.FromJson<RandomSpawnRecipe>(JsonUtility.ToJson(recipe));RefreshSources();}
        // ★ 适配：来源窗口内的范围选择应用后同步到底栏，不能显示全部地表却仍按仅陆地生成。
        public void SetLandOnly(bool value){_landOnly=value;if(_range!=null)_range.GetComponentInChildren<Text>().text=value?"范围：仅陆地":"范围：全部地表";}
        public void Generate()
        {
            if(!WorldAuthority.CanEdit)return;
            if(_spawner==null)return;int count=int.TryParse(_count.text,out var parsed)?Mathf.Clamp(parsed,1,_spawner.maxCount):defaultCount;_count.text=count.ToString();_spawner.StartRecipe(Recipe,count,_landOnly);
        }
        private void Update()
        {
            var rect=(RectTransform)transform;float available=((RectTransform)transform.parent).rect.width-leftOffset-12;
            rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(0,1);rect.offsetMin=new Vector2(leftOffset,0);rect.offsetMax=new Vector2(leftOffset+Mathf.Min(preferredWidth,Mathf.Max(240,available)),0);
            if(_spawner==null)return;_status.text=_spawner.Status;_generate.interactable=WorldAuthority.CanEdit&&!_spawner.IsBusy;_source.interactable=_count.interactable=_range.interactable=WorldAuthority.CanEdit&&!_spawner.IsBusy;_cancel.interactable=WorldAuthority.CanEdit&&_spawner.IsBusy;
        }
    }
}
