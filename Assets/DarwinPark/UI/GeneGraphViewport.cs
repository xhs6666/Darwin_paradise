using System.Collections.Generic;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.UI
{
    // ★ 新增：缩放只改变视图，节点保存的坐标保持不变；鼠标锚点与拖入坐标使用同一换算。
    public sealed class GeneGraphViewport
    {
        public const float MinZoom=.15f,MaxZoom=2f;
        public float Zoom {get;private set;}=1;
        public Vector2 Scroll;
        public Vector2 ToGraph(Vector2 local)=>(local+Scroll)/Zoom;
        public void SetZoom(float value,Vector2 anchor)
        {
            var point=ToGraph(anchor);Zoom=Mathf.Clamp(value,MinZoom,MaxZoom);
            Scroll=Vector2.Max(Vector2.zero,point*Zoom-anchor);
        }
        public void Fit(IEnumerable<GeneTreeNode> nodes,Vector2 nodeSize,Vector2 viewport)
        {
            Vector2 min=Vector2.positiveInfinity,max=Vector2.zero;bool any=false;
            foreach(var node in nodes){min=Vector2.Min(min,node.position);max=Vector2.Max(max,node.position+nodeSize);any=true;}
            if(!any){Zoom=1;Scroll=Vector2.zero;return;}
            var size=max-min+Vector2.one*40;
            Zoom=Mathf.Clamp(Mathf.Min((viewport.x-20)/size.x,(viewport.y-20)/size.y),MinZoom,MaxZoom);
            Scroll=Vector2.Max(Vector2.zero,(min-Vector2.one*20)*Zoom);
        }
    }
}
