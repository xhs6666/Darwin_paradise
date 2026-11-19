using System;
using System.Collections.Generic;
using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：每个实体仅持有路径游标；A*工作数组和真实时间预算由全局共享，避免500份寻路数组。
    [DefaultExecutionOrder(700)]public sealed class LandPathScheduler:MonoBehaviour
    {
        private struct Request {public LandRoutePlan route;public int serial,start,goal;}
        private readonly Queue<Request> _queue=new();private readonly Dictionary<(int,int),int[]> _cache=new();private readonly SurfaceMinHeap _open=new();private LandNavigationGraph _graph;private Request _job;private bool _busy;private int _stamp;private int[] _seen,_closed,_parent;private float[] _cost;
        public static LandPathScheduler Instance{get;private set;}public static long Searches,CacheHits,Expanded;
        public static void Configure(PlanetTerrainMeshBuilder planet)
        {var owner=planet.GetComponent<LandPathScheduler>()??planet.gameObject.AddComponent<LandPathScheduler>();Instance=owner;owner._graph=planet.Data.Navigation;owner._queue.Clear();owner._cache.Clear();owner._busy=false;if(owner._graph==null)return;int n=owner._graph.Points.Length;owner._seen=new int[n];owner._closed=new int[n];owner._parent=new int[n];owner._cost=new float[n];}
        public void RequestPath(LandRoutePlan route,int serial,int start,int goal)
        {if(_graph==null||start<0||goal<0){route.Complete(serial,null);return;}if(_cache.TryGetValue((start,goal),out var path)){CacheHits++;route.Complete(serial,path);return;}_queue.Enqueue(new Request{route=route,serial=serial,start=start,goal=goal});}
        private void Begin(Request request)
        {_job=request;_busy=true;_open.Clear();if(++_stamp==int.MaxValue){Array.Clear(_seen,0,_seen.Length);Array.Clear(_closed,0,_closed.Length);_stamp=1;}_seen[request.start]=_stamp;_cost[request.start]=0;_parent[request.start]=-1;_open.Push(request.start,0);Searches++;}
        private void Finish(int[] path)
        {if(path!=null){if(_cache.Count>=_graph.Settings.routeCacheCount)_cache.Clear();_cache[(_job.start,_job.goal)]=path;}_job.route.Complete(_job.serial,path);_busy=false;}
        private void LateUpdate()
        {
            // ★ 新增：采样可直接识别全局A*工作队列，而非笼统归到实体行动。
            using var measuredCost=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.LandPathSearch);
            if(!DarwinPark.Core.WorldRuntimeGate.Ready||_graph==null)return;double began=Time.realtimeSinceStartupAsDouble;int budget=Mathf.Max(1,_graph.Settings.expansionsPerFrame);
            while(budget>0&&(Time.realtimeSinceStartupAsDouble-began)*1000<Mathf.Max(.1f,_graph.Settings.pathMilliseconds))
            {
                if(!_busy){if(_queue.Count==0)break;var request=_queue.Dequeue();if(!request.route.IsCurrent(request.serial))continue;if(_graph.Regions[request.start]!=_graph.Regions[request.goal]){request.route.Complete(request.serial,null);continue;}
                    if(_graph.Segment(_graph.Directions[request.start],_graph.Directions[request.goal])){var direct=new[]{request.start,request.goal};if(_cache.Count>=_graph.Settings.routeCacheCount)_cache.Clear();_cache[(request.start,request.goal)]=direct;request.route.Complete(request.serial,direct);continue;}Begin(request);}
                if(!_job.route.IsCurrent(_job.serial)){_busy=false;continue;}if(_open.Count==0){Finish(null);continue;}int node=_open.Pop();if(_closed[node]==_stamp)continue;_closed[node]=_stamp;budget--;Expanded++;
                if(node==_job.goal){var path=new List<int>();for(int i=node;i>=0;i=_parent[i])path.Add(i);path.Reverse();Finish(path.ToArray());continue;}
                for(int e=0;e<8;e++)if(_graph.Linked(node,e)){int next=_graph.Neighbour(node,e);if(_closed[next]==_stamp)continue;float cost=_cost[node]+Vector3.Distance(_graph.Points[node],_graph.Points[next]);if(_seen[next]==_stamp&&cost>=_cost[next])continue;_seen[next]=_stamp;_cost[next]=cost;_parent[next]=node;_open.Push(next,cost+Vector3.Distance(_graph.Points[next],_graph.Points[_job.goal]));}
            }
        }
        private void OnDestroy(){if(Instance==this)Instance=null;}
    }
    // ★ 新增：只在目标地表格改变时请求路径，待算时停步，岸边速度再验证避免避让把实体推入海中。
    public sealed class LandRoutePlan
    {
        private LandNavigationGraph _graph;private int[] _path;private int _cursor,_goal=-1,_serial;private bool _pending,_failed;private float _retry;
        private Vector3 _verifiedStart,_verifiedEnd;private bool _verified;
        public static long SegmentValidations,SegmentReuses;
        public void Invalidate(){_verified=false;_failed=true;_retry=Time.unscaledTime+(_graph?.Settings.repathSeconds??0);}
        // ✦ 修复（Bug回归）：已验证路段在同一大圆走廊内复用；碰撞推离或航点改变必须重验。
        private bool Traversable(Vector3 a,Vector3 b)
        {
            float radius=_graph.Terrain.Radius+_graph.Terrain.SeaLevel*_graph.Terrain.HeightScale;
            if(_verified&&b==_verifiedEnd)
            {
                var delta=b-_verifiedStart;float length=delta.sqrMagnitude;
                var normal=Vector3.Cross(_verifiedStart,b);float t=Vector3.Dot(a-_verifiedStart,delta)/Mathf.Max(1e-12f,length);
                float cross=normal.sqrMagnitude>1e-12f?Mathf.Abs(Vector3.Dot(a,normal.normalized))*radius:Vector3.Distance(a,_verifiedStart)*radius;
                if(t>=0&&t<=1&&cross<=_graph.Settings.routeDeviationTolerance&&_graph.Dry(a)){SegmentReuses++;return true;}
            }
            SegmentValidations++;_verified=_graph.Segment(a,b);_verifiedStart=a;_verifiedEnd=b;return _verified;
        }
        public bool IsCurrent(int serial)=>serial==_serial;
        public void Complete(int serial,int[] path){if(!IsCurrent(serial))return;_path=path;_cursor=0;_pending=false;_failed=path==null;_verified=false;_retry=Time.unscaledTime+(_graph?.Settings.failureRetrySeconds??2);}
        public bool Resolve(PlanetTerrainMeshBuilder planet,Vector3 current,Vector3 destination,out Vector3 waypoint)
        {
            using var measuredCost=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.LandRouteFollow);
            waypoint=current;var graph=planet?.Data?.Navigation;if(graph==null)return false;var a=PlanetTerrainSampler.WorldToLocalDirection(planet.transform,current);var b=PlanetTerrainSampler.WorldToLocalDirection(planet.transform,destination);
            // Nearby, unobstructed formation and patrol targets can advance while long paths are queued.
            float localReach=graph.Settings.waypointArrival*4/graph.Terrain.Radius;
            bool sameGraph=_graph==graph;
            if((a-b).sqrMagnitude<localReach*localReach&&(sameGraph?Traversable(a,b):graph.Segment(a,b)))
            {
                if(!sameGraph||_pending)
                {
                    _serial++;_pending=false;_goal=-1;
                    _graph=graph;_verified=true;_verifiedStart=a;_verifiedEnd=b;
                }
                waypoint=destination;
                return true;
            }
            // ★ 修复（防Bug回归）：同一粗格可同时覆盖岸和浅水；缓存路径也必须核对实际目标。
            if (!graph.Dry(b)) return false;
            int goal=graph.Cell(b);
            if(_graph!=graph||_goal!=goal||_failed&&Time.unscaledTime>=_retry){_graph=graph;_goal=goal;_serial++;_verified=false;_path=null;_pending=true;_failed=false;if(LandPathScheduler.Instance!=null)LandPathScheduler.Instance.RequestPath(this,_serial,graph.Closest(a),graph.Closest(b));else Complete(_serial,null);}
            if(_pending||_failed||_path==null)return false;
            while(_cursor<_path.Length&&planet.transform.TransformVector(a-graph.Directions[_path[_cursor]]).magnitude*(planet.Data.Radius+planet.Data.SeaLevel*planet.Data.HeightScale)<graph.Settings.waypointArrival)_cursor++;
            if(_cursor>=_path.Length){if(!Traversable(a,b)){_failed=true;_retry=Time.unscaledTime+graph.Settings.repathSeconds;return false;}waypoint=destination;return true;}
            // ★ 修复（防Bug回归）：实际位置可能被碰撞推离旧路，不能沿缓存路点从湖面抄近路。
            if(!Traversable(a,graph.Directions[_path[_cursor]])){_failed=true;_retry=Time.unscaledTime+graph.Settings.repathSeconds;return false;}
            waypoint=planet.transform.TransformPoint(graph.Points[_path[_cursor]]);return true;
        }
        public static Vector3 SafeVelocity(PlanetTerrainMeshBuilder planet, Vector3 point, Vector3 velocity, float bodyRadius = 0)
        {
            if (velocity.sqrMagnitude < 1e-8f) return velocity;
            var graph = planet?.Data?.Navigation;
            if (graph == null) return Vector3.zero;

            // ★ 适配：一次坐标变换共用起点、终点和侧向检查；预测距离只用当前物理步。
            var transform = planet.transform;
            return SafeVelocity(graph,transform.worldToLocalMatrix,transform.localToWorldMatrix,point,velocity,bodyRadius);
        }

        // ★ 适配：批量重力复用星球变换，同一套岸线、坡度和身体侧向检查继续用于精确路径。
        public static Vector3 SafeVelocity(LandNavigationGraph graph,in Matrix4x4 toLocal,in Matrix4x4 toWorld,
            Vector3 point,Vector3 velocity,float bodyRadius,float? localSweepRadius=null)
        {
            using var measuredCost = DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.ShoreGuard);
            if (graph == null) return Vector3.zero;
            var start = toLocal.MultiplyPoint3x4(point);
            var motion = toLocal.MultiplyVector(velocity);
            var end = start + motion * Mathf.Max(.0001f, Time.fixedDeltaTime);
            // ★ 适配：尝试整段扫掠与精确回退共用局部起终点，接缝/近水拒绝后不重复转换。
            if(localSweepRadius.HasValue&&graph.ContainsDryMotionSweep(start,end,localSweepRadius.Value))return velocity;
            if (!graph.Segment(start, end)) return Vector3.zero;
            if (bodyRadius <= 0) return velocity;

            // 身体两侧也要留在岸上，防止中心仍在陆地时碰撞体横跨浅滩。
            var axis = Vector3.Cross(end, motion).normalized;
            var side = axis * (bodyRadius / Mathf.Max(.0001f, toWorld.MultiplyVector(axis).magnitude));
            return graph.Dry((end + side).normalized) && graph.Dry((end - side).normalized)
                ? velocity : Vector3.zero;
        }
    }
}
