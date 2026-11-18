using System;
using System.IO;
using UnityEngine;
namespace DarwinPark.Entities.Logic
{
    // ★ 新增：新内容的能力ID与离散演化方向统一配置，所有输入/AI共用。
    [Serializable] public sealed class BiologySettings
    {
        public int version=2;public string locomotionGene,centralGene,feedingGene,photoGene,mineralGene,swimmingState,amphibiousState;
        private static BiologySettings _current;
        public static BiologySettings Current {get{if(_current!=null)return _current;var asset=Resources.Load<TextAsset>("Entities/biology");if(asset==null)throw new InvalidDataException("缺少Entities/biology");var value=JsonUtility.FromJson<BiologySettings>(asset.text);if(value==null||value.version!=2)throw new InvalidDataException("生物能力版本无效");foreach(var field in typeof(BiologySettings).GetFields())if(field.FieldType==typeof(string)&&string.IsNullOrWhiteSpace((string)field.GetValue(value)))throw new InvalidDataException("生物能力缺少ID："+field.Name);return _current=value;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>_current=null;
    }
}
