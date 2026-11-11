using UnityEngine;

// ★ 修复（防Bug回归）：可选组件允许正常缺失，使用即时TryGet，保留安装/销毁可见性并消除编辑器空查询分配。
namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：可见色环对应真正的 GroupId；角色名称仍在实体面板显示，标记不添加碰撞体。
    public sealed class ColonyMemberMarker : MonoBehaviour
    {
        private LineRenderer _ring;private AntColonyMember _member;
        private string _group;
        private long _displayRevision=-1;
        private void Awake()
        {
            _member=DarwinPark.Core.OptionalComponent.Read<AntColonyMember>(this);_ring=ColonyAppearance.Circle(transform,"种群标记",.06f);_ring.positionCount=24;
            // ★ 修复（防Bug回归）：色环局部几何不随实体移动变化，初始化一次即可，不需要每帧重复 24 次跨引擎调用。
            var points=new Vector3[_ring.positionCount];for(int i=0;i<points.Length;i++){float angle=i*Mathf.PI*2/points.Length;points[i]=new Vector3(Mathf.Cos(angle)*.65f,.05f,Mathf.Sin(angle)*.65f);}_ring.SetPositions(points);
        }
        private void LateUpdate()
        {
            bool show=_member!=null&&_member.Active&&_member.Role!=AntRole.None&&PopulationArchive.IsVisible(_member.GroupId)&&DarwinPark.Core.OptionalComponent.Read<EntityManualControl>(this)?.IsControlled!=true;
            _ring.enabled=show;if(!show)return;
            if(_group!=_member.GroupId||_displayRevision!=PopulationArchive.DisplayRevision){_group=_member.GroupId;_displayRevision=PopulationArchive.DisplayRevision;ColonyAppearance.Tint(_ring,ColonyAppearance.ColorFor(_group));}
        }
        private void OnDisable(){if(_ring!=null)_ring.enabled=false;}
    }
}
