using System;
using System.IO;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarwinPark.EditorTools
{
    // ✦ 新增：构建副本剔除界面组件，不修改开发场景，不在运行期等待菜单创建服务。
    public sealed class DedicatedServerBuild : IProcessSceneWithReport
    {
        private static bool _headlessBuild;
        public int callbackOrder=>10000;
        public void OnProcessScene(Scene scene,BuildReport report)
        {
            if(report==null||report.summary.platformGroup!=BuildTargetGroup.Standalone||!_headlessBuild&&EditorUserBuildSettings.standaloneBuildSubtarget!=StandaloneBuildSubtarget.Server)return;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var menu in root.GetComponentsInChildren<GameMainMenu>(true))UnityEngine.Object.DestroyImmediate(menu);
            foreach(var root in scene.GetRootGameObjects())
            foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(component==null)continue;
                Type type=component.GetType();
                bool ui=type.Assembly.GetName().Name=="DarwinPark.UI";
                bool service=type==typeof(WorldSessionManager)||type==typeof(LanSession)||type==typeof(DedicatedServerBootstrap)||type==typeof(RandomPlanetSpawner)||type==typeof(WorldParameterUpdate)||type==typeof(NaturalWorldSeeder);
                if(ui&&!service||type==typeof(UIRoot)||type.Name=="OrbitCamera"||type.Name=="SimulationPauseHotkey")UnityEngine.Object.DestroyImmediate(component);
            }
            // ★ 修复（防Bug回归）：先收集待删除对象，再统一删除；相机可能就是根对象，嵌套 Canvas 也会随父对象销毁。
            var roots=scene.GetRootGameObjects();
            var presentationObjects=roots.SelectMany(root=>root.GetComponentsInChildren<Canvas>(true).Select(c=>c.gameObject)
                .Concat(root.GetComponentsInChildren<Camera>(true).Select(c=>c.gameObject))
                .Concat(root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Select(c=>c.gameObject))).Distinct().ToArray();
            foreach(var root in roots)
            {
                foreach(var audio in root.GetComponentsInChildren<AudioListener>(true))UnityEngine.Object.DestroyImmediate(audio);
                foreach(var video in root.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true))UnityEngine.Object.DestroyImmediate(video);
            }
            foreach(var presentation in presentationObjects)if(presentation!=null)UnityEngine.Object.DestroyImmediate(presentation);
            var world=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WorldSessionManager>(true)).FirstOrDefault();
            if(world==null)throw new BuildFailedException("Server scene requires WorldSessionManager");
            if(world.GetComponent<DedicatedServerBootstrap>()==null)world.gameObject.AddComponent<DedicatedServerBootstrap>();
            Debug.Log("[ServerBuild] Processed server scene: "+scene.path+" bootstrap="+world.GetComponent<DedicatedServerBootstrap>().enabled);
        }
        [MenuItem("DarwinPark/Build/Windows Dedicated Server")]
        public static void BuildWindows()=>Build(BuildTarget.StandaloneWindows64,"Builds/Server/Windows/DarwinServer.exe");
        [MenuItem("DarwinPark/Build/Linux Dedicated Server")]
        public static void BuildLinux()=>Build(BuildTarget.StandaloneLinux64,"Builds/Server/Linux/DarwinServer");
        [MenuItem("DarwinPark/Build/Windows Headless Server (no server module)")]
        public static void BuildWindowsHeadless()=>Build(BuildTarget.StandaloneWindows64,"Builds/Server/Windows/DarwinServer.exe",true);
        [MenuItem("DarwinPark/Build/Windows Server Core (Core + Start)")]
        public static void BuildWindowsCorePackage()
        {
            // ★ 新增：一键流程在独立构建目录生成真实 Player，再封装为两个入口文件，不复用旧构建冒充成功。
            string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            string raw=Path.GetFullPath(Path.Combine("Artifacts","ServerCoreBuild",id,"player"));Directory.CreateDirectory(raw);
            Build(BuildTarget.StandaloneWindows64,Path.Combine(raw,"DarwinServer.exe"),true);
            string destination=DedicatedServerMode.Argument("-server-package-output")??Path.Combine("Builds","ServerCore","Windows",id);
            ServerCorePackaging.Create(raw,destination);
        }
        public static void PackageExistingWindowsCore()
        {
            string source=DedicatedServerMode.Argument("-server-package-source")??"Builds/Server/Windows";
            string destination=DedicatedServerMode.Argument("-server-package-output")??Path.Combine("Builds","ServerCore","Windows",Guid.NewGuid().ToString("N"));
            ServerCorePackaging.Create(source,destination);
        }
        private static void Build(BuildTarget target,string fallback,bool headless=false)
        {
            string output=DedicatedServerMode.Argument("-server-output")??fallback;
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            BuildReport report;
            _headlessBuild=headless;
            // ★ 修复（防Bug回归）：失败的场景裁剪或普通 Player 的场景缓存不能用于专服，必须重新运行场景处理回调。
            try{report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=output,target=target,subtarget=(int)(headless?StandaloneBuildSubtarget.Player:StandaloneBuildSubtarget.Server),extraScriptingDefines=headless?new[]{"DARWIN_DEDICATED_SERVER"}:Array.Empty<string>(),options=BuildOptions.CleanBuildCache});}
            finally{_headlessBuild=false;}
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Dedicated server build failed");
            string folder=Path.GetDirectoryName(Path.GetFullPath(output));
            File.Copy("Assets/Resources/Entities/server_release.json",Path.Combine(folder,"release.json"),true);
            File.Copy("Server/server.example.json",Path.Combine(folder,"server.example.json"),true);
            File.Copy("Server/Start.ps1",Path.Combine(folder,"Start.ps1"),true);
            File.Copy("Server/Stop.ps1",Path.Combine(folder,"Stop.ps1"),true);
            File.Copy("Server/Console.ps1",Path.Combine(folder,"Console.ps1"),true);
            File.Copy("Server/Start.cmd",Path.Combine(folder,"Start.cmd"),true);
            Debug.Log("[ServerBuild] "+output+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings);
        }
    }
}
