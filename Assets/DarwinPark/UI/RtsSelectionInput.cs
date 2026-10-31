using System.Collections.Generic;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：R切换RTS，左键框选、中键转动、右键移动；扩展命令只显示所选实体基因实际支持的能力。
    [DefaultExecutionOrder(150)]
    public sealed class RtsSelectionInput : MonoBehaviour
    {
        public KeyCode toggleKey=KeyCode.R;
        private readonly List<DarwinEntity> _selected=new List<DarwinEntity>();
        private bool _dragging;private Vector2 _start;private RtsOrderKind _mode=RtsOrderKind.Move;
        public IReadOnlyList<DarwinEntity> Selected=>_selected;
        public static Rect CommandBarRect=>new Rect(12,Screen.height-260,Mathf.Min(580,Screen.width-24),46);
        public static bool IsOverCommandBar(Vector2 screenPoint)=>CommandBarRect.Contains(new Vector2(screenPoint.x,Screen.height-screenPoint.y));
        private void Update()
        {
            var session=LanSession.Instance;if(session==null||!WorldRuntimeGate.Ready||GameInputGate.Blocked){_dragging=false;return;}
            var selected=EventSystem.current?.currentSelectedGameObject;if(selected!=null&&selected.GetComponentInParent<InputField>() is InputField input&&input.isFocused)return;
            if(Input.GetKeyDown(toggleKey))
            {
                bool active=!WorldAuthority.RtsMode;session.SetRtsMode(active);_selected.Clear();
                if(!active&&session.IsClient&&EcologyIdentity.Find(session.OwnedEntityId) is DarwinEntity entity)EntityViewController.Instance.EnterView(entity);
            }
            if(!WorldAuthority.RtsMode)return;if(!session.CanUseRts){session.SetRtsMode(false);_selected.Clear();return;}
            _selected.RemoveAll(e=>!EcologyQueries.Alive(e)||!session.CanCommand(e));
            // ★ 修复（防Bug回归）：IMGUI不在EventSystem射线列表中，点击命令栏必须避免同时清空框选或向地面下令。
            bool over=EntityPlacementUtility.IsPointerOverUI(Input.mousePosition)||IsOverCommandBar(Input.mousePosition);
            if(Input.GetMouseButtonDown(0)&&!over){_dragging=true;_start=Input.mousePosition;}
            if(Input.GetMouseButtonUp(0)&&_dragging){_dragging=false;if(!over)SelectRectangle(_start,Input.mousePosition,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));}
            if(Input.GetMouseButtonDown(1)&&!over&&_selected.Count>0)
            {
                var camera=Camera.main;if(camera==null)return;DarwinEntity target=null;var ray=camera.ScreenPointToRay(Input.mousePosition);
                if(Physics.Raycast(ray,out var hit,camera.farClipPlane,~0,QueryTriggerInteraction.Ignore))target=hit.collider.GetComponentInParent<DarwinEntity>();
                if(!EntityPlacementUtility.TryGetSurface(camera,Input.mousePosition,out var point,out _))return;
                var resource=ColonyResource.All.Where(r=>r.State.kind==ColonyResourceKind.Corpse&&EcologyQueries.Distance(r.transform.position,point)<=LanSettings.Current.rtsPickRadius).OrderBy(r=>(r.transform.position-point).sqrMagnitude).FirstOrDefault();
                var mode=_mode;if(mode==RtsOrderKind.Move&&target!=null&&_selected.Any(e=>EcologyMotion.CanHunt(e.Genes,target)))mode=RtsOrderKind.Attack;
                session.IssueRts(_selected.Select(EcologyIdentity.Of).ToArray(),mode,point,target,resource);_mode=RtsOrderKind.Move;
            }
        }
        public void SelectRectangle(Vector2 start,Vector2 end,bool append=false)
        {
            var session=LanSession.Instance;var camera=Camera.main;if(session==null||camera==null||!session.CanUseRts)return;if(!append)_selected.Clear();
            var rect=Rect.MinMaxRect(Mathf.Min(start.x,end.x),Mathf.Min(start.y,end.y),Mathf.Max(start.x,end.x),Mathf.Max(start.y,end.y));
            if(rect.width<6&&rect.height<6)
            {if(Physics.Raycast(camera.ScreenPointToRay(end),out var hit,camera.farClipPlane)&&hit.collider.GetComponentInParent<DarwinEntity>() is DarwinEntity entity&&session.CanCommand(entity))_selected.Add(entity);return;}
            foreach(var entity in EntityRegistry.All)
            {
                if(_selected.Count>=LanSettings.Current.maximumRtsSelection)break;if(!EcologyQueries.Alive(entity)||!session.CanCommand(entity)||_selected.Contains(entity))continue;
                var screen=camera.WorldToScreenPoint(entity.transform.position);if(screen.z<=0||!rect.Contains(screen))continue;
                // 背面的实体投影也在屏幕内，必须排除被星球挡住的目标。
                var surface=EcologyQueries.Planet?.GetComponent<MeshCollider>();var direction=entity.transform.position-camera.transform.position;float distance=direction.magnitude;
                if(surface!=null&&surface.Raycast(new Ray(camera.transform.position,direction.normalized),out var ground,distance)&&ground.distance<distance-1)continue;
                _selected.Add(entity);
            }
            if(_selected.Count>0)FindFirstObjectByType<GeneInspectorUI>()?.ShowEntityInfo(_selected[0]);
        }
        private void OnGUI()
        {
            if(!WorldAuthority.RtsMode||!WorldRuntimeGate.Ready||GameInputGate.Blocked)return;RuntimeMenuTheme.Apply();
            GUILayout.BeginArea(CommandBarRect,GUI.skin.box);GUILayout.BeginHorizontal();GUILayout.Label("RTS · "+_selected.Count+"  R返回",GUILayout.Width(125));
            foreach(var entry in new[]{(RtsOrderKind.Move,"移动"),(RtsOrderKind.Attack,"攻击"),(RtsOrderKind.Collect,"采集"),(RtsOrderKind.Guard,"守卫"),(RtsOrderKind.Stop,"停止")})
            {
                GUI.enabled=_selected.Any(e=>RtsOrderController.Supports(e,entry.Item1));if(GUILayout.Button(entry.Item2,_mode==entry.Item1?RuntimeMenuTheme.Selected:GUI.skin.button,GUILayout.Height(30)))
                {if(entry.Item1==RtsOrderKind.Stop)LanSession.Instance.IssueRts(_selected.Select(EcologyIdentity.Of).ToArray(),entry.Item1,Vector3.zero);else _mode=entry.Item1;}GUI.enabled=true;
            }
            GUILayout.EndHorizontal();GUILayout.EndArea();
            if(_dragging)
            {
                var current=(Vector2)Input.mousePosition;var rect=Rect.MinMaxRect(Mathf.Min(_start.x,current.x),Screen.height-Mathf.Max(_start.y,current.y),Mathf.Max(_start.x,current.x),Screen.height-Mathf.Min(_start.y,current.y));
                var old=GUI.color;GUI.color=new Color(.2f,1,.7f,.15f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=UIVisualTheme.Accent;GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.yMax,rect.width,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.y,1,rect.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.xMax,rect.y,1,rect.height),Texture2D.whiteTexture);GUI.color=old;
            }
            foreach(var entity in _selected)
            {if(entity==null||Camera.main==null)continue;var screen=Camera.main.WorldToScreenPoint(entity.transform.position);if(screen.z>0){var old=GUI.color;GUI.color=UIVisualTheme.Accent;GUI.Label(new Rect(screen.x-12,Screen.height-screen.y-20,32,24),"◇");GUI.color=old;}}
        }
    }
}
