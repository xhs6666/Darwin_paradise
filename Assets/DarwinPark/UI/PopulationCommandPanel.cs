using System;
using System.Linq;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using UnityEngine;
using UnityEngine.UI;
namespace DarwinPark.UI
{
    // ★ 新增：命令按钮挂在既有可滚动实体信息面板，使用同一 Canvas/射线，点击不会穿透世界。
    public sealed class PopulationCommandPanel : MonoBehaviour
    {
        private DarwinEntity _entity;private Text _status;private string _message="";
        private Button[] _orders;private Button _challenge;
        public static void Build(DarwinEntity entity,Transform parent)
        {
            var population=entity.GetComponent<PopulationBehavior>();
            if(population==null||!population.Active)return;
            var obj=UIFactory.NewRect("PopulationCommands",parent,typeof(VerticalLayoutGroup),typeof(LayoutElement));
            obj.GetComponent<LayoutElement>().preferredHeight=165;
            var layout=obj.GetComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.spacing=3;
            var panel=obj.AddComponent<PopulationCommandPanel>();panel._entity=entity;
            panel._status=UIFactory.NewText("Status",obj.transform,"",13);panel._status.gameObject.AddComponent<LayoutElement>().preferredHeight=58;
            var row=UIFactory.NewRect("Orders",obj.transform,typeof(HorizontalLayoutGroup),typeof(LayoutElement));row.GetComponent<LayoutElement>().preferredHeight=30;
            var commands=new[]{PopulationCommand.Hunt,PopulationCommand.Gather,PopulationCommand.Rally,PopulationCommand.Defend};
            panel._orders=commands.Select(command=>UIFactory.NewButton(command.ToString(),row.transform,PopulationBehavior.CommandLabel(command),()=>panel.Issue(command),new Vector2(65,28))).ToArray();
            panel._challenge=UIFactory.NewButton("Duel",obj.transform,"向领袖发起决斗",panel.Challenge,new Vector2(240,28));
            panel._challenge.gameObject.AddComponent<LayoutElement>().preferredHeight=28;
            panel.Refresh();
        }
        private void Update(){Refresh();}
        private void Refresh()
        {
            if(_entity==null||_status==null)return;var population=_entity.GetComponent<PopulationBehavior>();if(population==null)return;
            bool blocked=GameInputGate.Blocked;bool leader=population.IsLeader;
            // ★ 适配：蚁群固定分工不再显示可用的狼群决斗/全群采集按钮。
            if(_entity.GetComponent<AntColonyMember>()?.Active==true)
            {_status.text=ColonyStatusPresentation.Summary(_entity);foreach(var button in _orders)button.interactable=false;_challenge.interactable=false;return;}
            _status.text=(population.Mode==EcologySettings.Load().antState?"蚁群":"狼群")+" · "+population.MemberCount+" 成员 · "+(leader?"领袖":"成员")+"\n领袖："+(population.Leader!=null?population.Leader.GetComponent<DarwinEntity>().DisplayName:"待选")+" · "+PopulationBehavior.CommandLabel(population.Command)+"\n"+_message;
            foreach(var button in _orders)button.interactable=!blocked&&leader;
            _challenge.interactable=!blocked&&!leader&&population.Active&&population.DuelRemaining<=0;
        }
        private void Challenge(){_entity.GetComponent<PopulationBehavior>().ChallengeLeadership(out _message);}
        private void Issue(PopulationCommand command)
        {
            if(GameInputGate.Blocked)return;
            var pop=_entity.GetComponent<PopulationBehavior>();var settings=EcologySettings.Load();
            var territory=_entity.GetComponent<TerritoryBehavior>();Vector3 point=territory!=null?territory.Center:_entity.transform.position;DarwinEntity prey=null;
            if(command==PopulationCommand.Hunt)
                prey=EntityRegistry.All.Where(e=>e!=_entity&&EcologyQueries.Alive(e)&&!pop.SameGroup(e)&&EcologyQueries.Distance(_entity.transform.position,e.transform.position)<=settings.maximumCommandRange)
                    .OrderBy(e=>(e.transform.position-_entity.transform.position).sqrMagnitude).FirstOrDefault();
            if(command==PopulationCommand.Gather&&!pop.FindOre(out point)){_message="附近没有可采集矿脉";return;}
            pop.Issue(command,prey,point,true,out _message);
        }
    }
}
