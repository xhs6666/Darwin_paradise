using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：尸体保留原对象用于点选/运输/存档，仅将球体与叉号按空间格提交GPU实例批次。
    [DefaultExecutionOrder(800)]
    public sealed class CorpseInstanceRenderer : MonoBehaviour
    {
        private sealed class Appearance
        {
            public Mesh mesh; public Material material; public RenderParams parameters; public float radius;
        }
        private sealed class Part
        {
            public ColonyResource owner; public Renderer source; public Transform transform;
            public Appearance appearance; public Group group; public Matrix4x4 matrix;public Vector3 localPosition,localScale;public Quaternion localRotation; public Bounds bounds; public bool dirty=true, original, batched, active; public int index,slot=-1;
        }
        private sealed class Chunk { public readonly Matrix4x4[] matrices = new Matrix4x4[511],world=new Matrix4x4[511]; public int count; }
        private sealed class Group
        {
            public Appearance appearance;public Bounds bounds;public readonly List<Chunk> chunks=new();public readonly List<Part> members=new();public int used;
            private void Store(Part part,int limit)
            {int chunk=part.slot/limit;while(chunks.Count<=chunk)chunks.Add(new Chunk());chunks[chunk].matrices[part.slot%limit]=part.matrix;InstanceWrites++;}
            public void Attach(Part part,int limit)
            {part.group=this;part.slot=members.Count;members.Add(part);Store(part,limit);used=(members.Count+limit-1)/limit;chunks[used-1].count=(members.Count-1)%limit+1;if(members.Count==1)bounds=part.bounds;else bounds.Encapsulate(part.bounds);}
            public void Change(Part part,int limit)
            {Store(part,limit);bounds.Encapsulate(part.bounds);}
            public void Detach(Part part,int limit)
            {
                int last=members.Count-1;var moved=members[last];if(part.slot!=last){members[part.slot]=moved;moved.slot=part.slot;Store(moved,limit);}members.RemoveAt(last);
                chunks[last/limit].count--;used=(members.Count+limit-1)/limit;part.slot=-1;part.group=null;
            }
        }
        private static CorpseInstanceRenderer _instance;
        private readonly List<Part> _parts = new();
        private readonly Dictionary<ColonyResource, List<Part>> _owners = new();
        private readonly Dictionary<(Mesh, Material, Color, Color, ShadowCastingMode, bool, int), Appearance> _appearances = new();
        private readonly Dictionary<(Appearance, Vector3Int), Group> _groups = new();
        private readonly List<Group> _visible = new();
        private readonly Plane[] _planes=new Plane[6];
        private Matrix4x4 _world; private float _cellSize;private int _limit,_instanceCount;
        private MaterialPropertyBlock _block; private bool _failed;
        // ★ 新增：记录实际实例槽写入，验证搬一具尸体不会重写全世界。
        public static long InstanceWrites {get;private set;}
        public static int RegisteredParts => _instance != null ? _instance._parts.Count : 0;
        public static int LastBatches { get; private set; }
        public static int LastInstances { get; private set; }

        public static void Register(ColonyResource resource)
        {
            if (resource == null || resource.State?.kind != ColonyResourceKind.Corpse || !resource.isActiveAndEnabled) return;
            var planet = EcologyQueries.Planet;
            if (planet == null) return;
            if (_instance == null) _instance = planet.GetComponent<CorpseInstanceRenderer>() ?? planet.gameObject.AddComponent<CorpseInstanceRenderer>();
            _instance.Add(resource);
        }
        private void Awake() { _instance = this; _block = new MaterialPropertyBlock(); }
        private void Add(ColonyResource resource)
        {
            if (_owners.ContainsKey(resource)) return;
            var parts = new List<Part>(2); _owners.Add(resource, parts);
            foreach (var renderer in resource.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>(); var material = renderer.sharedMaterial;
                if (filter == null || filter.sharedMesh == null || material == null || !renderer.enabled) continue;
                renderer.GetPropertyBlock(_block);
                Color color = _block.HasColor("_BaseColor") ? _block.GetColor("_BaseColor") : material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                Color emission = _block.HasColor("_EmissionColor") ? _block.GetColor("_EmissionColor") : material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                var key = (filter.sharedMesh, material, color, emission, renderer.shadowCastingMode, renderer.receiveShadows, renderer.gameObject.layer);
                if (!_appearances.TryGetValue(key, out var appearance))
                {
                    // 不把逐对象属性块交给实例化；同色尸体用独立共享色板，绝不改原材质和能量块颜色。
                    var palette = new Material(material) { name = "Corpse instance palette", enableInstancing = true };
                    if (palette.HasProperty("_BaseColor")) palette.SetColor("_BaseColor", color);
                    if (palette.HasProperty("_Color")) palette.SetColor("_Color", color);
                    if (palette.HasProperty("_EmissionColor")) palette.SetColor("_EmissionColor", emission);
                    appearance = new Appearance { mesh = filter.sharedMesh, material = palette, radius = filter.sharedMesh.bounds.extents.magnitude,
                        parameters = new RenderParams(palette) { layer = renderer.gameObject.layer, shadowCastingMode = renderer.shadowCastingMode, receiveShadows = renderer.receiveShadows, lightProbeUsage = LightProbeUsage.Off, motionVectorMode = MotionVectorGenerationMode.ForceNoMotion } };
                    _appearances.Add(key, appearance);
                }
                var part = new Part { owner = resource, source = renderer, transform = renderer.transform, appearance = appearance, original = renderer.enabled, index = _parts.Count };
                parts.Add(part); _parts.Add(part);
            }
        }
        public static void Remove(ColonyResource resource)
        {
            if (_instance == null || ReferenceEquals(resource, null) || !_instance._owners.Remove(resource, out var parts)) return;
            foreach (var part in parts)
            {
                if(part.group!=null){part.group.Detach(part,_instance._limit);_instance._instanceCount--;}
                if (part.source != null) part.source.enabled = part.original;
                int last = _instance._parts.Count - 1; var moved = _instance._parts[last];
                _instance._parts[part.index] = moved; moved.index = part.index; _instance._parts.RemoveAt(last);
            }
        }
        private void Restore()
        { foreach (var part in _parts) { if (part.source != null && part.batched) part.source.enabled = part.original; part.batched = false; } }
        private void LateUpdate()
        {
            using var timing=PerformanceTimings.Measure(PerformanceTimings.Area.CorpseBatches);
            LastBatches = LastInstances = 0;
            var settings = SimulationOptimization.Current;
            if (_failed || !settings.corpseBatchRendering || !settings.resourceInstancing || !SystemInfo.supportsInstancing) { Restore(); return; }
            float size = settings.corpseRenderCellSize; int limit = settings.corpseBatchSize;
            try
            {
                // ★ 修复（防Bug回归）：静止尸体不应每帧重读上万份Transform矩阵；只在自身/标记/星球变换时更新实例数据。
                var world=transform.localToWorldMatrix;bool resize=size!=_cellSize||limit!=_limit;_world=world;_cellSize=size;_limit=limit;var inverse=world.inverse;
                if(resize){_groups.Clear();_visible.Clear();_instanceCount=0;foreach(var part in _parts){part.group=null;part.slot=-1;part.dirty=true;}}
                foreach(var owner in _owners)
                {
                    if(owner.Key==null)continue;
                    bool active=owner.Key.isActiveAndEnabled&&owner.Key.State.energy>0;
                    var root=owner.Key.transform;bool rootSignal=root.hasChanged,moved=false;
                    foreach(var part in owner.Value)
                    {
                        part.active=active&&part.source!=null;
                        if(!part.active){if(part.group!=null){part.group.Detach(part,limit);_instanceCount--;}part.dirty=true;if(part.source!=null&&part.batched){part.source.enabled=part.original;part.batched=false;}continue;}
                        bool poseChanged=false;
                        if(part.dirty||rootSignal||part.transform.hasChanged)
                        {
                            var position=part.transform.localPosition;var rotation=part.transform.localRotation;var scale=part.transform.localScale;
                            poseChanged=part.localPosition!=position||part.localRotation!=rotation||part.localScale!=scale;
                            part.localPosition=position;part.localRotation=rotation;part.localScale=scale;part.transform.hasChanged=false;
                        }
                        if(part.transform==root)moved=poseChanged;
                        if(part.dirty||resize||poseChanged||moved&&part.transform!=root)
                        {
                            // ★ 修复（防Bug回归）：批次保存星球局部矩阵；自转不重读全部尸体的世界矩阵，只在可见批次提交时转换。
                            part.matrix=inverse*part.transform.localToWorldMatrix;var point=part.matrix.GetColumn(3);
                            var cell=new Vector3Int(Mathf.FloorToInt(point.x/size),Mathf.FloorToInt(point.y/size),Mathf.FloorToInt(point.z/size));
                            var key=(part.appearance,cell);
                            if(!_groups.TryGetValue(key,out var group)){group=new Group{appearance=part.appearance};_groups.Add(key,group);_visible.Add(group);}
                            float scale=Mathf.Sqrt(Mathf.Max(part.matrix.GetColumn(0).sqrMagnitude,part.matrix.GetColumn(1).sqrMagnitude,part.matrix.GetColumn(2).sqrMagnitude));
                            part.bounds=new Bounds(point,Vector3.one*(part.appearance.radius*scale*2));
                            // ★ 修复（防Bug回归）：只写变化对象的槽；跨格时交换删除旧槽，绝不因一具尸体搬运重建数千静止实例。
                            if(part.group!=group){if(part.group!=null){part.group.Detach(part,limit);_instanceCount--;}group.Attach(part,limit);_instanceCount++;}else group.Change(part,limit);
                            part.dirty=false;part.transform.hasChanged=false;
                        }
                        if(!part.batched){part.source.enabled=false;part.batched=true;}
                    }
                    root.hasChanged=false;
                }
                LastInstances=_instanceCount;
            }
            catch (System.InvalidOperationException ex)
            { _failed = true; Restore(); Debug.LogWarning("[尸体批次] 当前平台/材质不可实例化，恢复原显示：" + ex.Message); }
        }
        // ★ 修复（防Bug回归）：每台镜头仅提交可见空间格；原先把地球背面和镜头外批次也逐个提交，CPU绘制仍过高。
        private void OnEnable(){RenderPipelineManager.beginCameraRendering+=Draw;Camera.onPreCull+=DrawBuiltIn;}
        private void DrawBuiltIn(Camera camera){if(GraphicsSettings.currentRenderPipeline==null)Draw(default,camera);}
        private void Draw(ScriptableRenderContext context,Camera camera)
        {
            if(_failed||!isActiveAndEnabled||!SimulationOptimization.Current.corpseBatchRendering||!SimulationOptimization.Current.resourceInstancing)return;
            using var timing=PerformanceTimings.Measure(PerformanceTimings.Area.CorpseBatches);
            _world=transform.localToWorldMatrix;GeometryUtility.CalculateFrustumPlanes(camera,_planes);
            var scale=transform.lossyScale;var planet=EcologyQueries.Planet;float occluder=planet!=null&&planet.Data!=null?planet.radius*Mathf.Min(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z))*Mathf.Cos(2*Mathf.Atan(1f/planet.Data.Subdivisions)):0;
            Vector3 center=transform.position,eye=camera.transform.position;float eyeDistance=Vector3.Distance(eye,center);
            try
            {
                foreach(var group in _visible)
                {
                    if(group.used==0)continue;
                    var bounds=WorldBounds(group.bounds,_world);
                    if(!GeometryUtility.TestPlanesAABB(_planes,bounds))continue;
                    // 使用地形保守内切球，只剔除整个格子被实心星球遮住的情况；保留近处/地平线和水下对象。
                    var toward=bounds.center-eye;float distance=toward.magnitude,radius=bounds.extents.magnitude;
                    if(occluder>0&&eyeDistance>occluder&&distance>radius)
                    {
                        float cone=Mathf.Asin(occluder/eyeDistance)-Mathf.Asin(Mathf.Clamp01(radius/distance));
                        if(cone>0&&Vector3.Dot((center-eye)/eyeDistance,toward/distance)>Mathf.Cos(cone)&&distance-radius>Mathf.Sqrt(eyeDistance*eyeDistance-occluder*occluder))continue;
                    }
                    var parameters=group.appearance.parameters;parameters.camera=camera;parameters.worldBounds=bounds;
                    for(int i=0;i<group.used;i++){var chunk=group.chunks[i];for(int j=0;j<chunk.count;j++)chunk.world[j]=_world*chunk.matrices[j];Graphics.RenderMeshInstanced(parameters,group.appearance.mesh,0,chunk.world,chunk.count);LastBatches++;}
                }
            }
            catch(System.InvalidOperationException ex){_failed=true;Restore();Debug.LogWarning("[尸体批次] 恢复原显示："+ex.Message);}
        }
        private static Bounds WorldBounds(Bounds local,Matrix4x4 world)
        {
            var e=local.extents;var x=world.MultiplyVector(new Vector3(e.x,0,0));var y=world.MultiplyVector(new Vector3(0,e.y,0));var z=world.MultiplyVector(new Vector3(0,0,e.z));
            return new Bounds(world.MultiplyPoint3x4(local.center),new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*2);
        }
        private void OnDisable(){RenderPipelineManager.beginCameraRendering-=Draw;Camera.onPreCull-=DrawBuiltIn;Restore();}
        private void OnDestroy()
        { Restore(); foreach (var item in _appearances.Values) if (item.material != null) Destroy(item.material); if (_instance == this) _instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _instance = null; LastBatches = LastInstances = 0;InstanceWrites=0; }
    }
}
