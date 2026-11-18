using System;
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Stats;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.Rendering;
namespace DarwinPark.UI
{
    // ★ 新增：所有部件合并成一个Renderer，无部件脚本/碰撞体；专服不安装该表现适配器。
    public sealed class EntityBodyView : MonoBehaviour
    {
        private EntityGeneController _genes;private MeshFilter _filter;private MeshRenderer _renderer;private Mesh _mesh;private bool _far;private Color _bodyColor;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int BodyColor = Shader.PropertyToID("_BodyColor");
        private static readonly int FarBody = Shader.PropertyToID("_FarBody");
        private static readonly MorphologyExpression.Resolver Expression = new();
        private readonly MorphologyExpression.Cache _expressionCache = new();
        private SavedBodyComposition _savedComposition;

        internal SavedBodyComposition CaptureComposition() =>
            _genes?.Evaluation == null ? null : Expression.Capture(_genes.Genome,_genes.Evaluation,_expressionCache);

        internal void RestoreComposition(SavedBodyComposition composition) => _savedComposition = composition;
        private static PartCatalog _simplifiedCatalog;
        private static Mesh _simplifiedModel;
        private static readonly Dictionary<Mesh, (Vector3[] vertices, Vector3[] normals, int[] triangles)> ModelGeometry = new();
        private EntityStats _stats;
        private Transform _bodyTransform;
        private Color _tint = Color.white;
        private bool _hasTint;
        private MaterialPropertyBlock _block;private int _signature;private bool _hasSignature,_appearanceDirty;
        private sealed class MeshEntry {public Mesh mesh;public int users;public List<ExpressedPart> parts;public object catalog;}
        private static readonly Dictionary<int,List<MeshEntry>> Meshes=new();private MeshEntry _entry;
        public static long MeshBuilds {get;private set;}public static long MeshReuses {get;private set;}
        private static readonly Dictionary<string,Mesh> Models=new();private static readonly HashSet<Mesh> OwnedModels=new();private static Material _material;
        public int PartCount {get;private set;}public IReadOnlyList<ExpressedPart> Parts {get;private set;}
        public static void Attach(DarwinEntity entity){if(entity.GetComponent<EntityBodyView>()==null)entity.gameObject.AddComponent<EntityBodyView>();}
        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _bodyTransform = transform;
            _genes = GetComponent<EntityGeneController>();
            _stats = GetComponent<EntityStats>();
            _filter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            _genes.OnEvaluationChanged += Dirty;
            if (_stats != null) _stats.RenderTintChanged += TintChanged;
        }
        private void OnEnable()=>BodyVisualScheduler.Register(this);
        private void Dirty(GenomeEvaluation evaluation)
        {
            using var performance = PerformanceTimings.Measure(PerformanceTimings.Area.BodyDirty);
            if (isActiveAndEnabled) BodyVisualScheduler.Enqueue(this);
        }

        private void TintChanged(Renderer renderer, Color tint)
        {
            if (renderer != _renderer) return;
            _hasTint = true;
            _tint = tint;
        }

