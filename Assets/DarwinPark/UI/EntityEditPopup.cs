using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace DarwinPark.UI
{
    // ★ 新增：实体编辑使用独立弹窗，失效原因仅由基因悬浮显示，原实体面板尺寸、基因行和滚动布局保持一致。
    public sealed partial class EntityEditPopup:MonoBehaviour
    {
        public static EntityEditPopup Instance{get;private set;}public bool Visible=>_overlay!=null;
        private GameObject _overlay;private DarwinEntity _entity;private bool _running,_blocked;private Vector2 _scroll;
        private CursorLockMode _cursor;private bool _cursorVisible;
        private string _status="";private readonly string[] _values=new string[3];private static readonly string[] StatIds={"health","nutrient","storedNutrient"},StatNames={"生命值","饱食度","储存营养"};
        private void Awake()=>Instance=this;
        public static EntityEditPopup Ensure(){var root=ServiceLocator.Get<UIRoot>();return root.GetComponent<EntityEditPopup>()??root.gameObject.AddComponent<EntityEditPopup>();}
        public static void AddEntry(Transform parent,DarwinEntity entity,ColonyResource corpse=null)
        {
            var button=UIFactory.NewButton("EntityEdit",parent,"编辑",()=>Ensure().Open(entity,corpse),new Vector2(124,24));button.GetComponent<Button>().interactable=entity!=null;var rect=button.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(1,1);rect.anchoredPosition=Vector2.zero;
        }
        public void Open(DarwinEntity entity,ColonyResource corpse=null)
        {
            if(Visible||!WorldRuntimeGate.Ready||entity==null)return;
            _entity=entity;_status="";_cursor=Cursor.lockState;_cursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;_scroll=Vector2.zero;_blocked=GameInputGate.Blocked;_running=ServiceLocator.Get<SimulationClock>().IsRunning;if(!WorldAuthority.Online)ServiceLocator.Get<SimulationClock>().Pause();GameInputGate.Blocked=true;
            _overlay=UIFactory.NewRect("EntityEditOverlay",ServiceLocator.Get<UIRoot>().MainCanvas.transform,typeof(Image));UIFactory.Stretch(_overlay.GetComponent<RectTransform>());_overlay.GetComponent<Image>().color=Color.clear;
            for(int i=0;i<StatIds.Length;i++){float value=0;entity?.GetComponent<EntityStats>()?.TryGet(StatIds[i],out value);_values[i]=value.ToString("F2");}
            LoadBodyAndTasks();
        }
        public bool ApplyStage(AgeState age)
        {bool ok=_entity!=null&&LanEditActions.Submit(new LanCommand{kind="entityStage",entity=EcologyIdentity.Of(_entity),count=(int)age});_status=ok?(WorldAuthority.IsReplica?"已提交，等待房主确认":"生命阶段已应用"):"无法修改：请检查权限和实体状态";Object.FindFirstObjectByType<GeneInspectorUI>()?.RefreshDisplayedInfo();return ok;}
        public bool ApplyStat(string id,float value)
        {bool ok=_entity!=null&&LanEditActions.Submit(new LanCommand{kind="entityStat",entity=EcologyIdentity.Of(_entity),parameter=id,value=value});_status=ok?(WorldAuthority.IsReplica?"已提交，等待房主确认":"数值已应用"):"请输入有效范围内的数值，并检查编辑权限";return ok;}
        public void Close()
        {if(!Visible)return;_overlay.SetActive(false);Destroy(_overlay);_overlay=null;GameInputGate.Blocked=WorldRuntimeGate.Ready?_blocked:true;if(_running&&!WorldAuthority.Online&&WorldRuntimeGate.Ready&&!WorldRuntimeGate.Loading)ServiceLocator.Get<SimulationClock>().Play();_entity=null;Cursor.lockState=_cursor;Cursor.visible=_cursorVisible;}
        private void Update(){if(!Visible)return;if(!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||_entity==null){Close();return;}if(Input.GetKeyDown(KeyCode.Escape)){GameInputGate.ConsumeEscape();Close();}}
        private void OnDisable()=>Close();private void OnDestroy(){if(Instance==this)Instance=null;}
        private void OnGUI()
        {
            if(!Visible)return;GUI.depth=-960;RuntimeMenuTheme.Apply();var rect=new Rect((Screen.width-Mathf.Min(600,Screen.width-24))/2,(Screen.height-Mathf.Min(620,Screen.height-24))/2,Mathf.Min(600,Screen.width-24),Mathf.Min(620,Screen.height-24));GUI.Box(rect,GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x+16,rect.y+12,rect.width-32,rect.height-24));GUILayout.BeginHorizontal();GUILayout.Label(_entity!=null?"编辑实体 · "+_entity.DisplayName:"实体已失效",RuntimeMenuTheme.Title);if(GUILayout.Button("关闭",GUILayout.Width(64))){Close();}GUILayout.EndHorizontal();
            _scroll=GUILayout.BeginScrollView(_scroll);
            if(_entity!=null)
            {
                GUILayout.Label("生命阶段");bool previous=GUI.enabled;GUI.enabled=previous&&WorldAuthority.CanEdit;GUILayout.BeginHorizontal();if(GUILayout.Button("幼年"))ApplyStage(AgeState.Infant);if(GUILayout.Button("成年"))ApplyStage(AgeState.Adult);if(GUILayout.Button("老年"))ApplyStage(AgeState.Elder);GUILayout.EndHorizontal();
                for(int i=0;i<StatIds.Length;i++){GUILayout.BeginHorizontal();GUILayout.Label(StatNames[i],GUILayout.Width(80));_values[i]=GUILayout.TextField(_values[i],32);if(GUILayout.Button("应用",GUILayout.Width(64))){if(float.TryParse(_values[i],out var value))ApplyStat(StatIds[i],value);else _status="请输入有效数值";}GUILayout.EndHorizontal();}GUI.enabled=previous;
                DrawBodyAndTasks();
            }
            if(!string.IsNullOrEmpty(_status))GUILayout.Label(_status);
            // ★ 修复（防Bug回归）：原因改由基因卡悬浮呈现，此窗只保留实体编辑。
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
