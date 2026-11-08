using DarwinPark.Core;
using DarwinPark.Planet;
using UnityEngine;
namespace DarwinPark.Entities
{
    // ★ 新增：只复用同地形、同位置附近且没有起跳/下落的接地结果；传送、重建和星球移动立即失效。
    public sealed class GravityProbeCache
    {
        private EntityGeneController _genes;private Transform _planet;private MeshCollider _ground;private PlanetTerrainMeshBuilder _builder;
        private PlanetTerrainData _data;private Matrix4x4 _matrix;private Vector3 _position;private RaycastHit _hit;private float _next,_radius,_height;private bool _found,_ready;private float _friction=float.NaN;
        public long Raycasts {get;private set;}
        public EntityGeneController Genes(Component owner){if(_genes==null)_genes=owner.GetComponent<EntityGeneController>();return _genes;}
        public bool UnderWater(Transform planet,Vector3 point){Bind(planet);return _builder!=null&&_builder.IsSubmerged(point);}
        private void Bind(Transform planet){if(_planet==planet&&_builder!=null&&_ground!=null)return;_planet=planet;_ground=planet!=null?planet.GetComponent<MeshCollider>():null;_builder=planet!=null?planet.GetComponent<PlanetTerrainMeshBuilder>():null;_ready=false;}
        public bool Probe(Transform planet,Rigidbody body,Vector3 direction,out RaycastHit hit)
        {
            Bind(planet);var matrix=planet.localToWorldMatrix;var data=_builder?.Data;float radius=_builder!=null?_builder.radius:0,height=_builder!=null?_builder.heightScale:0;
            var tuning=SimulationOptimization.Current;bool gentle=Mathf.Abs(Vector3.Dot(body.linearVelocity,direction))<tuning.groundProbeVerticalSpeed;
            if(!_ready||Time.fixedTime>=_next||!gentle||(body.position-_position).sqrMagnitude>tuning.groundProbeMoveTolerance*tuning.groundProbeMoveTolerance||matrix!=_matrix||!ReferenceEquals(data,_data)||radius!=_radius||height!=_height)
            {
                _found=_ground!=null&&_ground.Raycast(new Ray(body.position,direction),out _hit,20);Raycasts++;_position=body.position;_matrix=matrix;_data=data;_radius=radius;_height=height;_next=Time.fixedTime+SimulationOptimization.Current.groundProbeSeconds;_ready=true;
            }
            hit=_hit;if(_found)hit.distance=Mathf.Max(0,Vector3.Dot(body.position-_hit.point,-direction));return _found;
        }
        public void Friction(PhysicsMaterial material,float value)
        {if(material==null||value==_friction)return;_friction=value;material.staticFriction=material.dynamicFriction=value;material.frictionCombine=PhysicsMaterialCombine.Minimum;}
    }
}
