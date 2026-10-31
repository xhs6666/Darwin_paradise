using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：在原图层按钮网格追加种群范围；范围线按球面地表采样，同群颜色与成员标记一致。
    public sealed class PopulationRangeLayer : MonoBehaviour
    {
        private static PopulationRangeLayer _instance;
        private readonly Dictionary<string,LineRenderer> _rings=new Dictionary<string,LineRenderer>();
        private bool _visible;private float _next;
        public static bool Visible=>_instance!=null&&_instance._visible;
        public static void SetVisible(bool value)
        {
            if(_instance==null&&value)_instance=new GameObject("种群范围图层").AddComponent<PopulationRangeLayer>();
            if(_instance==null)return;_instance._visible=value;_instance._next=0;
            foreach(var ring in _instance._rings.Values)if(ring!=null)ring.enabled=value;
        }
        public static void ClearWorld(){if(_instance==null)return;foreach(var ring in _instance._rings.Values)if(ring!=null)Destroy(ring.gameObject);_instance._rings.Clear();SetVisible(false);}
        private void OnDestroy(){if(_instance==this)_instance=null;}
        private void Update()
        {
            if(!_visible)return;
            if(!WorldRuntimeGate.Ready){foreach(var ring in _rings.Values)if(ring!=null)ring.enabled=false;return;}
            if(Time.unscaledTime<_next)return;_next=Time.unscaledTime+AntColonySettings.Current.scanInterval;
            var groups=EntityRegistry.All.Select(e=>e.GetComponent<PopulationBehavior>()).Where(p=>p!=null&&p.Active&&!string.IsNullOrEmpty(p.GroupId)).GroupBy(p=>p.GroupId).ToArray();
            var live=new HashSet<string>(groups.Select(g=>g.Key));
            foreach(string obsolete in _rings.Keys.Where(k=>!live.Contains(k)).ToArray()){if(_rings[obsolete]!=null)Destroy(_rings[obsolete].gameObject);_rings.Remove(obsolete);}
            foreach(var group in groups)
            {
                var pop=group.First();var queen=AntColonyMember.QueenFor(pop);var leader=queen!=null?queen.Population:pop.Leader;if(leader==null)continue;
                Vector3 center=leader.transform.position;float radius=queen!=null?queen.Radius:Mathf.Max(AntColonySettings.Current.minimumColonyRadius,leader.GetComponent<EntityGeneController>().GetAttribute("territory_radius"));
                if(!_rings.TryGetValue(group.Key,out var ring)||ring==null)
                {ring=ColonyAppearance.Circle(EcologyQueries.Planet!=null?EcologyQueries.Planet.transform:transform,"范围 "+group.Key,AntColonySettings.Current.overlayWidth);_rings[group.Key]=ring;}
                ring.enabled=true;ring.positionCount=AntColonySettings.Current.circleSegments;ColonyAppearance.Tint(ring,ColonyAppearance.ColorFor(group.Key));
                Vector3 up=EcologyQueries.Up(center),axis=Vector3.Cross(up,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.Cross(up,Vector3.right).normalized;
                for(int i=0;i<ring.positionCount;i++){var point=EcologyQueries.Surface(center+Quaternion.AngleAxis(i*360f/ring.positionCount,up)*axis*radius);ring.SetPosition(i,ring.transform.InverseTransformPoint(point));}
            }
        }
    }
}
