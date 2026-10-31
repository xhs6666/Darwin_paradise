using UnityEngine;
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：卵生后代独立孵化；孵化时间、碰撞、生命值和外壳防御均可跨存档恢复。
    public sealed class EggIncubation : MonoBehaviour
    {
        public float Remaining {get;private set;}
        public bool Incubating=>Remaining>0;
        public void Begin(float seconds){Remaining=Mathf.Max(0,seconds);}
        public void Advance(float dt)
        {
            if(dt<=0||!Incubating)return;
            Remaining=Mathf.Max(0,Remaining-dt);
            var body=GetComponent<Rigidbody>();if(body!=null&&!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            if(!Incubating)GetComponent<EntityLifeCycle>()?.InitializeBirth();
        }
    }
}
