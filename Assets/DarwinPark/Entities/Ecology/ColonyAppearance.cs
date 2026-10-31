using UnityEngine;

namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：同群共享稳定色标，独立材质只用于新建线条，不修改实体或地表共享材质。
    public static class ColonyAppearance
    {
        private static Material _lineMaterial;
        public static Color ColorFor(string id)
        {uint hash=2166136261;unchecked{foreach(char c in id??""){hash^=c;hash*=16777619;}}return Color.HSVToRGB((hash%100000)/100000f,.6f,1);}
        // ★ 修复（防Bug回归）：URP Unlit 不读取线条顶点色，必须使用独立属性块，否则所有种群标记和范围均显示成白色。
        public static void Tint(LineRenderer line,Color color)
        {
            line.startColor=line.endColor=color;
            var block=new MaterialPropertyBlock();line.GetPropertyBlock(block);block.SetColor("_BaseColor",color);block.SetColor("_Color",color);line.SetPropertyBlock(block);
        }
        public static LineRenderer Circle(Transform parent,string name,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.loop=true;line.useWorldSpace=false;line.widthMultiplier=width;
            if(_lineMaterial==null){var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new System.InvalidOperationException("种群标记缺少 Unlit Shader");_lineMaterial=new Material(shader);}
            line.sharedMaterial=_lineMaterial;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
    }
}