        internal static Mesh SimplifiedModel(PartCatalog catalog)
        {
            if (_simplifiedCatalog != catalog || _simplifiedModel == null)
            {
                _simplifiedModel = Model(MorphologyRegistry.Part(catalog.simplifiedPartId));
                _simplifiedCatalog = catalog;
            }
            return _simplifiedModel;
        }
        private static Mesh Model(PartDefinition part)
        {
            string key=string.IsNullOrEmpty(part.modelResource)?part.primitive:part.modelResource;if(Models.TryGetValue(key,out var mesh)&&mesh!=null)return mesh;
            if(!string.IsNullOrEmpty(part.modelResource)){mesh=Resources.Load<Mesh>(part.modelResource);if(mesh==null)throw new InvalidOperationException("部件模型资源不存在："+part.modelResource);}
            else
            {
                // 共享低模椭球，形状通过装配矩阵变换；不为每个实体生成新的基础模型。
                const int sides=8,rings=6;var vertices=new List<Vector3>();var triangles=new List<int>();
                for(int y=0;y<=rings;y++){float latitude=Mathf.PI*y/rings;for(int x=0;x<=sides;x++){float angle=2*Mathf.PI*x/sides;var p=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(angle),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(angle))*.5f;if(part.primitive=="capsule")p.y+=Mathf.Sign(p.y)*.2f;else if(part.primitive=="cube")p=p.normalized*.5f/Mathf.Max(Mathf.Abs(p.normalized.x),Mathf.Abs(p.normalized.y),Mathf.Abs(p.normalized.z));vertices.Add(p);}}
                for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){int a=y*(sides+1)+x,b=a+sides+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
                mesh=new Mesh{name="BodyPart-"+key};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();OwnedModels.Add(mesh);
            }
            Models.Add(key,mesh);return mesh;
        }
        public void Rebuild()
        {
            var camera = Camera.main;
            Rebuild(camera != null ? camera.transform.position : _bodyTransform.position, MorphologyRegistry.Settings);
        }

        internal void Rebuild(Vector3 cameraPosition, PartCatalog catalog)
        {
            using var performance = PerformanceTimings.Measure(PerformanceTimings.Area.BodyRebuild);
            if (_genes?.Evaluation == null) return;
            var parts = Expression.Resolve(_genes.Genome, _genes.Evaluation, _expressionCache, _savedComposition);
            _savedComposition = null;
            if (ReferenceEquals(parts, Parts))
            {
                SetDetail(cameraPosition, catalog);
                return;
            }
            // ★ 修复（防Bug回归）：环境重算不代表形体变化；仅跨越可见精度时重建，避免不断分配模型。
            int signature = Signature(parts);
            if (_hasSignature && signature == _signature && Matches(_entry, parts, catalog))
            {
                Parts = parts;
                SetDetail(cameraPosition, catalog);
                return;
            }
            // ★ 修复（防Bug回归）：相同部件表达共用合并网格，编辑个体改用新条目，不复制或改写其他实体的模型。
            foreach(var part in parts)if(part.slotId=="body")_bodyColor=part.color;
            MeshEntry entry=null;if(Meshes.TryGetValue(signature,out var choices))foreach(var choice in choices)if(Matches(choice,parts,catalog)){entry=choice;MeshReuses++;break;}
            if(entry==null)
            {
            using var build = PerformanceTimings.Measure(PerformanceTimings.Area.BodyMeshBuild);
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            foreach(var part in parts)
            {
                var model=Model(MorphologyRegistry.Part(part.partId));if (!ModelGeometry.TryGetValue(model, out var geometry))
                {
                    geometry = (model.vertices, model.normals, model.triangles);
                    ModelGeometry.Add(model, geometry);
                }
                var points=geometry.vertices;var sourceNormals=geometry.normals;int first=vertices.Count;var matrix=Matrix4x4.TRS(part.position,part.rotation,part.scale);var normalMatrix=matrix.inverse.transpose;
                for(int i=0;i<points.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(points[i]));normals.Add(normalMatrix.MultiplyVector(sourceNormals[i]).normalized);colors.Add(part.color);}
                foreach(int index in geometry.triangles)indices.Add(first+index);
            }
            var mesh=new Mesh{name="OrganismBody",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();MeshBuilds++;entry=new MeshEntry{mesh=mesh,parts=parts,catalog=catalog};if(!Meshes.TryGetValue(signature,out choices))Meshes[signature]=choices=new();choices.Add(entry);
            }
            ReleaseMesh();_entry=entry;entry.users++;_mesh=entry.mesh;_hasSignature=true;_signature=signature;_filter.sharedMesh=_mesh;
            if(_material==null){_material=Resources.Load<Material>("Morphology/body");if(_material==null)throw new InvalidOperationException("缺少Morphology/body材质");}_material.enableInstancing=true;_renderer.sharedMaterial=_material;_renderer.shadowCastingMode=ShadowCastingMode.Off;_renderer.receiveShadows=false;_far=false;_appearanceDirty=true;
            Parts = parts;
            PartCount = parts.Count;
            SetDetail(cameraPosition, catalog);
        }
        internal void SetDetail(Vector3 cameraPosition, PartCatalog catalog)
        {
            if (_mesh == null) return;
            bool far = (cameraPosition - _bodyTransform.position).sqrMagnitude > catalog.farDistance * catalog.farDistance;
            if (far == _far && !_appearanceDirty) return;
            _appearanceDirty = false;
            if (far != _far || _filter.sharedMesh == null)
            {
                _filter.sharedMesh = far ? SimplifiedModel(catalog) : _mesh;
                _far = far;
            }
            _renderer.GetPropertyBlock(_block);
            if (!_hasTint)
            {
                _tint = _block.HasColor(BaseColor) ? _block.GetColor(BaseColor) : Color.white;
                _hasTint = true;
            }
            _block.SetFloat(FarBody, far ? 1 : 0);
            _block.SetColor(BodyColor, _bodyColor);
            _renderer.SetPropertyBlock(_block);
        }

        internal MeshRenderer BodyRenderer => _renderer;
        internal static Material BodyMaterial => _material;
        internal Color InstanceTint => _tint;
        internal Color InstanceBodyColor => _bodyColor;
        internal bool HasMesh => _mesh != null;

        private static int Signature(IReadOnlyList<ExpressedPart> parts)
        {
            int signature = 17;
            foreach (var part in parts)
            {
                unchecked
                {
                    signature = signature * 31 + part.slotId.GetHashCode();
                    signature = signature * 31 + part.partId.GetHashCode();
                }
                var s = part.scale;
                var p = part.position;
                var c = part.color;
                var r = part.rotation;
                signature = Combine(Combine(Combine(signature, s.x), s.y), s.z);
                signature = Combine(Combine(Combine(signature, p.x), p.y), p.z);
                signature = Combine(Combine(Combine(signature, c.r), c.g), c.b);
                signature = Combine(Combine(Combine(Combine(signature, r.x), r.y), r.z), r.w);
            }
            return signature;
        }

        private static int Combine(int signature, float value) => unchecked(signature * 31 + Mathf.RoundToInt(value * 64));
        private static bool Same(float a,float b)=>Mathf.RoundToInt(a*64)==Mathf.RoundToInt(b*64);
        private static bool Same(Vector3 a,Vector3 b)=>Same(a.x,b.x)&&Same(a.y,b.y)&&Same(a.z,b.z);
        private static bool Same(Quaternion a,Quaternion b)=>Same(a.x,b.x)&&Same(a.y,b.y)&&Same(a.z,b.z)&&Same(a.w,b.w);
        private static bool Same(Color a,Color b)=>Same(a.r,b.r)&&Same(a.g,b.g)&&Same(a.b,b.b);
        private static bool Matches(MeshEntry entry,IReadOnlyList<ExpressedPart> parts, PartCatalog catalog)
        {
            if(entry==null||entry.mesh==null||entry.catalog!=catalog||entry.parts.Count!=parts.Count)return false;
            for(int i=0;i<parts.Count;i++){var a=entry.parts[i];var b=parts[i];if(a.partId!=b.partId||a.slotId!=b.slotId||!Same(a.scale,b.scale)||!Same(a.position,b.position)||!Same(a.rotation,b.rotation)||!Same(a.color,b.color))return false;}return true;
        }
        private void ReleaseMesh()
        {
            if(_entry==null)return;if(--_entry.users==0){if(Meshes.TryGetValue(_signature,out var choices)){choices.Remove(_entry);if(choices.Count==0)Meshes.Remove(_signature);}if(_entry.mesh!=null)Destroy(_entry.mesh);}_entry=null;
        }
        private void OnDisable()=>BodyVisualScheduler.Unregister(this);
        private void OnDestroy(){if(_genes!=null)_genes.OnEvaluationChanged-=Dirty;if(_stats!=null)_stats.RenderTintChanged-=TintChanged;ReleaseMesh();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){foreach(var choices in Meshes.Values)foreach(var entry in choices)if(entry.mesh!=null){if(Application.isPlaying)Destroy(entry.mesh);else DestroyImmediate(entry.mesh);}Meshes.Clear();MeshBuilds=MeshReuses=0;foreach(var mesh in OwnedModels)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}OwnedModels.Clear();Models.Clear();ModelGeometry.Clear();_material=null;_simplifiedCatalog=null;_simplifiedModel=null;}
    }
    // ★ 新增：全局有预算的外观更新队列，重算事件不直接阻塞模拟，远近切换不重建完整网格。
    public sealed class BodyVisualScheduler : MonoBehaviour
    {
        private static BodyVisualScheduler _instance;private static readonly List<EntityBodyView> Views=new();private static readonly Queue<EntityBodyView> Dirty=new();private static readonly HashSet<EntityBodyView> Queued=new();private int _cursor;
        private FarBodyBatchRenderer _batches;
        private bool _batchFailure;
        private void Awake()=>_batches=new FarBodyBatchRenderer();
        private GeneInspectorUI _inspector;
        private float _nextInspectorLookup;
        public static int FarInstances=>_instance!=null?(_instance._batches?.LastInstances??0):0;
        public static int FarBatches=>_instance!=null?(_instance._batches?.LastBatches??0):0;
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering+=Draw;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=Draw;
            _batches?.Restore();
        }
        private void Draw(ScriptableRenderContext context,Camera camera)
        {
            if(_inspector==null&&Time.unscaledTime>=_nextInspectorLookup)
            {
                _nextInspectorLookup=Time.unscaledTime+2;
                _inspector=FindFirstObjectByType<GeneInspectorUI>();
            }
            var selected=_inspector!=null?_inspector.CurrentEntity:null;
            if(_batchFailure)return;
            try{_batches?.Draw(camera,Views,selected!=null?selected.GetComponent<EntityBodyView>():null);}
            catch(Exception error){_batchFailure=true;_batches?.Restore();Debug.LogException(error,this);}
        }
        internal static void Release(Renderer renderer){if(renderer is MeshRenderer mesh)_instance?._batches?.Release(mesh);}

        public static void Register(EntityBodyView view){if(!Views.Contains(view))Views.Add(view);if(_instance==null)_instance=new GameObject("BodyVisualScheduler").AddComponent<BodyVisualScheduler>();Enqueue(view);}
        public static void Unregister(EntityBodyView view)
        {
            Release(view.BodyRenderer);
            Views.Remove(view);
            Queued.Remove(view);
            if (Views.Count != 0) return;
            Dirty.Clear();
            if (_instance != null) _instance._cursor = 0;
        }
        public static void Enqueue(EntityBodyView view){if(Queued.Add(view))Dirty.Enqueue(view);}
        private void LateUpdate()
        {
            using var performance = PerformanceTimings.Measure(PerformanceTimings.Area.BodyQueue);
            if (WorldRuntimeGate.Loading || Views.Count == 0) return;
            bool needsLodPass = _batchFailure || !FarBodyBatchRenderer.Enabled;
            if (Dirty.Count == 0 && !needsLodPass) return;

            var catalog = MorphologyRegistry.Settings;
            double end = Time.realtimeSinceStartupAsDouble + catalog.refreshMilliseconds / 1000;
            var camera = Camera.main;
            Vector3 point = camera != null ? camera.transform.position : Vector3.zero;
            while (Dirty.Count > 0 && Time.realtimeSinceStartupAsDouble < end)
            {
                var view = Dirty.Dequeue();
                if (view == null || !Queued.Remove(view) || !view.isActiveAndEnabled) continue;
                try { view.Rebuild(camera != null ? point : view.transform.position, catalog); }
                catch (Exception error) { Debug.LogError("[实体部件] " + error.Message, view); }
            }

            if (!needsLodPass) return;
            int count = Views.Count;
            while (count-- > 0 && Time.realtimeSinceStartupAsDouble < end)
            {
                if (_cursor >= Views.Count) _cursor = 0;
                var view = Views[_cursor++];
                if (view != null) view.SetDetail(point, catalog);
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){Views.Clear();Dirty.Clear();Queued.Clear();_instance=null;}
    }
}
