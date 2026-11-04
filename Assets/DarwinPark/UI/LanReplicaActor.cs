using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：客户端实体是只读副本；基因仅在表达变化时重算，位置插值，避免每个网络帧重建所有行为。
    [DefaultExecutionOrder(100)]
    public sealed class LanReplicaActor : MonoBehaviour
    {
        private string _genome,_role;private bool _initialized;private Vector3 _point;private Quaternion _rotation;
        public static void Freeze(DarwinEntity entity)
        {
            foreach(var component in entity.GetComponents<MonoBehaviour>())
                if(!(component is DarwinEntity||component is EntityStats||component is EntityGeneController||component is PopulationBehavior||component is AntColonyMember||component is ColonyMemberMarker||component is EcologyIdentity||component is EntityStateController||component is EntityManualControl||component is RtsOrderController||component is LanReplicaActor))component.enabled=false;
            var body=entity.GetComponent<Rigidbody>();if(body!=null){body.isKinematic=true;body.useGravity=false;}
        }
        public void Apply(LanActor incoming)
        {
            var entity=GetComponent<DarwinEntity>();var saved=incoming.state;var genes=entity.Genes;string genome=JsonUtility.ToJson(saved.genome);
            bool expression=genome!=_genome||genes.CurrentAge!=saved.age||genes.IsGestating!=saved.gestating||Mathf.Abs(genes.EnvironmentGeneSuppression-incoming.suppression)>.001f;
            if(expression){genes.Genome=saved.genome;genes.EnvironmentGeneSuppression=incoming.suppression;genes.IsGestating=saved.gestating;genes.RestoreStageState(saved.age,saved.mutatedStages,saved.stageChecks);genes.Recalculate();_genome=genome;}
            GetComponent<EntityStats>().RestoreValues(saved.stats);entity.DisplayName=saved.name;transform.localScale=saved.scale;
            string role=(saved.ecology?.groupId??"")+"|"+(saved.ecology?.ant?.role??"");
            if(!_initialized||role!=_role){saved.ecology?.Restore(entity);_role=role;}
            if(saved.ecology?.rts!=null)RtsOrderController.Ensure(entity).Restore(saved.ecology.rts);
            if(saved.ecology!=null)GetComponent<EntityStateController>()?.Restore(saved.ecology.entityStateId);
            _point=saved.position;_rotation=saved.rotation;if(!_initialized||Vector3.Distance(transform.position,_point)>40)transform.SetPositionAndRotation(_point,_rotation);
            _initialized=true;Freeze(entity);
            // ★ 适配：冻结行为之后再展示权威寄生状态，不能在成员电脑吸收或孵化。
            NutritionReplicaPresentation.Apply(entity,saved.ecology);
            if(expression){var inspector=FindFirstObjectByType<GeneInspectorUI>();if(inspector?.CurrentEntity==entity)inspector.ShowEntityInfo(entity);}
        }
        private void LateUpdate(){if(WorldAuthority.IsReplica&&_initialized)transform.SetPositionAndRotation(Vector3.Lerp(transform.position,_point,1-Mathf.Exp(-Time.unscaledDeltaTime*16)),Quaternion.Slerp(transform.rotation,_rotation,1-Mathf.Exp(-Time.unscaledDeltaTime*16)));}
    }
}
