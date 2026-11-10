using UnityEngine;
namespace DarwinPark.Core
{
    // ★ 新增：可选组件缺失是正常状态。TryGetComponent避免编辑器为每次空GetComponent生成诊断字符串；不跨帧缓存，新增/销毁立即可见。
    public static class OptionalComponent
    {
        public static T Read<T>(Component owner)where T:Component
            =>owner!=null&&owner.TryGetComponent<T>(out var result)?result:null;
    }
}
