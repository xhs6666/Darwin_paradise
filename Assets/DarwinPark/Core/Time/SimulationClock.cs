using System;
using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 修复（防Bug回归）：先更新统一时钟，实体本帧才能读取相同的模拟步长。
    [DefaultExecutionOrder(-1000)]
    public class SimulationClock : MonoBehaviour
    {
        [SerializeField] private bool _startRunning = true;
        [SerializeField] private float _timeScale = 1f;

        public bool IsRunning { get; private set; }
        public float GameTime { get; private set; }

        // ★ 核心：全局统一受控的 DeltaTime，所有实体和系统都用这个
        public float GameDeltaTime { get; private set; }
        public float TimeScale => IsRunning ? _timeScale : 0f;
        public float ConfiguredTimeScale => _timeScale;
        private int _resumeFrame=-1;

        public event Action<bool> OnRunningChanged;

        private void Awake()
        {
            if (ServiceLocator.TryGet<SimulationClock>(out _))
            {
                Destroy(this);
                return;
            }
            ServiceLocator.Register(this);
            IsRunning = _startRunning;
            GameTime = 0f; // 确保从 0 开始
        }

        private void Start()
        {
            ApplyTimeScale();
            OnRunningChanged?.Invoke(IsRunning);
        }

        private void Update()
        {
            // ★ 适配：联机副本只显示房主下发的时间，不能独立推进第二份世界。
            if(WorldAuthority.IsReplica){GameDeltaTime=0;return;}
            // ★ 修复（防Bug回归）：加载屏完成前不累计日期，模拟仍沿用统一时钟。
            if (IsRunning && Time.frameCount>_resumeFrame && (!WorldRuntimeGate.Managed || WorldRuntimeGate.Ready))
            {
                // GameDeltaTime = 真实时间 * 时间流速
                GameDeltaTime = Time.unscaledDeltaTime * _timeScale;
                GameTime += GameDeltaTime;
            }
            else
            {
                GameDeltaTime = 0f;
            }
        }

        // ★ 适配：存档恢复总时间和倍率，加载过程始终保持暂停。
        public void RestoreState(float time,float scale){GameTime=Mathf.Max(0,time);_timeScale=Mathf.Max(0,scale);IsRunning=false;GameDeltaTime=0;ApplyTimeScale();OnRunningChanged?.Invoke(false);}
        // ★ 适配：只读网络时钟不经本地启停请求，避免收到同步时再次向服务端发包。
        public void ApplyRemoteState(float time,float scale,bool running){GameTime=time;_timeScale=scale;GameDeltaTime=0;bool changed=IsRunning!=running;IsRunning=running;if(changed)OnRunningChanged?.Invoke(running);}
        // ★ 修复（防Bug回归）：加载结束的当前帧 delta 仍包含磁盘/上传等待，下一帧才开始玩家时间。
        public void Play() { if(WorldAuthority.InterceptTime(true,_timeScale))return;if(!IsRunning)_resumeFrame=Time.frameCount;IsRunning = true; ApplyTimeScale(); OnRunningChanged?.Invoke(true); }
        public void Pause() { if(WorldAuthority.InterceptTime(false,_timeScale))return;IsRunning = false; GameDeltaTime = 0f; ApplyTimeScale(); OnRunningChanged?.Invoke(false); }
        public void Toggle() { if (IsRunning) Pause(); else Play(); }
        public void SetTimeScale(float s){if(WorldAuthority.InterceptTime(IsRunning,s))return;_timeScale=Mathf.Max(0,s);}

        private void ApplyTimeScale()
        {
            // 我们不使用 Time.timeScale = 0，因为那会影响 UI 动画。
            // 所有逻辑冻结完全靠 GameDeltaTime 为 0 来控制。
        }

        private void OnDisable() => ServiceLocator.Unregister<SimulationClock>();
    }
}
