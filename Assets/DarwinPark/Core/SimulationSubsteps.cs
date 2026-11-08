using UnityEngine;

namespace DarwinPark.Core
{
    // ★ 修复（防Bug回归）：累计0.1秒的微小浮点余数不能再触发整轮基因行为；余数并入最后一步，模拟时间不丢失。
    public static class SimulationSubsteps
    {
        public const float MaximumStep=.1f;
        private const float RoundingTolerance=.000001f;
        public static int Count(float elapsed)=>elapsed<=0?0:Mathf.Max(1,Mathf.CeilToInt((elapsed-RoundingTolerance)/MaximumStep));
        public static float Slice(float elapsed,int index,int count)=>index==count-1?elapsed-index*MaximumStep:MaximumStep;
    }
}
