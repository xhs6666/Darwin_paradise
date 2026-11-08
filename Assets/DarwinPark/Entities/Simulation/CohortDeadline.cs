using System;
using UnityEngine;

namespace DarwinPark.Entities.Simulation
{
    // ★ 修复（防Bug回归）：空间格与基因相同的近邻仍同批结算，不同格错开；不能让所有静止实体每半秒一起补算。
    public static class CohortDeadline
    {
        public static double Next(double after,Vector3 point,int signature,float radius,float interval,int buckets)
        {
            uint hash;
            unchecked
            {
                hash=(uint)(Mathf.FloorToInt(point.x/radius)*73856093)^((uint)Mathf.FloorToInt(point.y/radius)*19349663u)^((uint)Mathf.FloorToInt(point.z/radius)*83492791u)^((uint)signature*2654435761u);
                hash^=hash>>16;
            }
            double phase=(hash%(uint)buckets)*(double)interval/buckets;
            return phase+(Math.Floor((after-phase)/interval)+1)*interval;
        }
    }
}
