using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：运行时由能量块创建入口挂载；每物件属性块不会污染其他能量块或尸体材质。
    [RequireComponent(typeof(ColonyResource))] public sealed class EnergyBlockGradeVisual:MonoBehaviour
    {
        private ColonyResource _resource;private Renderer _renderer;private Light _light;private MaterialPropertyBlock _block;
        private float _energy=float.NaN;
        private void Awake(){_resource=GetComponent<ColonyResource>();_renderer=GetComponent<Renderer>();_light=GetComponent<Light>();_block=new MaterialPropertyBlock();}
        private void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(_resource.State==null||_resource.State.kind==ColonyResourceKind.Corpse||_energy==_resource.State.energy)return;
            _energy=_resource.State.energy;int index=EnergyBlockGrades.Level(_energy);var grade=EnergyBlockGrades.Table.levels[index];
            ColorUtility.TryParseHtmlString(grade.color,out var color);_renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor",color);_block.SetColor("_Color",color);_block.SetColor("_EmissionColor",color*AntColonySettings.Current.glowIntensity);_renderer.SetPropertyBlock(_block);
            if(_light!=null)_light.color=color;
            transform.localScale=Vector3.one*AntColonySettings.Current.energyBlockSize*grade.scale;
            gameObject.name=$"{grade.name}能量块 Lv.{index+1} · {_energy:F1}";
        }
    }
}
