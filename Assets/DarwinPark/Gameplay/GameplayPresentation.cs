using System;
using DarwinPark.Entities;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：玩法只发出表现请求；无界面服务端不安装适配器，不能反向依赖相机、菜单或输入组件。
    public static class GameplayPresentation
    {
        public static Action SuspendInteraction, BeforeWorldChange, ClearWorld, PrepareControls, ExitView;
        public static Action<DarwinEntity> EnterView, RestoreFavorite;
        public static Func<DarwinEntity,bool> IsFavorite;
        public static Action<WorldSaveData,bool,PlanetTerrainMeshBuilder> RestoreCamera;
        public static Action<WorldSaveData> CaptureCamera;
        public static System.Func<Entities.DarwinEntity, Genetics.SavedBodyComposition> CaptureBody;
        public static System.Action<Entities.DarwinEntity, Genetics.SavedBodyComposition> RestoreBody;
        public static Action<LanSession> AttachSession;
        public static Action<string> ShowNetworkMain;
        public static Func<IDisposable> BeginParameterUpdate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SuspendInteraction=BeforeWorldChange=ClearWorld=PrepareControls=ExitView=null;
            EnterView=RestoreFavorite=null;IsFavorite=null;RestoreCamera=null;CaptureCamera=null;CaptureBody=null;RestoreBody=null;
            AttachSession=null;ShowNetworkMain=null;BeginParameterUpdate=null;
        }
    }
}
