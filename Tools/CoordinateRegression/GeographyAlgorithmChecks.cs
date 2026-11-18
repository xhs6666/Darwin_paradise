using System.Reflection;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Planet;
using UnityEngine;

public static class GeographyAlgorithmChecks
{
 private static readonly List<string> Passed=new();
 private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Passed.Add(text);}
 private static PlanetTerrainData Make(Func<Vector3,float> height)
 {
  const int n=32;int stride=n+1,faceSize=stride*stride;
  var d=new PlanetTerrainData{Subdivisions=n,Radius=100,HeightScale=20,SeaLevel=.5f,FaceHeights=new float[6][],Vertices=new Vector3[6*faceSize],Triangles=new int[36*n*n]};
  for(int f=0;f<6;f++)
  {
   d.FaceHeights[f]=new float[faceSize];
   for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
   {
    float u=2f*x/n-1,v=2f*y/n-1;var dir=(f<2?new Vector3(f==0?1:-1,v,u):f<4?new Vector3(u,f==2?1:-1,v):new Vector3(u,v,f==4?1:-1)).normalized;
    float h=height(dir);d.FaceHeights[f][y*stride+x]=h;d.Vertices[f*faceSize+y*stride+x]=dir*(100+h*20);
   }
   for(int y=0;y<n;y++)for(int x=0;x<n;x++)
   {
    int a=f*faceSize+y*stride+x,b=a+1,c=a+stride,e=c+1,t=(f*n*n+y*n+x)*6;
    d.Triangles[t]=a;d.Triangles[t+1]=c;d.Triangles[t+2]=b;d.Triangles[t+3]=b;d.Triangles[t+4]=c;d.Triangles[t+5]=e;
   }
  }
  d.Navigation=new LandNavigationGraph(d,new LandNavigationSettings{longitudeCells=64,latitudeCells=32});return d;
 }
 public static IReadOnlyList<string> Run(PlanetTerrainMeshBuilder planet)
 {
  var data=Make(d=>MathF.Abs(d.x)<.14f&&MathF.Abs(d.y)<.14f&&d.z>.9f?.3f:.7f);planet.Data=data;
  var graph=data.Navigation;var a=new Vector3(-.35f,0,1).normalized;var b=new Vector3(.35f,0,1).normalized;
  Check(!graph.Segment(a,b)&&graph.Connected(a,b),"A pond blocks straight movement while its banks stay connected");
  Check(graph.Closest(Vector3.forward)<0,"An ocean destination cannot snap onto a dry route");
  var water=PlanetCoordinate.At(planet,Vector3.forward*110.5f);
  Check(water.IsWater&&water.Immersion<0&&water.WaterDepth>3.9f,"Geography separates water depth from body-center immersion");
  var land=PlanetCoordinate.At(planet,a*115);Check(!land.IsWater&&land.Altitude>0,"Land geography uses actual triangle surfaces");
  LandPathScheduler.Configure(planet);var scheduler=planet.GetComponent<LandPathScheduler>();var update=typeof(LandPathScheduler).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
  var route=new LandRoutePlan();Vector3 current=a*114,target=b*114;int steps=0;
  while(Vector3.Distance(current,target)>2&&steps++<600)
  {
   Time.frameCount++;Time.unscaledTime+=.016f;update.Invoke(scheduler,null);
   if(route.Resolve(planet,current,target,out var waypoint))
   {current=Vector3.RotateTowards(current.normalized,waypoint.normalized,.02f,0).normalized*114;Check(graph.Dry(current.normalized),"A route-following step stays on land");}
  }
  Check(Vector3.Distance(current,target)<=2,"Shared budgeted A* reaches a target around a pond");
  Check(LandRoutePlan.SafeVelocity(planet,Vector3.forward*110,Vector3.right*20)==Vector3.zero,"Shore guard blocks underwater land propulsion");
  Check(LandRoutePlan.SegmentReuses>0,"Normal route following reuses validated segments");
  var displaced=a*114;bool found=route.Resolve(planet,displaced,target,out var next);
  Check(!found||graph.Segment(a,next.normalized),"Displacement cannot reuse a route across water");
  var islands=Make(d=>MathF.Abs(d.y)<.2f?.3f:.7f);
  Check(!islands.Navigation.Connected(Vector3.up,Vector3.down),"An ocean separates disconnected continents");
  // ★ 修复（防Bug回归）：合法上坡物理步只有毫米长度；acos(dot) 舍入为零不能将其误判为悬崖。
  var slope=Make(d=>.7f+2*d.x);planet.Data=slope;
  foreach(float speed in new[]{.05f,.5f,2f})
  {
   var start=Vector3.forward*114;var velocity=Vector3.right*speed;
   Check(slope.Navigation.Segment(start.normalized,(start+velocity*Time.fixedDeltaTime).normalized),"A short uphill physics step remains traversable at speed "+speed);
   Check(LandRoutePlan.SafeVelocity(planet,start,velocity,.5f)==velocity,"Shore guard preserves legal uphill start velocity "+speed);
  }
  planet.Data=data;
  var s1=PlanetCoordinate.AtLocal(data,PlanetTerrainSampler.DirectionFromUV(.00001f,.5f)*115);
  var s2=PlanetCoordinate.AtLocal(data,PlanetTerrainSampler.DirectionFromUV(.99999f,.5f)*115);
  Check(MathF.Abs(s1.WaterDepth-s2.WaterDepth)<.001f,"Longitude seam geography is consistent");
  planet.transform.position=new Vector3(30,-20,15);planet.transform.rotation=Quaternion.Euler(15,67,12);planet.transform.localScale=Vector3.one*2;
  var point=planet.transform.TransformPoint(Vector3.forward*109);var coord=PlanetCoordinate.At(planet,point);
  Check((coord.LocalPosition-Vector3.forward*109).magnitude<.001f&&coord.IsWater,"Planet transforms preserve local geographic coordinates");
  planet.transform.position=Vector3.zero;planet.transform.rotation=Quaternion.identity;planet.transform.localScale=Vector3.one;
  return Passed;
 }
}
