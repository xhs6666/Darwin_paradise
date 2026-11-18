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
        // ✦ 新增：限制空格版本缓存，出生/移动分布变化不能让索引历史无限增长。
        public int coordinateRevisionCellLimit=65536;
        public float perceptionBudgetMilliseconds=1.5f, minimumRealRefreshSeconds=.05f, memorySeconds=5f;
        public int perceptionScansPerFrame=64, maximumRememberedTargets=256;
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
                if(value.coordinateRevisionCellLimit<256||value.coordinateRevisionCellLimit>1000000)throw new InvalidDataException("coordinateRevisionCellLimit must be between 256 and 1000000");
                if(value.perceptionBudgetMilliseconds<=0||!float.IsFinite(value.perceptionBudgetMilliseconds)||value.minimumRealRefreshSeconds<=0||!float.IsFinite(value.minimumRealRefreshSeconds)||value.memorySeconds<=0||!float.IsFinite(value.memorySeconds)||value.perceptionScansPerFrame<1||value.perceptionScansPerFrame>4096||value.maximumRememberedTargets<1||value.maximumRememberedTargets>65536)throw new InvalidDataException("Invalid perception budget or memory settings");
                return _current=value;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>_current=null;
    }
}
