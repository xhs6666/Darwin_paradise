using System.Collections.Generic;
using DarwinPark.Core;
using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：全部能量块共用一次低频外观调度；远处关闭逐块点光源，营养和拾取仍完整保留。
    [DefaultExecutionOrder(250)]public sealed class ResourceVisualScheduler:MonoBehaviour
    {
        private static ResourceVisualScheduler _instance;private static readonly HashSet<EnergyBlockGradeVisual> Items=new();private float _next;
        public static void Register(EnergyBlockGradeVisual item)
        {Items.Add(item);if(_instance==null){_instance=Object.FindFirstObjectByType<ResourceVisualScheduler>();if(_instance==null)new GameObject("ResourceVisualScheduler").AddComponent<ResourceVisualScheduler>();}}
        public static void Remove(EnergyBlockGradeVisual item)=>Items.Remove(item);
        private void Awake(){if(_instance!=null&&_instance!=this){Destroy(gameObject);return;}_instance=this;}
        private void OnDestroy(){if(_instance==this)_instance=null;}
        private void LateUpdate()
        {
            if(Time.unscaledTime<_next)return;_next=Time.unscaledTime+SimulationOptimization.Current.visualRefreshSeconds;var camera=Camera.main;Vector3 point=camera!=null?camera.transform.position:Vector3.zero;float distance=SimulationOptimization.Current.lightDistance;
            foreach(var item in Items)if(item!=null&&item.isActiveAndEnabled){item.Refresh();item.SetLightVisible(camera==null||(item.transform.position-point).sqrMagnitude<=distance*distance);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_instance=null;Items.Clear();}
    }
}
