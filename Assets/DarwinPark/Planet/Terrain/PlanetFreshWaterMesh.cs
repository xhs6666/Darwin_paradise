using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：河流/湖泊使用一个静态合并网格，不增加逐河流Update、灯光或碰撞体。
    public sealed class PlanetFreshWaterMesh:MonoBehaviour
    {
        private Mesh _mesh;private Material _material;private GameObject _surface;
        public float surfaceSpacing=.6f,surfaceLift=.025f;
        public int maximumCellCuts=24;
        public Color deepWaterColor=new(.025f,.16f,.24f,1);public float shelfDepth=4,waveStrength=.10f,visibility=5;
        public static void Rebuild(PlanetTerrainMeshBuilder planet)
        {var visual=planet.GetComponent<PlanetFreshWaterMesh>()??planet.gameObject.AddComponent<PlanetFreshWaterMesh>();visual.Build(planet.Data);}
        private void Build(PlanetTerrainData data)
        {
            if(_surface!=null){_surface.SetActive(false);Destroy(_surface);_surface=null;}if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);
            var hydro=data?.Hydrology;if(hydro==null)return;
            _mesh=FreshWaterSurfaceMesher.Build(data,surfaceSpacing,maximumCellCuts,surfaceLift);
            if(_mesh.vertexCount==0)return;var go=new GameObject("RiversAndLakes",typeof(MeshFilter),typeof(MeshRenderer));_surface=go;go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=_mesh;
            // ★ 修复（防Bug回归）：无光照河湖会在夜间发亮；复用已打包的海水着色器，不增加独立水面光源或绘制对象。
            var shader=Shader.Find("DarwinPark/PlanetOcean");if(shader==null){Debug.LogError("[水系] 缺少已打包的PlanetOcean Shader");return;}
            _material=new Material(shader){name="FreshWater"};_material.SetColor("_ShallowColor",hydro.Settings.waterColor);_material.SetColor("_DeepColor",deepWaterColor);_material.SetFloat("_Cull",0);_material.SetFloat("_FreshWater",1);_material.SetFloat("_ShelfDepth",shelfDepth);_material.SetFloat("_WaveStrength",waveStrength);_material.SetFloat("_OceanOptics",1);_material.SetFloat("_Visibility",visibility);ApplyLayer(0);var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=_material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        internal void ApplyLayer(float layer)
        {
            if (_material == null) return;
            // ★ 适配：浅水显示下方的河床和生物；数据图层恢复不透明并写入深度。
            bool optical = layer < .5f;
            _material.renderQueue = optical ? (int)UnityEngine.Rendering.RenderQueue.Transparent-19 :
                (int)UnityEngine.Rendering.RenderQueue.Geometry+1;
            _material.SetFloat("_WaterDepthWrite",optical?0:1);
            _material.SetFloat("_WaterDstBlend",optical?(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha:0);
            _material.SetFloat("_LayerMode",layer);
        }

        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
