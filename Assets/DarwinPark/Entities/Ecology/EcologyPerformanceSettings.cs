using System;
using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    [Serializable] public sealed class EcologyPerformanceSettings
    {
        public bool reproductionDiagnostics=false,stationarySleep=true;
        public float summaryInterval=5,restSpeed=.08f,restContactSlack=.04f;
        private static EcologyPerformanceSettings _current;
        public static EcologyPerformanceSettings Current
        {
            get
            {
                if(_current!=null)return _current;
                var asset=Resources.Load<TextAsset>("Entities/ecology_performance");
                if(asset==null){Debug.LogWarning("缺少 Entities/ecology_performance，使用性能配置默认值");return _current=new EcologyPerformanceSettings();}
                var parsed=JsonUtility.FromJson<EcologyPerformanceSettings>(asset.text);
                if(parsed==null||!float.IsFinite(parsed.summaryInterval)||parsed.summaryInterval<=0||!float.IsFinite(parsed.restSpeed)||parsed.restSpeed<0||!float.IsFinite(parsed.restContactSlack)||parsed.restContactSlack<0)
                    throw new InvalidOperationException("生态性能配置无效");
                return _current=parsed;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>_current=null;
    }
}
