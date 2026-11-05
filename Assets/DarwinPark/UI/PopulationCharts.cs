using System;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.UI
{
    public sealed class PopulationChartValue {public string Label;public int Count;public Color Color;}
    // ★ 新增：基因按实体去重；扇形图是携带人次构成，个体携带率单独列出，避免把多基因比例误当互斥分类。
    public static class PopulationDistributions
    {
        public static List<PopulationChartValue> Genes(string id,bool expressed)
        {
            var record=PopulationArchive.Find(id);var counts=new Dictionary<string,int>();var members=PopulationArchive.Members(id);
            if(members.Count>0)foreach(var entity in members)
            {
                var ids=expressed?entity.Genes.Evaluation.Genes.Where(g=>g.IsActive).Select(g=>g.Definition.Id):entity.Genes.Genome.Genes.Select(g=>g.DefinitionId);
                foreach(string gene in ids.Distinct()){counts.TryGetValue(gene,out int count);counts[gene]=count+1;}
            }
            else if(record!=null)foreach(var gene in record.genes)counts[gene.id]=expressed?gene.expressed:gene.carried;
            return counts.Where(p=>p.Value>0).OrderByDescending(p=>p.Value).ThenBy(p=>p.Key).Select(p=>new PopulationChartValue{Label=Genetics.GeneDatabase.Get(p.Key)?.DisplayName??p.Key,Count=p.Value,Color=ColonyAppearance.DefaultColorFor(p.Key)}).ToList();
        }
        public static List<PopulationChartValue> Ages(string id)
        {
            var record=PopulationArchive.Find(id);var members=PopulationArchive.Members(id);int infants,adults,elders;
            if(members.Count>0){infants=members.Count(e=>e.Genes.CurrentAge==AgeState.Infant);adults=members.Count(e=>e.Genes.CurrentAge==AgeState.Adult);elders=members.Count-infants-adults;}
            else{infants=record?.infants??0;adults=record?.adults??0;elders=record?.elders??0;}
            return new List<PopulationChartValue>{new PopulationChartValue{Label="幼年",Count=infants,Color=new Color(.3f,.85f,.6f)},new PopulationChartValue{Label="成年",Count=adults,Color=new Color(.3f,.65f,1)},new PopulationChartValue{Label="老年",Count=elders,Color=new Color(1,.65f,.3f)}};
        }
    }
    // ★ 新增：图表是游戏原生UI，按数据签名更新扇形贴图；关闭窗口后不继续生成图形。
    public sealed class PopulationCharts:IDisposable
    {
        private Texture2D _pie;private string _signature;
        public void Draw(Rect area,IReadOnlyList<PopulationChartValue> values,bool pie,float labelWidth=140)
        {
            if(values.Sum(v=>v.Count)==0){GUI.Label(area,"暂无可统计数据");return;}
            if(pie)
            {
                string signature=string.Join("|",values.Select(v=>v.Label+":"+v.Count+":"+v.Color));
                if(_pie==null||_signature!=signature){BuildPie(values);_signature=signature;}
                float side=Mathf.Min(area.width,area.height);GUI.DrawTexture(new Rect(area.x+(area.width-side)/2,area.y,side,side),_pie,ScaleMode.ScaleToFit,true);return;
            }
            // ★ 适配：发展历史改用年月日后需更宽的标签区域，避免日期覆盖柱形。
            labelWidth=Mathf.Min(labelWidth,Mathf.Max(0,area.width-100));
            float maximum=Mathf.Max(1,values.Max(v=>v.Count)),row=Mathf.Max(22,Mathf.Min(32,area.height/values.Count));
            for(int i=0;i<values.Count;i++)
            {
                var value=values[i];float y=area.y+i*row;GUI.Label(new Rect(area.x,y,labelWidth,row),value.Label);
                var old=GUI.color;GUI.color=value.Color;GUI.DrawTexture(new Rect(area.x+labelWidth+5,y+5,Mathf.Max(1,(area.width-labelWidth-65)*value.Count/maximum),row-10),Texture2D.whiteTexture);GUI.color=old;
                GUI.Label(new Rect(area.xMax-54,y,54,row),value.Count.ToString());
            }
        }
        private void BuildPie(IReadOnlyList<PopulationChartValue> values)
        {
            const int size=256;var pixels=new Color[size*size];float sum=values.Sum(v=>v.Count);var cumulative=new List<float>();float total=0;
            foreach(var value in values){total+=value.Count/sum;cumulative.Add(total);}
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Vector2 p=new Vector2(x+.5f-size*.5f,y+.5f-size*.5f);float distance=p.magnitude;if(distance>size*.5f)continue;
                float angle=Mathf.Repeat(Mathf.Atan2(p.y,p.x)/(2*Mathf.PI)+.25f,1);int slice=0;while(slice<cumulative.Count-1&&angle>cumulative[slice])slice++;
                var color=values[slice].Color;color.a=Mathf.Clamp01(size*.5f-distance);pixels[y*size+x]=color;
            }
            if(_pie==null)_pie=new Texture2D(size,size,TextureFormat.RGBA32,false){name="种群分布图",filterMode=FilterMode.Bilinear};_pie.SetPixels(pixels);_pie.Apply();
        }
        public void Dispose(){if(_pie!=null)UnityEngine.Object.Destroy(_pie);_pie=null;}
    }
}
