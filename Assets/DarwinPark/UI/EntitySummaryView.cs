using System;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：独立的只读摘要视图；读真实数值及上限，不影响实体属性和原信息区布局。
    public static class EntitySummaryView
    {
        private static Text Label(Transform parent,string name,string text,float y,int size,Color color)
        {
            var label=UIFactory.NewText(name,parent,text,size);var rt=label.rectTransform;
            rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);
            rt.anchoredPosition=new Vector2(0,-y);rt.sizeDelta=new Vector2(0,size+6);label.color=color;
            return label;
        }
        public static Action Build(Transform parent,DarwinEntity entity)
        {
            Label(parent,"Heading","实体信息",0,14,UIVisualTheme.Muted);
            var fields=UIVisualTheme.Data.summaryStats;var values=new Text[fields.Length];var fills=new RectTransform[fields.Length];
            for(int i=0;i<fields.Length;i++)
            {
                var column=UIFactory.NewRect("Stat_"+fields[i].id,parent);var rect=column.GetComponent<RectTransform>();
                rect.anchorMin=new Vector2((float)i/fields.Length,1);rect.anchorMax=new Vector2((float)(i+1)/fields.Length,1);
                rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(0,-86);rect.offsetMax=new Vector2(-12,-28);
                Label(column.transform,"Label",fields[i].label,0,13,UIVisualTheme.Muted);
                values[i]=Label(column.transform,"Value","—",19,22,UIVisualTheme.Text);
                if(!fields[i].bar)continue;
                var track=UIFactory.NewRect("Track",column.transform,typeof(Image));var tr=track.GetComponent<RectTransform>();
                tr.anchorMin=new Vector2(0,0);tr.anchorMax=new Vector2(1,0);tr.pivot=new Vector2(.5f,0);tr.sizeDelta=new Vector2(0,4);
                UIVisualTheme.Paint(track.GetComponent<Image>(),"track");track.GetComponent<Image>().raycastTarget=false;
                var fill=UIFactory.NewRect("Fill",track.transform,typeof(Image));UIFactory.Stretch(fill.GetComponent<RectTransform>());
                UIVisualTheme.Paint(fill.GetComponent<Image>(),"fill");fill.GetComponent<Image>().raycastTarget=false;fills[i]=fill.GetComponent<RectTransform>();
            }
            var stage=Label(parent,"Stage","",96,14,UIVisualTheme.Text);
            var activity=Label(parent,"Activity","",120,14,UIVisualTheme.Text);
            var climate=Label(parent,"Climate","",146,13,UIVisualTheme.Muted);
            var environment=Label(parent,"Environment","",170,12,UIVisualTheme.Muted);
            return ()=>
            {
                if(entity==null)return;
                var stats=entity.GetComponent<EntityStats>();
                var snapshot=stats!=null?stats.CaptureValues():null;
                for(int i=0;i<fields.Length;i++)
                {
                    var data=snapshot?.FirstOrDefault(v=>v.id==fields[i].id);
                    values[i].text=data!=null?data.value.ToString(fields[i].format):"—";
                    if(fills[i]!=null)fills[i].anchorMax=new Vector2(data!=null&&data.max>0?Mathf.Clamp01(data.value/data.max):0,1);
                }
                string age=entity.Genes.CurrentAge==AgeState.Adult?"成年":entity.Genes.CurrentAge==AgeState.Elder?"老年":"幼年";
                string accent=ColorUtility.ToHtmlStringRGB(UIVisualTheme.Accent);
                // ★ 修复（防Bug回归）：分工附属状态可能较长，独占一行，不能挤占生命阶段和气候信息。
                stage.text="生命阶段  <color=#"+accent+">"+age+"</color>";
                activity.text="状态  <color=#"+accent+">"+EntityStatePresentation.Current(entity)+"</color>";
                if(entity.Genes.IsGestating)activity.text+=" · 孕育中";
                if(PlanetClimateSystem.Instance!=null)
                {var sample=PlanetClimateSystem.Instance.GetClimateAt(entity.transform.position);climate.text=$"温度  {sample.Temperature:F1} °C     湿度  {sample.Humidity:F0}%";}
                var hazard=entity.GetComponent<EntityEnvironmentHazard>();environment.text=hazard!=null?hazard.GetStatusLine():"";
            };
        }
    }
}
