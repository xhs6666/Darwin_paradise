using DarwinPark.Entities.Ecology;
using System.Collections.Generic;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.Entities
{
    // ★ 新增：只让已落地且无移动能力的动态刚体休眠，保留碰撞、被击退、搬运和基因获得行动后的唤醒能力。
    public static class StationaryBodyRest
    {
        private sealed class Contact {public Vector3 position,planetPosition;public Quaternion rotation;public Transform planet;public PlanetTerrainMeshBuilder builder;public PlanetTerrainData data;public float radius,heightScale;}
        private static readonly Dictionary<Rigidbody,Contact> Contacts=new Dictionary<Rigidbody,Contact>();
        public static bool IsResting(Rigidbody body)
        {
            if(!body.IsSleeping()||!Contacts.TryGetValue(body,out var contact))return false;
            // ★ 修复（防Bug回归）：外部休眠、存档传送和星球移动不能绕过地表救援；只跳过经过接触验证且位置未变的休眠。
            return contact.planet!=null&&contact.builder!=null&&ReferenceEquals(contact.data,contact.builder.Data)&&contact.radius==contact.builder.radius&&contact.heightScale==contact.builder.heightScale&&body.position==contact.position&&contact.planet.position==contact.planetPosition&&contact.planet.rotation==contact.rotation;
        }
        public static bool RemainsAsleep(Rigidbody body,EntityGeneController genes)
        {
            if(EcologyPerformanceSettings.Current.stationarySleep&&IsResting(body)&&genes!=null&&genes.GetAttribute("moveSpeed")<=0)return true;
            Contacts.Remove(body);if(body.IsSleeping())body.WakeUp();return false;
        }
        public static void TryRest(Rigidbody body,EntityGeneController genes,Collider collider,RaycastHit hit,Transform planet)
        {
            var config=EcologyPerformanceSettings.Current;
            if(!config.stationarySleep||genes==null||collider==null||hit.collider==null||hit.collider.transform!=planet||genes.GetAttribute("moveSpeed")>0)return;
            // ★ 修复（防Bug回归）：只在真正接触星球地面时休眠；不能把出生下落或站在移动实体上误当成静止植物。
            Vector3 contact=collider.ClosestPoint(hit.point);
            if((contact-hit.point).sqrMagnitude>config.restContactSlack*config.restContactSlack||body.linearVelocity.sqrMagnitude>config.restSpeed*config.restSpeed)return;
            var builder=planet.GetComponent<PlanetTerrainMeshBuilder>();if(builder==null||builder.Data==null)return;
            if(!Contacts.TryGetValue(body,out var record)){record=new Contact();Contacts.Add(body,record);}
            // ★ 适配：地形重建或星球参数变化后也须唤醒，不能让旧休眠实体悬在新地表上方。
            record.builder=builder;record.data=builder.Data;record.radius=builder.radius;record.heightScale=builder.heightScale;
            record.position=body.position;record.planet=planet;record.planetPosition=planet.position;record.rotation=planet.rotation;body.Sleep();
        }
        public static void Remove(Rigidbody body){if(body!=null)Contacts.Remove(body);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void Clear()=>Contacts.Clear();
    }
}
