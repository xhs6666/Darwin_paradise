using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarwinPark.UI
{
    // ★ 新增：远景实体以共享模型显式批量提交，避免深度等通道仍逐个经过 MeshRenderer。
    internal sealed class FarBodyBatchRenderer
    {
        private const int BatchSize=256;
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        private static readonly int BodyColor=Shader.PropertyToID("_BodyColor");
        private static readonly int FarBody=Shader.PropertyToID("_FarBody");
        private readonly Matrix4x4[] _matrices=new Matrix4x4[BatchSize];
        private readonly Vector4[] _colors=new Vector4[BatchSize],_tints=new Vector4[BatchSize];
        private readonly Plane[] _planes=new Plane[6];
        private readonly MaterialPropertyBlock _batch=new();
        private HashSet<MeshRenderer> _hidden=new(),_visibleBatch=new();
        private int _count;
        private Bounds _bounds;
        public int LastInstances {get;private set;}
        public int LastBatches {get;private set;}

        public FarBodyBatchRenderer()
        {
            var far=new float[BatchSize];
            System.Array.Fill(far,1f);
            _batch.SetFloatArray(FarBody,far);
        }

        public static bool Enabled => SimulationOptimization.Current.farBodyBatches && SystemInfo.supportsInstancing;

        public void Draw(Camera camera,IReadOnlyList<EntityBodyView> views,EntityBodyView detail)
        {
            using var performance = PerformanceTimings.Measure(PerformanceTimings.Area.BodyFarDraw);
            if(camera==null)return;
            if(camera.cameraType!=CameraType.Game||!camera.CompareTag("MainCamera"))
            {
                Restore();
                return;
            }
            LastInstances=LastBatches=0;
            var settings=SimulationOptimization.Current;
            if(!Enabled||WorldRuntimeGate.Loading||views.Count==0)
            {
                Restore();
                return;
            }
            bool orthographic = camera.orthographic;
            float factor = camera.pixelHeight * (orthographic ? 1f / camera.orthographicSize : Mathf.Abs(camera.projectionMatrix.m11));
            float nearClip = camera.nearClipPlane;
            int cullingMask = camera.cullingMask;
            var cameraTransform = camera.transform;
            var cameraPosition = cameraTransform.position;
            var forward = cameraTransform.forward;
            var catalog = MorphologyRegistry.Settings;
            var material = EntityBodyView.BodyMaterial;
            bool canSubmit = material != null && material.enableInstancing;
            Mesh mesh = canSubmit ? EntityBodyView.SimplifiedModel(catalog) : null;
            GeometryUtility.CalculateFrustumPlanes(camera,_planes);
            int layer=-1;
            try
            {
                for (int i = 0; i < views.Count; i++)
                {
                    var view = views[i];
                    if (view == null || !view.HasMesh) continue;
                    // One pass updates distance LOD and gathers the explicit screen-size batches.
                    view.SetDetail(cameraPosition, catalog);
                    if (!canSubmit || view == detail) continue;
                    var renderer = view.BodyRenderer;
                    if (renderer == null || !renderer.enabled || renderer.sharedMaterial != material) continue;
                    bool hidden = renderer.forceRenderingOff;
                    if (hidden && !_hidden.Contains(renderer)) continue;
                    int viewLayer=renderer.gameObject.layer;
                    if((cullingMask&(1<<viewLayer))==0)continue;
                    var bounds=renderer.bounds;
                    float radius=bounds.extents.magnitude;
                    float depth=Vector3.Dot(bounds.center-cameraPosition,forward)-radius;
                    if(depth<=nearClip||radius*factor/(orthographic?1:depth)>settings.farBodyScreenPixels)continue;
                    if(!GeometryUtility.TestPlanesAABB(_planes,bounds))continue;

                    if(_count!=0&&layer!=viewLayer)
                        Submit(camera,mesh,material,layer);
                    layer=viewLayer;
                    _matrices[_count]=renderer.localToWorldMatrix;
                    _colors[_count]=view.InstanceBodyColor;
                    _tints[_count]=view.InstanceTint;
                    if(_count==0)_bounds=bounds;else _bounds.Encapsulate(bounds);
                    _count++;
                    _visibleBatch.Add(renderer);
                    // ★ 修复（防Bug回归）：只在远近分组改变时切换原 Renderer，避免每帧重复写两次。
                    if(!hidden)renderer.forceRenderingOff=true;
                    if(_count==BatchSize)Submit(camera,mesh,material,layer);
                }
                if(_count>0)Submit(camera,mesh,material,layer);
                foreach(var renderer in _hidden)
                    if(renderer!=null&&!_visibleBatch.Contains(renderer))renderer.forceRenderingOff=false;
                (_hidden,_visibleBatch)=(_visibleBatch,_hidden);
                _visibleBatch.Clear();
            }
            catch
            {
                Restore();
                throw;
            }
        }

        private void Submit(Camera camera,Mesh mesh,Material material,int layer)
        {
            _batch.SetVectorArray(BodyColor,_colors);
            _batch.SetVectorArray(BaseColor,_tints);
            var parameters=new RenderParams(material)
            {
                camera=camera,layer=layer,worldBounds=_bounds,matProps=_batch,
                shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false,
                lightProbeUsage=LightProbeUsage.Off
            };
            Graphics.RenderMeshInstanced(parameters,mesh,0,_matrices,_count);
            LastInstances+=_count;LastBatches++;
            _count=0;
        }

        public void Release(MeshRenderer renderer)
        {
            if(renderer!=null&&_hidden.Remove(renderer))renderer.forceRenderingOff=false;
        }

        public void Restore()
        {
            foreach(var renderer in _hidden)if(renderer!=null)renderer.forceRenderingOff=false;
            foreach(var renderer in _visibleBatch)if(renderer!=null)renderer.forceRenderingOff=false;
            _hidden.Clear();
            _visibleBatch.Clear();
            _count=0;
        }
    }
}
