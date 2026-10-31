using System;
using System.Linq;
using System.Collections.Generic;
using DarwinPark.Genetics;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：稳定标识供群体/宿主跨存档引用；禁止使用 InstanceID 或实体名字关联对象。
    public sealed class EcologyIdentity : MonoBehaviour
    {
        private static readonly Dictionary<string,EcologyIdentity> Identities=new Dictionary<string,EcologyIdentity>();
        private DarwinEntity _entity;
        public string Id {get;private set;}=Guid.NewGuid().ToString("N");
        private void OnEnable(){_entity=GetComponent<DarwinEntity>();Identities[Id]=this;}
        private void OnDisable(){if(Identities.TryGetValue(Id,out var current)&&current==this)Identities.Remove(Id);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Identities.Clear();
        public void Restore(string id){if(!Guid.TryParseExact(id,"N",out _))return;OnDisable();Id=id;if(isActiveAndEnabled)OnEnable();}
        // ★ 修复（防Bug回归）：运输每帧解析实体 ID，不能为每名工匠再次遍历全部实体；恢复 ID 和停用时同步更新索引。
        public static DarwinEntity Find(string id)=>!string.IsNullOrEmpty(id)&&Identities.TryGetValue(id,out var identity)&&identity!=null&&identity.isActiveAndEnabled?identity._entity:null;
        public static string Of(DarwinEntity entity)=>Ensure(entity.gameObject).Id;
        public static EcologyIdentity Ensure(GameObject obj)=>obj.GetComponent<EcologyIdentity>()??obj.AddComponent<EcologyIdentity>();
    }
    public static class EcologyQueries
    {
        private static PlanetTerrainMeshBuilder _planet;
        public static PlanetTerrainMeshBuilder Planet=>_planet!=null?_planet:(_planet=UnityEngine.Object.FindFirstObjectByType<PlanetTerrainMeshBuilder>());
        public static GeneEvaluation Active(EntityGeneController genes,string id)
        {
            // ★ 修复（防Bug回归）：卵虽继承了社会/寄生基因，未孵化时不能开采、当领袖或附着宿主。
            if(genes==null||genes.GetComponent<EggIncubation>()?.Incubating==true)return null;
            // ★ 修复（防Bug回归）：只寻找最强表达，线性选择无需为每次感知和成员查询创建排序迭代器。
            GeneEvaluation selected=null;
            if(genes.Evaluation!=null)foreach(var gene in genes.Evaluation.Genes)
                if(gene.IsActive&&gene.Definition.Id==id&&(selected==null||gene.EffectiveStrength>selected.EffectiveStrength))selected=gene;
            return selected;
        }
        public static bool Alive(DarwinEntity entity)=>entity!=null&&entity.isActiveAndEnabled&&entity.GetComponent<Stats.EntityStats>() is Stats.EntityStats stats&&stats.TryGet("health",out var hp)&&hp>0;
        public static Vector3 ToLocal(Vector3 point)=>Planet!=null?Planet.transform.InverseTransformPoint(point):point;
        public static Vector3 ToWorld(Vector3 point)=>Planet!=null?Planet.transform.TransformPoint(point):point;
        public static Vector3 Up(Vector3 point)=>(point-(Planet!=null?Planet.transform.position:Vector3.zero)).normalized;
        public static float Distance(Vector3 a,Vector3 b)
        {
            if(Planet==null)return Vector3.Distance(a,b);
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(Up(a),Up(b)),-1,1))*Mathf.Max(Planet.radius,Planet.WaterRadius);
        }
        public static Vector3 Surface(Vector3 point)
        {
            if(Planet==null)return point;
            Vector3 direction=Up(point);if(direction.sqrMagnitude<.5f)direction=Vector3.up;
            var col=Planet.GetComponent<MeshCollider>();float outside=Planet.radius+Planet.heightScale+100;
            if(col!=null&&col.Raycast(new Ray(Planet.transform.position+direction*outside,-direction),out var hit,outside*2))return hit.point+direction;
            return Planet.transform.position+direction*(Planet.WaterRadius+1);
        }
    }
}
