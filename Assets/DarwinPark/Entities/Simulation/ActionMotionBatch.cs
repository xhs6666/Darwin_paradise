using UnityEngine;
namespace DarwinPark.Entities.Simulation
{
    // ★ 新增：同一次基因补算只在最后小步写一次刚体，避免在完全相同物理位置重复转向/避让。
    public static class ActionMotionBatch
    {
        private struct Frame {public EntityGeneController owner;public bool last;public float elapsed;}
        private static Frame _current;private static readonly System.Collections.Generic.Stack<Frame> Parents=new();
        public static void Begin(EntityGeneController owner,float elapsed){if(_current.owner!=null)Parents.Push(_current);_current=new Frame{owner=owner,elapsed=elapsed};}
        public static void Step(EntityGeneController owner,bool last){if(_current.owner==owner)_current.last=last;}
        public static bool Consume(EntityGeneController owner,ref float dt){if(_current.owner!=owner)return true;if(!_current.last)return false;dt=_current.elapsed;return true;}
        public static void End(EntityGeneController owner){if(_current.owner==owner)_current=Parents.Count>0?Parents.Pop():default;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){_current=default;Parents.Clear();}
    }
}
