using System;
using System.IO;
using UnityEngine;

namespace DarwinPark.Genetics
{
    // ★ 新增：观察的范围倍率与刷新周期由资源配置，基因数值仍来自世界保存的定义。
    [Serializable]public sealed class ObservationSettings
    {
        public string geneId="",rangeAttribute="",wideState="",attentionState="",migrationSource="";
        public float wideRangeMultiplier,attentionRangeMultiplier,wideRefreshSeconds,attentionRefreshSeconds;
        public float coordinateCellSize;public int maximumCellsPerQuery;
        private static ObservationSettings _current;
        public static ObservationSettings Current
        {
            get
            {
                if(_current!=null)return _current;var asset=Resources.Load<TextAsset>("Entities/observation");if(asset==null)throw new InvalidDataException("缺少Entities/observation");
                var value=JsonUtility.FromJson<ObservationSettings>(asset.text);
                if(value==null||string.IsNullOrWhiteSpace(value.geneId)||string.IsNullOrWhiteSpace(value.rangeAttribute)||string.IsNullOrWhiteSpace(value.wideState)||string.IsNullOrWhiteSpace(value.attentionState)||value.wideState==value.attentionState)throw new InvalidDataException("观察状态配置无效");
                foreach(float number in new[]{value.wideRangeMultiplier,value.attentionRangeMultiplier,value.wideRefreshSeconds,value.attentionRefreshSeconds,value.coordinateCellSize})if(!float.IsFinite(number)||number<=0)throw new InvalidDataException("观察范围、周期和坐标格须为正有限数");
                if(value.maximumCellsPerQuery<1||value.maximumCellsPerQuery>1000000)throw new InvalidDataException("视野格查询上限无效");
                return _current=value;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>_current=null;
    }
}
