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
        [Tooltip("单帧最多推进的模拟秒数；多余时间继续排队，防止卡顿后无限补算")]
        public float maximumGameDelta=.5f;
        [Tooltip("负载恢复后补算速度最多为所选倍率的倍数，避免切回1倍却突然快进几十倍")]
        public float maximumCatchUpMultiplier=1.5f;
        private readonly SimulationStepBudget _budget=new SimulationStepBudget();
        private bool _budgetedFrame;
        public double PendingGameTime=>_budget.Pending;
        // ★ 适配：过载帧的物理行动也按实际交付时间缩放，避免日历限速而实体仍全速移动。
        public float MotionTimeScale=>!IsRunning?0:WorldAuthority.IsReplica||!_budgetedFrame?_timeScale:GameDeltaTime/Mathf.Max(.0001f,Time.unscaledDeltaTime);

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
                // ★ 适配：补算设上限，日历只累计实际完成的统一模拟步长；暂停不新增积压。
                float incoming=Time.unscaledDeltaTime*_timeScale;
                // ★ 修复（防Bug回归）：恢复负载或降低倍率后渐进补算，物理与日历一起按交付倍率推进，不能突然几十倍追赶。
                float limit=Mathf.Min(Mathf.Max(.1f,maximumGameDelta),incoming*Mathf.Max(1,maximumCatchUpMultiplier));
                GameDeltaTime = incoming>0?_budget.Consume(Time.unscaledDeltaTime,_timeScale,limit):0;
                _budgetedFrame=true;
                GameTime += GameDeltaTime;
            }
            else
            {
                GameDeltaTime = 0f;
            }
        }

        // ★ 适配：存档恢复总时间和倍率，加载过程始终保持暂停。
        public void RestoreState(float time,float scale){_budget.Clear();_budgetedFrame=false;GameTime=Mathf.Max(0,time);_timeScale=Mathf.Max(0,scale);IsRunning=false;GameDeltaTime=0;ApplyTimeScale();OnRunningChanged?.Invoke(false);}
        // ★ 适配：只读网络时钟不经本地启停请求，避免收到同步时再次向服务端发包。
        public void ApplyRemoteState(float time,float scale,bool running){GameTime=time;_timeScale=scale;GameDeltaTime=0;bool changed=IsRunning!=running;IsRunning=running;if(changed)OnRunningChanged?.Invoke(running);}
        // ★ 修复（防Bug回归）：加载结束的当前帧 delta 仍包含磁盘/上传等待，下一帧才开始玩家时间。
        public void Play() { if(WorldAuthority.InterceptTime(true,_timeScale))return;if(!IsRunning){_resumeFrame=Time.frameCount;_budgetedFrame=false;}IsRunning = true; ApplyTimeScale(); OnRunningChanged?.Invoke(true); }
        public void Pause() { if(WorldAuthority.InterceptTime(false,_timeScale))return;IsRunning = false; GameDeltaTime = 0f; ApplyTimeScale(); OnRunningChanged?.Invoke(false); }
        public void Toggle() { if (IsRunning) Pause(); else Play(); }
        public void SetTimeScale(float s){if(WorldAuthority.InterceptTime(IsRunning,s))return;_timeScale=Mathf.Max(0,s);_budgetedFrame=false;}

        private void ApplyTimeScale()
        {
            // 我们不使用 Time.timeScale = 0，因为那会影响 UI 动画。
            // 所有逻辑冻结完全靠 GameDeltaTime 为 0 来控制。
        }

        private void OnDisable() => ServiceLocator.Unregister<SimulationClock>();
    }
}
