using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 适配：单次公共查询仍读取即时世界；生成协程显式传入同步片段快照，不能用全局旧坐标替代。
    public static class ProducerPlacementSearch
    {
        public static Vector3 Direction(Vector3 planetCenter,EcologyDefaultsSettings settings)
            =>new ProducerPlacementSnapshot().Direction(planetCenter,settings);
    }
}
