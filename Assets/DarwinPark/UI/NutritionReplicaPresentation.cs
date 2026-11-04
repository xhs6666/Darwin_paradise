using System.Collections.Generic;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：客户端只展示服务器寄生状态，不能本地扣宿主营养或孵化第二份后代。
    public sealed class NutritionReplicaPresentation:MonoBehaviour
    {
        public bool attached;public int parasites,foreignEggs;
        private readonly Dictionary<Renderer,bool> _renderers=new Dictionary<Renderer,bool>();
        private readonly Dictionary<Collider,bool> _colliders=new Dictionary<Collider,bool>();
        public static void Apply(DarwinEntity entity,EcologySaveState state)
        {
            var view=entity.GetComponent<NutritionReplicaPresentation>()??entity.gameObject.AddComponent<NutritionReplicaPresentation>();
            bool hidden=!string.IsNullOrEmpty(state?.nutritionParasite?.host);view.parasites=state?.nutritionParasiteCount??0;view.foreignEggs=state?.foreignEggs?.Count??0;
            if(view.attached==hidden)return;view.attached=hidden;
            // ★ 适配：副本的隐藏与不可点选成对恢复，不能在成员电脑点到宿主体内的不可见实体。
            if(hidden)
            {
                foreach(var renderer in entity.GetComponentsInChildren<Renderer>(true)){view._renderers[renderer]=renderer.enabled;renderer.enabled=false;}
                foreach(var collider in entity.GetComponentsInChildren<Collider>(true)){view._colliders[collider]=collider.enabled;collider.enabled=false;}
            }
            else
            {
                foreach(var item in view._renderers)if(item.Key!=null)item.Key.enabled=item.Value;view._renderers.Clear();
                foreach(var item in view._colliders)if(item.Key!=null)item.Key.enabled=item.Value;view._colliders.Clear();
            }
        }
    }
}
