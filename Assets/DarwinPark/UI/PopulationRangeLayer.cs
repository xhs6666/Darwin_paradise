using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 修复（防Bug回归）：向地表传递活动圆数据，以并集填色或外沿显示，避免每个圆各画一圈内部交叉线。
    public sealed class PopulationRangeLayer : MonoBehaviour
    {
        private static PopulationRangeLayer _instance;
        private bool _visible;private float _next;private Texture2D _data;
        public static bool Visible=>_instance!=null&&_instance._visible;
        public static bool Outline {get;private set;}
        public static int ActiveCircleCount {get;private set;}
        public static void SetVisible(bool value)
        {
            if(_instance==null&&value)_instance=new GameObject("种群范围图层").AddComponent<PopulationRangeLayer>();
            if(_instance==null)return;_instance._visible=value;_instance._next=0;
            if(!value)_instance.Apply(false);else _instance.RefreshNow();
        }
        public static void ToggleStyle(){Outline=!Outline;if(_instance!=null){_instance._next=0;_instance.RefreshNow();}}
        public static void ClearWorld(){if(_instance==null)return;SetVisible(false);ActiveCircleCount=0;if(_instance._data!=null)Destroy(_instance._data);_instance._data=null;}
        private void OnDestroy(){Apply(false);if(_data!=null)Destroy(_data);if(_instance==this)_instance=null;}
        private void Update()
        {
            if(!_visible)return;if(!WorldRuntimeGate.Ready){Apply(false);return;}
            if(Time.unscaledTime>=_next)RefreshNow();
        }
        public void RefreshNow()
        {
            _next=Time.unscaledTime+AntColonySettings.Current.scanInterval;
            var planet=EcologyQueries.Planet;if(!_visible||!WorldRuntimeGate.Ready||planet==null){Apply(false);return;}
            var pixels=new List<Color>();int index=0;float radius=Mathf.Max(planet.radius,planet.WaterRadius);
            var groups=EntityRegistry.All.Select(e=>e.GetComponent<PopulationBehavior>()).Where(p=>p!=null&&p.Active&&!string.IsNullOrEmpty(p.GroupId)).GroupBy(p=>p.GroupId).OrderBy(g=>g.Key);
            foreach(var group in groups)
            {
                if(!PopulationArchive.IsVisible(group.Key))continue;
                Color tint=ColonyAppearance.ColorFor(group.Key);
                foreach(var circle in ColonyActivityRange.Circles(group.First()))
                {
                    var direction=planet.transform.InverseTransformPoint(circle.Center).normalized;
                    // ★ 适配：预存半径余弦，逐像素判断不用反三角函数，避免范围填色拖慢渲染。
                    pixels.Add(new Color(direction.x,direction.y,direction.z,Mathf.Cos(circle.Radius/radius)));pixels.Add(new Color(tint.r,tint.g,tint.b,index));
                }
                index++;
            }
            ActiveCircleCount=pixels.Count/2;int height=Mathf.NextPowerOfTwo(Mathf.Max(1,ActiveCircleCount));
            if(_data==null||_data.height!=height){if(_data!=null)Destroy(_data);_data=new Texture2D(2,height,TextureFormat.RGBAFloat,false,true){name="种群活动区数据",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};}
            while(pixels.Count<height*2)pixels.Add(Color.clear);_data.SetPixels(pixels.ToArray());_data.Apply(false,false);Apply(true);
        }
        private void Apply(bool show)
        {
            var planet=EcologyQueries.Planet;if(planet==null)return;
            foreach(var renderer in planet.GetComponentsInChildren<MeshRenderer>())
            {
            if(renderer.gameObject!=planet.gameObject&&(renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name!="DarwinPark/PlanetOcean"))continue;
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            block.SetFloat("_PopulationCount",show?ActiveCircleCount:0);if(_data!=null)block.SetTexture("_PopulationData",_data);
            float radius=Mathf.Max(planet.radius,planet.WaterRadius);block.SetVector("_PopulationStyle",new Vector4(show?(Outline?2:1):0,GameplayTuning.Current.populationFillOpacity,AntColonySettings.Current.overlayWidth/radius,0));renderer.SetPropertyBlock(block);
            }
        }
    }
}
