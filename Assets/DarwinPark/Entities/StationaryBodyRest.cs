using DarwinPark.Entities.Ecology;
using System.Collections.Generic;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 新增：只让已落地且无移动能力的动态刚体休眠，保留碰撞、被击退、搬运和基因获得行动后的唤醒能力。
    public static class StationaryBodyRest
    {
        private sealed class Contact {public Vector3 position;public Matrix4x4 matrix;public Transform planet;public PlanetTerrainMeshBuilder builder;public PlanetTerrainData data;public MeshCollider ground;public Mesh mesh;public EntityGeneController genes;public int revision;public bool zeroBaseSpeed;public float radius,heightScale;}
        private static readonly Dictionary<Rigidbody,Contact> Contacts=new Dictionary<Rigidbody,Contact>();
        public static bool IsResting(Rigidbody body)
        {
            if(!body.IsSleeping()||!Contacts.TryGetValue(body,out var contact))return false;
            // ★ 修复（防Bug回归）：外部休眠、存档传送和星球移动不能绕过地表救援；只跳过经过接触验证且位置未变的休眠。
            // ★ 修复（防Bug回归）：缩放、替换/禁用碰撞体和表达改变也必须唤醒，不能只核对位置/旋转。
            return contact.planet!=null&&contact.builder!=null&&contact.ground!=null&&contact.ground.enabled&&contact.ground.sharedMesh==contact.mesh&&contact.genes!=null&&contact.genes.LogicRevision==contact.revision&&ReferenceEquals(contact.data,contact.builder.Data)&&contact.radius==contact.builder.radius&&contact.heightScale==contact.builder.heightScale&&body.position==contact.position&&contact.planet.localToWorldMatrix==contact.matrix;
        }
        public static bool RemainsAsleep(Rigidbody body,EntityGeneController genes)
        {
            // ★ 修复（防Bug回归）：零基础速度且表达版本未变时不会被乘法修饰变成可移动，不必每个物理步重查全部角色。
            if(EcologyPerformanceSettings.Current.stationarySleep&&IsResting(body)&&genes!=null&&(Contacts[body].zeroBaseSpeed||genes.GetAttribute("moveSpeed")<=0))return true;
            Contacts.Remove(body);if(body.IsSleeping())body.WakeUp();return false;
        }
        public static void TryRest(Rigidbody body,EntityGeneController genes,Collider collider,RaycastHit hit,Transform planet)
        {RestAt(body,genes,collider,hit.point,hit.normal,hit.collider,planet,genes!=null?genes.GetAttribute("moveSpeed"):0);}
        // ★ 适配：共享真实三角形接触点和本步已解析的移动速度，避免第二次查询角色/光环。
        public static void TryRest(Rigidbody body,EntityGeneController genes,Collider collider,EntityGroundSample hit,Transform planet,float moveSpeed)
        {RestAt(body,genes,collider,hit.point,hit.normal,hit.ground,planet,moveSpeed);}
        private static void RestAt(Rigidbody body,EntityGeneController genes,Collider collider,Vector3 point,Vector3 normal,Collider ground,Transform planet,float moveSpeed)
        {
            var config=EcologyPerformanceSettings.Current;
            if(!config.stationarySleep||genes==null||collider==null||ground==null||ground.transform!=planet||moveSpeed>0)return;
            // ★ 修复（防Bug回归）：只在真正接触星球地面时休眠；不能把出生下落或站在移动实体上误当成静止植物。
            // ★ 修复（防Bug回归）：高速/下落时根本不可能休眠，先判断速度避免无效ClosestPoint；斜坡再验证真实接触。
            if(body.linearVelocity.sqrMagnitude>config.restSpeed*config.restSpeed||!VerifiedGroundRest.Touching(collider,ground,point,normal,config.restContactSlack))return;
            var builder=planet.GetComponent<PlanetTerrainMeshBuilder>();if(builder==null||builder.Data==null)return;
            if(!Contacts.TryGetValue(body,out var record)){record=new Contact();Contacts.Add(body,record);}
            // ★ 适配：地形重建或星球参数变化后也须唤醒，不能让旧休眠实体悬在新地表上方。
            record.builder=builder;record.data=builder.Data;record.radius=builder.radius;record.heightScale=builder.heightScale;
            record.position=body.position;record.planet=planet;record.matrix=planet.localToWorldMatrix;record.ground=ground as MeshCollider;record.mesh=record.ground!=null?record.ground.sharedMesh:null;record.genes=genes;record.revision=genes.LogicRevision;record.zeroBaseSpeed=genes.Evaluation==null||!genes.Evaluation.Attributes.TryGetValue("moveSpeed",out float basic)||basic<=0;body.Sleep();
        }
        public static void Remove(Rigidbody body){if(body!=null)Contacts.Remove(body);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void Clear()=>Contacts.Clear();
    }
}
