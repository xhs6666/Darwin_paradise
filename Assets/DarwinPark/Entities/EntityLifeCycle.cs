using DarwinPark.Entities.Stats;
using UnityEngine;

namespace DarwinPark.Entities
{
    /// <summary>★ 新增：寿命进度独立于可被抑制/复制的生长基因逻辑，避免重挂基因重置年龄或永远不死。</summary>
    [RequireComponent(typeof(EntityGeneController),typeof(EntityStats))]
    public sealed class EntityLifeCycle : MonoBehaviour
    {
        public float Progress { get; private set; }
        public bool HasDied { get; private set; }
        private bool _born;
        private EntityGeneController _genes;
        private EntityStats _stats;
        private void Awake(){_genes=GetComponent<EntityGeneController>();_stats=GetComponent<EntityStats>();}
        private void Start()=>InitializeBirth();
        public void InitializeBirth()
        {
            if(_born)return;_born=true;
            _genes.CurrentAge=AgeState.Infant;
            _genes.TryStageMutation(AgeState.Infant);_genes.Recalculate();
        }
        public void Advance(float dt)
        {
            if(dt<=0 || HasDied)return;
            InitializeBirth();
            if(_stats.TryGet("health",out float hp) && hp<=0)return;
            var settings=LifeCycleSettings.Load();
            // 孕育仍可抑制幼年成长；老化不能因为基因失效而无限暂停。
            float speed=_genes.CurrentAge==AgeState.Elder
                ?Mathf.Max(settings.minimumAgingSpeed,_genes.GetAttribute("agingSpeed"))*settings.agingProgressScale
                :Mathf.Max(0,_genes.GetAttribute("growthSpeed"))*settings.growthProgressScale;
            Progress=Mathf.Min(settings.deathProgress,Progress+speed*dt);
            if(Progress>=settings.adultProgress && _genes.CurrentAge==AgeState.Infant)EnterStage(AgeState.Adult);
            if(Progress>=settings.elderProgress && _genes.CurrentAge==AgeState.Adult)EnterStage(AgeState.Elder);
            if(Progress>=settings.deathProgress)
            {
                // ★ 修复（防Bug回归）：寿命终点直接结算死亡，不能靠缓慢扣血与修复互相抵消。
                HasDied=true;_stats.TryGet("health",out hp);_stats.Modify("health",-Mathf.Max(0,hp),false);
                GetComponent<EntityManualControl>()?.Release();
                gameObject.SetActive(false);Destroy(gameObject);
            }
        }
        // ★ 适配：读档直接恢复寿命，不运行出生初始化。
        public void RestoreProgress(float progress){Progress=progress;HasDied=false;_born=true;}
        private void EnterStage(AgeState stage)
        {_genes.CurrentAge=stage;_genes.TryStageMutation(stage);_genes.Recalculate();}
    }
}
