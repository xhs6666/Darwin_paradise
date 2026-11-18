using System;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Planet;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：统一安装客户端表现适配；玩法层及专服不引用 UI 程序集。
    internal static class ClientPresentationRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if(DedicatedServerMode.Enabled)return;
            DarwinEntity.PreparePresentation=EntityBodyView.Attach;
            GameplayPresentation.CaptureBody=entity=>entity.GetComponent<EntityBodyView>()?.CaptureComposition();
            GameplayPresentation.RestoreBody=(entity,composition)=>entity.GetComponent<EntityBodyView>()?.RestoreComposition(composition);
            LanRuntimeServices.CreateClient=owner=>new LanClientRuntime(owner);
            GameplayPresentation.SuspendInteraction=()=>{EntityViewController.Instance?.ExitView();EntityPlacementTool.Instance?.CancelPlacement();};
            GameplayPresentation.BeforeWorldChange=()=>{GameplayPresentation.SuspendInteraction?.Invoke();PopulationRangeLayer.ClearWorld();};
            GameplayPresentation.ClearWorld=()=>
            {
                GameplayPresentation.BeforeWorldChange?.Invoke();
                foreach(var inspector in UnityEngine.Object.FindObjectsByType<GeneInspectorUI>(FindObjectsSortMode.None))inspector.HideEntityInfo();
                EntityFavoritesManager.Instance?.ClearWorld();
            };
            GameplayPresentation.EnterView=entity=>EntityViewController.Instance?.EnterView(entity);
            GameplayPresentation.ExitView=()=>EntityViewController.Instance?.ExitView();
            GameplayPresentation.RestoreFavorite=entity=>EntityFavoritesManager.Instance?.ToggleFavorite(entity);
            GameplayPresentation.IsFavorite=entity=>EntityFavoritesManager.Instance?.IsFavorite(entity)==true;
            GameplayPresentation.PrepareControls=WorldHudInputLifecycle.PrepareWorldControls;
            GameplayPresentation.AttachSession=session=>
            {
                if(session.GetComponent<LanLobbyUI>()==null)session.gameObject.AddComponent<LanLobbyUI>();
                if(session.GetComponent<RtsSelectionInput>()==null)session.gameObject.AddComponent<RtsSelectionInput>();
            };
            GameplayPresentation.ShowNetworkMain=reason=>UnityEngine.Object.FindFirstObjectByType<GameMainMenu>()?.ShowNetworkMain(reason);
            GameplayPresentation.RestoreCamera=(data,isNew,builder)=>
            {
                var camera=Camera.main;var orbit=camera!=null?camera.GetComponent<OrbitCamera>():null;
                if(orbit==null)return;
                if(isNew){var position=builder.transform.position+new Vector3(0,.35f,-1).normalized*orbit.distance;orbit.RestoreView(position,Quaternion.LookRotation(builder.transform.position-position,Vector3.up),orbit.distance);}
                else orbit.RestoreView(data.cameraPosition,data.cameraRotation,data.cameraDistance);
            };
            GameplayPresentation.CaptureCamera=data=>
            {
                var camera=Camera.main;var orbit=camera!=null?camera.GetComponent<OrbitCamera>():null;
                if(camera!=null&&orbit!=null){data.cameraPosition=camera.transform.position;data.cameraRotation=camera.transform.rotation;data.cameraDistance=orbit.distance;}
            };
            GameplayPresentation.BeginParameterUpdate=()=>new ParameterViewScope();
        }

        private sealed class ParameterViewScope:IDisposable
        {
            private readonly UIRoot root;
            private readonly bool canvas,worldCanvas,cursorVisible;
            private readonly CursorLockMode cursorLock;
            public ParameterViewScope()
            {
                WorldGameSettingsPanel.Instance?.Close();EntityPlacementTool.Instance?.CancelPlacement();
                cursorLock=Cursor.lockState;cursorVisible=Cursor.visible;
                if(ServiceLocator.TryGet<UIRoot>(out var ui))root=ui;
                if(root!=null){canvas=root.MainCanvas!=null&&root.MainCanvas.enabled;if(root.MainCanvas!=null)root.MainCanvas.enabled=false;worldCanvas=root.WorldCanvas!=null&&root.WorldCanvas.enabled;if(root.WorldCanvas!=null)root.WorldCanvas.enabled=false;}
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                var updater=WorldParameterUpdate.Instance;
                if(updater.GetComponent<WorldParameterUpdatePresentation>()==null)updater.gameObject.AddComponent<WorldParameterUpdatePresentation>();
            }
            public void Dispose()
            {
                if(root!=null){if(root.MainCanvas!=null)root.MainCanvas.enabled=canvas;if(root.WorldCanvas!=null)root.WorldCanvas.enabled=worldCanvas;}
                Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;
            }
        }
    }

    public sealed class WorldParameterUpdatePresentation:MonoBehaviour
    {
        private void OnGUI()
        {
            var update=WorldParameterUpdate.Instance;if(update==null||!update.Busy)return;
            GUI.depth=-1500;RuntimeMenuTheme.Apply();GUI.color=new Color(.025f,.04f,.065f,.7f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            float width=Mathf.Min(480,Screen.width-32),height=Mathf.Min(360,Screen.height-32);GUILayout.BeginArea(new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height),GUI.skin.box);GUILayout.Label("正在更新当前世界",RuntimeMenuTheme.Title);WorldLoadingView.Draw(update.Status,update.Progress,"完成后返回原位置");GUILayout.EndArea();
        }
    }
}
