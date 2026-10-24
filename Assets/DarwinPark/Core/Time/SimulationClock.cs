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
            if (IsRunning)
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

        public void Play() { IsRunning = true; ApplyTimeScale(); OnRunningChanged?.Invoke(true); }
        public void Pause() { IsRunning = false; GameDeltaTime = 0f; ApplyTimeScale(); OnRunningChanged?.Invoke(false); }
        public void Toggle() { if (IsRunning) Pause(); else Play(); }
        public void SetTimeScale(float s) => _timeScale = Mathf.Max(0f, s);

        private void ApplyTimeScale()
        {
            // 我们不使用 Time.timeScale = 0，因为那会影响 UI 动画。
            // 所有逻辑冻结完全靠 GameDeltaTime 为 0 来控制。
        }

        private void OnDisable() => ServiceLocator.Unregister<SimulationClock>();
    }
}
