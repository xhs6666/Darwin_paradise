using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：尸体用共享的暖色叉号与活体区分，不增加碰撞体、灯光或逐帧Update。
    public sealed class ColonyResourcePresentation:MonoBehaviour
    {
        [Serializable] private sealed class Settings
        {public int version=0;public string corpseColor="",markerColor="";public float markerRadius=0,markerHeight=0,markerWidth=0;}
        private static Settings _settings;private static Mesh _mesh;private static Material _material;private Transform _marker;
        private static Settings Load()
        {
            if(_settings!=null)return _settings;var asset=Resources.Load<TextAsset>("Entities/resource_presentation");
            if(asset==null)throw new InvalidOperationException("缺少 Entities/resource_presentation");
            var value=JsonUtility.FromJson<Settings>(asset.text);
            if(value==null||value.version!=1||!ColorUtility.TryParseHtmlString(value.corpseColor,out _)||!ColorUtility.TryParseHtmlString(value.markerColor,out _))throw new InvalidOperationException("尸体显示配置无效");
            foreach(float number in new[]{value.markerRadius,value.markerHeight,value.markerWidth})if(!float.IsFinite(number)||number<=0)throw new InvalidOperationException("尸体标记尺寸无效");
            return _settings=value;
        }
        public void Build()
        {
            var config=Load();var renderer=GetComponent<Renderer>();var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);ColorUtility.TryParseHtmlString(config.corpseColor,out var color);block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);
            if(_mesh==null)
            {
                var vertices=new Vector3[8];var triangles=new int[12];
                for(int i=0;i<2;i++)
                {
                    var a=new Vector3(-config.markerRadius,config.markerHeight,(i==0?-1:1)*config.markerRadius);var b=new Vector3(config.markerRadius,config.markerHeight,(i==0?1:-1)*config.markerRadius);var side=Vector3.Cross(Vector3.up,b-a).normalized*(config.markerWidth*.5f);int offset=i*4,index=i*6;
                    vertices[offset]=a+side;vertices[offset+1]=a-side;vertices[offset+2]=b+side;vertices[offset+3]=b-side;
                    triangles[index]=offset;triangles[index+1]=offset+1;triangles[index+2]=offset+2;triangles[index+3]=offset+2;triangles[index+4]=offset+1;triangles[index+5]=offset+3;
                }
                _mesh=new Mesh{name="Corpse status cross"};_mesh.vertices=vertices;_mesh.triangles=triangles;_mesh.RecalculateBounds();
                var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");if(shader==null)throw new InvalidOperationException("尸体标记缺少Unlit材质");
                _material=new Material(shader){name="Corpse status marker"};ColorUtility.TryParseHtmlString(config.markerColor,out var markerColor);if(_material.HasProperty("_BaseColor"))_material.SetColor("_BaseColor",markerColor);if(_material.HasProperty("_Color"))_material.SetColor("_Color",markerColor);
            }
            var marker=new GameObject("CorpseStatusMark",typeof(MeshFilter),typeof(MeshRenderer));_marker=marker.transform;_marker.SetParent(transform,false);marker.GetComponent<MeshFilter>().sharedMesh=_mesh;var display=marker.GetComponent<MeshRenderer>();display.sharedMaterial=_material;display.shadowCastingMode=ShadowCastingMode.Off;display.receiveShadows=false;Align();
        }
        public void Align(){if(_marker!=null)_marker.rotation=Quaternion.FromToRotation(Vector3.up,EcologyQueries.Up(transform.position));}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset(){_settings=null;_mesh=null;_material=null;}
    }
}
