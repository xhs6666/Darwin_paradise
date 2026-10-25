using System.Collections.Generic;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

namespace DarwinPark.UI
{
    /// <summary>★ 新增：点击/拖拽共用的地表拾取、纯视觉预览及独立基因组投放。</summary>
    public static class EntityPlacementUtility
    {
        // ★ 修复（防Bug回归）：拖拽松手不能读取缓存的 pointerEnter，按释放坐标重做 UI 命中。
        public static bool IsPointerOverUI(Vector2 screen)
        {
            if(EventSystem.current==null)return false;
            var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
            return hits.Exists(h=>h.module is UnityEngine.UI.GraphicRaycaster);
        }
        public static bool TryGetSurface(Camera camera, Vector2 screen, out Vector3 point, out Vector3 outward)
        {
            point = default; outward = Vector3.up;
            if (camera == null) return false;
            var builder = Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            if (builder == null || builder.Data == null) return false;
            var collider = builder.GetComponent<MeshCollider>();
            var ray=camera.ScreenPointToRay(screen);
            if (collider == null || !collider.Raycast(ray, out var hit, camera.farClipPlane))
                return false;
            // ★ 修复（防Bug回归）：穿进星球后的背面不能作为可见的投放地面。
            if(Vector3.Dot(hit.normal,-ray.direction)<=0)return false;
            point = hit.point;
            outward = (point - builder.transform.position).normalized;
            return true;
        }

        // ★ 新增：批量投放按球面邻域散开，每个位置重新查询真实地面，避免重叠或落进山体。
        public static int SpawnBatch(Genome genome,Vector3 point,Vector3 outward,float height,int count,float spacing)
        {
            var builder=Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            if(builder==null)return 0;
            var ground=builder.GetComponent<MeshCollider>();if(ground==null)return 0;
            var a=Vector3.Cross(outward,Mathf.Abs(outward.y)>.95f?Vector3.right:Vector3.up).normalized;
            var b=Vector3.Cross(outward,a);int spawned=0;
            float outer=ground.bounds.extents.magnitude+height;
            for(int i=0;i<count;i++)
            {
                float radius=spacing*Mathf.Sqrt(i), angle=i*2.39996323f;
                var dir=(point-builder.transform.position+a*(Mathf.Cos(angle)*radius)+b*(Mathf.Sin(angle)*radius)).normalized;
                if(ground.Raycast(new Ray(builder.transform.position+dir*outer,-dir),out var hit,outer) &&
                    Spawn(genome,hit.point+dir*height))spawned++;
            }
            return spawned;
        }

        public static Genome CopyGenome(Genome source)
        {
            if (source == null) return null;
            var copy = new Genome();
            foreach (var gene in source.Genes)
            {
                var item = GeneInstance.Create(gene.DefinitionId, gene.Value, gene.Tier);
                item.Enabled = gene.Enabled; item.CopyStrength = gene.CopyStrength;
                item.OriginInstanceId = gene.OriginInstanceId;
                item.MissingStateIds = new List<string>(gene.MissingStateIds);
                copy.Add(item);
            }
            return copy;
        }

        public static bool Spawn(Genome genome, Vector3 position)
        {
            if (DarwinEntity.Spawner == null)
            {
                Debug.LogError("[EntityPlacement] 生成器未初始化，请检查 GameBootstrap。");
                return false;
            }
            // 同一模板的多次投放必须复制遗传数据，避免一个实体突变改写模板及同批个体。
            DarwinEntity.Spawner.Invoke(CopyGenome(genome), position, "");
            return true;
        }

        public static GameObject CreatePreview(GameObject prefab)
        {
            if (prefab == null) return null;
            // 不 Instantiate 实体预制体：Awake/OnEnable 会先注册实体、计算基因，再 Destroy 无法撤销副作用。
            var preview = new GameObject("EntityPlacementPreview");
            preview.transform.localScale = prefab.transform.localScale;
            foreach (var source in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (source.sharedMesh == null) continue;
                var sourceRenderer = source.GetComponent<MeshRenderer>();
                if (sourceRenderer == null) continue;
                var child = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(preview.transform, false);
                child.transform.localPosition = prefab.transform.InverseTransformPoint(source.transform.position);
                child.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * source.transform.rotation;
                child.transform.localScale = new Vector3(
                    source.transform.lossyScale.x / prefab.transform.lossyScale.x,
                    source.transform.lossyScale.y / prefab.transform.lossyScale.y,
                    source.transform.lossyScale.z / prefab.transform.lossyScale.z);
                child.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var renderer = child.GetComponent<MeshRenderer>();
                var materials = new Material[sourceRenderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    var sourceMaterial = sourceRenderer.sharedMaterials[i];
                    if (sourceMaterial == null) continue;
                    var material = new Material(sourceMaterial);
                    if (material.HasProperty("_BaseColor"))
                    {
                        Color color = material.GetColor("_BaseColor"); color.a = .45f;
                        material.SetColor("_BaseColor", color);
                    }
                    material.SetFloat("_Surface", 1f); material.SetFloat("_ZWrite", 0f);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            return preview;
        }

        public static void DestroyPreview(GameObject preview)
        {
            if (preview == null) return;
            preview.SetActive(false);
            var materials = new HashSet<Material>();
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null) materials.Add(material);
            foreach (var material in materials) Object.Destroy(material);
            Object.Destroy(preview);
        }
    }
}
