using System;

namespace DarwinPark.Core
{
    // ★ 新增：限制单帧补算工作量，尚未完成的时间排队；禁止丢弃积压或让日历提前跳过实体模拟。
    public sealed class SimulationStepBudget
    {
        public double Pending {get;private set;}
        public float Consume(float elapsed,float scale,float maximumStep)
        {
            if(float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0||float.IsNaN(scale)||float.IsInfinity(scale)||scale<0||float.IsNaN(maximumStep)||float.IsInfinity(maximumStep)||maximumStep<=0)
                throw new ArgumentOutOfRangeException(nameof(maximumStep),"时间与倍率须有限且非负，单帧预算须大于零");
            Pending+=(double)elapsed*scale;
            float delivered=(float)Math.Min(Pending,maximumStep);Pending=Math.Max(0,Pending-delivered);return delivered;
        }
        public void Clear()=>Pending=0;
    }
}
