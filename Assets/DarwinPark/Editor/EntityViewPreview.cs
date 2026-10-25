using System.IO;
using System.Linq;
using DarwinPark.Entities;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using DarwinPark.UI;
using UnityEditor;
using UnityEngine;

namespace DarwinPark.EditorTools
{
    /// <summary>★ 新增：在真实陆地创建临时可操控个体，供近景纹理、键鼠与跳跃实机验收；退出播放自动清理。</summary>
    public static class EntityViewPreview
    {
        [MenuItem("Tools/Darwin Park/Preview Entity View")]
        public static void Preview()
        {
            if (!Application.isPlaying) { Debug.LogError("请先播放并等待星球生成"); return; }
            var builder = Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>();
            var view = Object.FindFirstObjectByType<EntityViewController>();
            if (builder == null || builder.Data == null || view == null) return;
            Vector3 toward = (Camera.main.transform.position-builder.transform.position).normalized;
            var normals = builder.GetComponent<MeshFilter>().sharedMesh.normals;
            var vertex = builder.Data.Vertices.Where((p,i)=>p.magnitude>builder.WaterRadius+2 && p.magnitude<builder.WaterRadius+6 && Vector3.Dot(p.normalized,normals[i])>.97f)
                .OrderByDescending(p=>Vector3.Dot(builder.transform.TransformDirection(p.normalized),toward)).FirstOrDefault();
            if (vertex==Vector3.zero) { Debug.LogError("未找到适合预览的陆地");return; }
            Vector3 up=builder.transform.TransformDirection(vertex.normalized);
            var genome=new Genome();genome.Add(GeneInstance.Create("gene_growth",10,0));genome.Add(GeneInstance.Create("gene_locomotion",75,0));
            var before=EntityRegistry.All.ToArray();
            if(!EntityPlacementUtility.Spawn(genome,builder.transform.TransformPoint(vertex)+up*1.2f))return;
            var entity=EntityRegistry.All.First(e=>!before.Contains(e));entity.DisplayName="近景操控预览";
            view.EnterView(entity);
            double captureAt=EditorApplication.timeSinceStartup+1;
            EditorApplication.CallbackFunction capture=null;
            capture=()=>{
                if(EditorApplication.timeSinceStartup<captureAt)return;
                EditorApplication.update-=capture;
                if(!Application.isPlaying || view.Target!=entity)return;
                Directory.CreateDirectory("Artifacts/EntityControlReview");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/EntityControlReview/near-surface.png"));
            };
            EditorApplication.update+=capture;
        }
    }
}
