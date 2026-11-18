// Test host only: deterministic Unity object/transform adapters. Production algorithms are linked unchanged.
using System.Reflection;
using System.Text.Json;
using N=System.Numerics;
namespace UnityEngine
{
 public class Object
 {
  private static int _next;private readonly int _id=++_next;public int GetInstanceID()=>_id;
  public static void DestroyImmediate(Object value){if(value is GameObject go)go.SetActive(false);}
 }
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();}
 public class MonoBehaviour:Component {public bool enabled=true;public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;}
 public class SphereCollider:Component{}
 public class Mesh:Object{} public class MeshCollider:Component{public bool enabled=true;public Mesh sharedMesh;}
 public struct Color{} public struct Bounds{}
 [AttributeUsage(AttributeTargets.Class)]public class DefaultExecutionOrderAttribute:Attribute{public DefaultExecutionOrderAttribute(int value){}}
 public class GameObject:Object
 {
  private readonly Dictionary<Type,Component> _components=new();public readonly Transform transform=new();public bool activeInHierarchy=true;
  public GameObject(string name){}
  public T AddComponent<T>() where T:Component,new()
  {
   if(typeof(T)==typeof(DarwinPark.Entities.DarwinEntity)&&GetComponent<DarwinPark.Entities.EntityGeneController>()==null)AddComponent<DarwinPark.Entities.EntityGeneController>();
   var result=new T{gameObject=this};_components[typeof(T)]=result;
   if(result is DarwinPark.Entities.DarwinEntity entity)DarwinPark.Entities.EntityRegistry.Register(entity);
   return result;
  }
  public T GetComponent<T>() where T:class=>_components.TryGetValue(typeof(T),out var result)?result as T:null;
  public void SetActive(bool active){activeInHierarchy=active;var e=GetComponent<DarwinPark.Entities.DarwinEntity>();if(e!=null){if(active)DarwinPark.Entities.EntityRegistry.Register(e);else DarwinPark.Entities.EntityRegistry.Unregister(e);}}
 }
 public readonly struct Vector2{public readonly float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public readonly struct Vector3
 {
  public readonly float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  internal N.Vector3 Native=>new(x,y,z);internal static Vector3 From(N.Vector3 v)=>new(v.X,v.Y,v.Z);
  public static Vector3 zero=>default;public static Vector3 one=>new(1,1,1);public static Vector3 right=>new(1,0,0);public static Vector3 up=>new(0,1,0);public static Vector3 down=>new(0,-1,0);public static Vector3 forward=>new(0,0,1);
  public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>MathF.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude>0?this/magnitude:zero;
  public static Vector3 operator +(Vector3 a,Vector3 b)=>From(a.Native+b.Native);public static Vector3 operator -(Vector3 a,Vector3 b)=>From(a.Native-b.Native);
  public static Vector3 operator *(Vector3 a,float b)=>From(a.Native*b);public static Vector3 operator /(Vector3 a,float b)=>From(a.Native/b);
  public static bool operator ==(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<1e-10f;public static bool operator !=(Vector3 a,Vector3 b)=>!(a==b);
  public override bool Equals(object o)=>o is Vector3 v&&this==v;public override int GetHashCode()=>HashCode.Combine(x,y,z);
  public static float Dot(Vector3 a,Vector3 b)=>N.Vector3.Dot(a.Native,b.Native);
  public static Vector3 operator -(Vector3 a)=>From(-a.Native);public static Vector3 operator *(float v,Vector3 a)=>a*v;
  public static Vector3 Cross(Vector3 a,Vector3 b)=>From(N.Vector3.Cross(a.Native,b.Native));public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;
  public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Math.Clamp(t,0,1);
  public static float Angle(Vector3 a,Vector3 b)=>MathF.Acos(Math.Clamp(Dot(a.normalized,b.normalized),-1,1))*180/MathF.PI;
  public static Vector3 Slerp(Vector3 a,Vector3 b,float t)
  {float dot=Math.Clamp(Dot(a.normalized,b.normalized),-1,1),angle=MathF.Acos(dot);if(angle<1e-4f)return Lerp(a,b,t).normalized;return (a*MathF.Sin((1-t)*angle)+b*MathF.Sin(t*angle))/MathF.Sin(angle);}
  public static Vector3 RotateTowards(Vector3 a,Vector3 b,float radians,float magnitude)=>Slerp(a,b,MathF.Min(1,radians/MathF.Max(1e-6f,Angle(a,b)*MathF.PI/180)));
 }
 public readonly struct Vector3Int:IEquatable<Vector3Int>
 {
  public readonly int x,y,z;public Vector3Int(int x,int y,int z){this.x=x;this.y=y;this.z=z;}
  public bool Equals(Vector3Int b)=>x==b.x&&y==b.y&&z==b.z;public override bool Equals(object b)=>b is Vector3Int v&&Equals(v);public override int GetHashCode()=>HashCode.Combine(x,y,z);
  public static bool operator ==(Vector3Int a,Vector3Int b)=>a.Equals(b);public static bool operator !=(Vector3Int a,Vector3Int b)=>!a.Equals(b);
 }
 public readonly struct Quaternion
 {
  internal readonly N.Quaternion Native;internal Quaternion(N.Quaternion q){Native=q;}public static Quaternion identity=>new(N.Quaternion.Identity);
  public static Quaternion Inverse(Quaternion q)=>new(N.Quaternion.Inverse(q.Native));public static Vector3 operator *(Quaternion q,Vector3 v)=>Vector3.From(N.Vector3.Transform(v.Native,q.Native));
  public static Quaternion Euler(float x,float y,float z)=>new(N.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));
 }
 public readonly struct Matrix4x4
 {
  internal readonly N.Matrix4x4 Native;internal Matrix4x4(N.Matrix4x4 m){Native=m;}public static Matrix4x4 identity=>new(N.Matrix4x4.Identity);
  public Vector3 MultiplyPoint3x4(Vector3 p)=>Vector3.From(N.Vector3.Transform(p.Native,Native));
  public static bool operator ==(Matrix4x4 a,Matrix4x4 b)=>a.Native==b.Native;public static bool operator !=(Matrix4x4 a,Matrix4x4 b)=>!(a==b);
  public override bool Equals(object o)=>o is Matrix4x4 m&&this==m;public override int GetHashCode()=>Native.GetHashCode();
 }
 public class Transform
 {
  public Vector3 position;public Quaternion rotation=Quaternion.identity;public Vector3 localScale=Vector3.one;
  public Matrix4x4 localToWorldMatrix=>new(N.Matrix4x4.CreateScale(localScale.Native)*N.Matrix4x4.CreateFromQuaternion(rotation.Native)*N.Matrix4x4.CreateTranslation(position.Native));
  public Matrix4x4 worldToLocalMatrix{get{N.Matrix4x4.Invert(localToWorldMatrix.Native,out var result);return new(result);}}
  public Vector3 InverseTransformPoint(Vector3 p)=>worldToLocalMatrix.MultiplyPoint3x4(p);public Vector3 TransformPoint(Vector3 p)=>localToWorldMatrix.MultiplyPoint3x4(p);
  public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}
  public Vector3 lossyScale=>localScale;public Vector3 TransformDirection(Vector3 v)=>rotation*v;public Vector3 InverseTransformDirection(Vector3 v)=>Quaternion.Inverse(rotation)*v;
  public Vector3 TransformVector(Vector3 v)=>Vector3.From(N.Vector3.TransformNormal(v.Native,localToWorldMatrix.Native));public Vector3 InverseTransformVector(Vector3 v)=>Vector3.From(N.Vector3.TransformNormal(v.Native,worldToLocalMatrix.Native));
 }
 public static class Mathf
 {
  public const float PI=MathF.PI,Deg2Rad=MathF.PI/180,Rad2Deg=180/MathF.PI;
  public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
  public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
  public static float Sin(float v)=>MathF.Sin(v);public static float Cos(float v)=>MathF.Cos(v);public static float Sqrt(float v)=>MathF.Sqrt(v);
  public static float Abs(float v)=>MathF.Abs(v);public static float Atan2(float a,float b)=>MathF.Atan2(a,b);public static float Asin(float v)=>MathF.Asin(v);
  public static float Repeat(float v,float length)=>v-MathF.Floor(v/length)*length;
  public static float Lerp(float a,float b,float t)=>a+(b-a)*Math.Clamp(t,0,1);public static int CeilToInt(float v)=>(int)MathF.Ceiling(v);
  public static int FloorToInt(float v)=>(int)MathF.Floor(v);public static float Floor(float v)=>MathF.Floor(v);
  public static float Max(float a,float b)=>MathF.Max(a,b);public static float Min(float a,float b)=>MathF.Min(a,b);public static float Clamp01(float v)=>Math.Clamp(v,0,1);
 }
 public static class Time {public static int frameCount;public static float time,unscaledTime;public static float fixedDeltaTime=.02f,deltaTime=.016f;public static double realtimeSinceStartupAsDouble=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;}
 public static class JsonUtility
 {
  private static readonly JsonSerializerOptions Options=new(){IncludeFields=true};
  public static string ToJson(object v)=>JsonSerializer.Serialize(v,v.GetType(),Options);public static T FromJson<T>(string v)=>JsonSerializer.Deserialize<T>(v,Options);
 }
 public enum RuntimeInitializeLoadType{SubsystemRegistration}
 [AttributeUsage(AttributeTargets.Method)]public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
}
namespace DarwinPark.Core
{
 public static class ServiceLocator {public static bool TryGet<T>(out T value){value=default;return false;}}
 public static class WorldRuntimeGate{public static bool Ready=true;}
 public class SimulationClock{public float GameTime;}
 public class SimulationOptimization{public static SimulationOptimization Current=new();public bool localCompetition=true;public float decisionSeconds=.25f;}
 public static class PerformanceTimings
 {
  public enum Area{CoordinateGrid,Neighborhood,Perception,LandPathSearch,LandRouteFollow,ShoreGuard}public readonly struct Scope:IDisposable{public void Dispose(){}}public static Scope Measure(Area a)=>default;
 }
}
namespace DarwinPark.Genetics
{
 public class Genome{public List<Gene> Genes=new();}public class Gene{public string DefinitionId;}
 public class State{public string StateId;}public class Evaluation{public State State;}
 public class GenomeEvaluation{public Dictionary<string,float> Attributes=new();}
 public class ObservationSettings
 {
  public string geneId="gene_observation",attentionState="attention",rangeAttribute="visionRange";
  public float attentionRangeMultiplier=.8f,wideRangeMultiplier=2,attentionRefreshSeconds=.1f,wideRefreshSeconds=.6f,coordinateCellSize=64;
  public int maximumCellsPerQuery=4096,coordinateRevisionCellLimit=65536,perceptionScansPerFrame=64,maximumRememberedTargets=256;
  public float perceptionBudgetMilliseconds=1.5f,minimumRealRefreshSeconds=.05f,memorySeconds=5;
  private static ObservationSettings _current=new();public static ObservationSettings Current=>_current;
 }
}
namespace DarwinPark.Planet
{
 public class PlanetHydrology{public class Config{public float riverHalfWidth=1;}public Config Settings=new();public float Water(UnityEngine.Vector3 direction)=>.5f;}
 public class PlanetTerrainMeshBuilder:UnityEngine.MonoBehaviour{public float radius=100,heightScale=20;public PlanetTerrainData Data=new(){Radius=100};public UnityEngine.Mesh GeneratedMesh;}

}
namespace DarwinPark.Entities
{
 public class EntityGeneController:UnityEngine.MonoBehaviour
 {
  public DarwinPark.Genetics.Genome Genome=new();public DarwinPark.Genetics.GenomeEvaluation Evaluation=new();public int LogicRevision;
  public float GetAttribute(string id)=>Evaluation.Attributes.TryGetValue(id,out float v)?v:0;
  public void Recalculate(){Evaluation=new();LogicRevision++;Ecology.EntityNeighborhood.Changed(this);}
 }
 public class DarwinEntity:UnityEngine.MonoBehaviour{public EntityGeneController Genes=>GetComponent<EntityGeneController>();}
 public static class EntityRegistry
 {
  private static readonly HashSet<DarwinEntity> Entities=new();public static long MembershipRevision;public static IReadOnlyCollection<DarwinEntity> All=>Entities;
  public static void Register(DarwinEntity e){if(Entities.Add(e)){MembershipRevision++;Ecology.EntityNeighborhood.MembershipChanged(e,false);}}
  public static void Unregister(DarwinEntity e){if(Entities.Remove(e)){MembershipRevision++;Ecology.EntityNeighborhood.MembershipChanged(e,true);Ecology.EntityPerceptionHub.Remove(e.Genes);}}
 }
}
namespace DarwinPark.Entities.Stats
{
 public class EntityStats:UnityEngine.MonoBehaviour
 {
  private float _health=100;public bool TryGet(string id,out float v){v=_health;return id=="health";}
  public void Modify(string id,float delta,bool flash){_health=Math.Clamp(_health+delta,0,100);DarwinPark.Entities.Ecology.EntityNeighborhood.Changed(GetComponent<DarwinPark.Entities.EntityGeneController>());}
 }
}
namespace DarwinPark.Entities.Ecology
{
 public class EcologyBalanceSettings{public static EcologyBalanceSettings Current=new();public float competitionRadius=10,competitionPerNeighbor=.2f;}
 public static class EcologyQueries
 {
  public static DarwinPark.Planet.PlanetTerrainMeshBuilder Planet;
  public static UnityEngine.Vector3 ToLocal(UnityEngine.Vector3 p)=>Planet!=null?Planet.transform.InverseTransformPoint(p):p;
  public static DarwinPark.Genetics.Evaluation Active(EntityGeneController g,string id)=>null;
 }
 public static class ProducerClassification{public static bool IsProducer(EntityGeneController g)=>g.GetAttribute("producer")>0;public static void Remove(EntityGeneController g){}public static void Clear(){}}
 public static class EcologyMotion{public static bool Detectable(DarwinEntity target,UnityEngine.Vector3 origin,float range,DarwinEntity self)=>(target.transform.position-origin).sqrMagnitude<=range*range;}
}
