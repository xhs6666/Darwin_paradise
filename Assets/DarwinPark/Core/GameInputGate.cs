namespace DarwinPark.Core
{
    // ★ 新增：菜单与工具共用输入门控，避免编辑连线时穿透到游戏。
    public static class GameInputGate
    {
        private static bool _blocked;
        private static int _escapeFrame=-1;
        public static bool Blocked { get=>_blocked || _escapeFrame==UnityEngine.Time.frameCount; set=>_blocked=value; }
        public static void ConsumeEscape()=>_escapeFrame=UnityEngine.Time.frameCount;
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){_blocked=false;_escapeFrame=-1;}
    }
}
