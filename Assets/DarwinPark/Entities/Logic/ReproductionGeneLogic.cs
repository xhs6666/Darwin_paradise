using DarwinPark.Entities.Simulation; // ★ 适配：同步模拟批内复用组件，不缓存实时状态数值。
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DarwinPark.Genetics;
using DarwinPark.Entities.Stats;

namespace DarwinPark.Entities.Logic
{
    public class ReproductionGeneLogic : IGeneLogic
    {
        private EntityGeneController _controller;
        private GeneInstance _gene;

        private float _gestationTimer = 0f;
        private float _cooldownTimer = 0f;
        private float _mateWaitTimer;
        private bool _parthenogenetic;
        private float _maintenanceWait;
        // ★ 适配：固定本胎总数和剩余数量，满额/空间不足时保留，不重复生产已出生子代。
        private MultipleBirthState _litter;

        private LifeCycleSettings _settings;

        private Genome _partnerGenome = null;
        private readonly Ecology.MateCoordinates _mateCoordinates=new();


        public void Initialize(EntityGeneController controller, GeneInstance gene)
        {
            _controller = controller;
            _gene = gene;
            _settings=LifeCycleSettings.Load();
        }

        // ★ 适配：孕育计时、产后冷却与配偶基因组一起保存，不能读档后立即生产或丢失后代遗传。
        public ReproductionSaveState CaptureState()=>new ReproductionSaveState{gene=_gene.InstanceId,gestation=_gestationTimer,cooldown=_cooldownTimer,partner=_partnerGenome,mateWait=_mateWaitTimer,maintenanceWait=_maintenanceWait,parthenogenetic=_parthenogenetic,litter=MultipleBirthPolicy.Copy(_litter)};
        // ★ 修复（防Bug回归）：Unity内联序列化可能把null配偶恢复为空Genome，不能因此把单亲孕育误判为有性。
        public void RestoreState(ReproductionSaveState state){MultipleBirthPolicy.Validate(state.litter);_litter=MultipleBirthPolicy.Copy(state.litter);_gestationTimer=state.gestation;_cooldownTimer=state.cooldown;_partnerGenome=state.partner?.Genes?.Count>0?state.partner:null;_mateWaitTimer=state.mateWait;_maintenanceWait=state.maintenanceWait;_parthenogenetic=state.parthenogenetic;}
        public void OnUpdate(float dt)
        {
            // ★ 适配：按需区分重算、签名与实际行为成本。
            using var performance=DarwinPark.Core.PerformanceTimings.Measure(DarwinPark.Core.PerformanceTimings.Area.Reproduction);
            if(dt<=0||DarwinPark.Core.WorldAuthority.IsReplica)return;
            _mateCoordinates.Advance(dt);
            // ★ 适配：蚁群王后走独立孕育流程，其余分工不得运行普通繁殖逻辑。
            if(Ecology.AntColonyReproduction.BlocksOrdinary(_controller)){_mateWaitTimer=0;return;}
            // ★ 适配：基础、性别特化和卵生复制可共存，但只由最高阶有效繁殖基因推进一次孕育。
            if(!GeneBehaviorAuthority.IsPrimary<ReproductionGeneLogic>(_controller,_gene)){_mateWaitTimer=0;return;}
            var stats = ActiveSimulationBatch.Read<EntityStats>(_controller);
            if (stats == null) return;

            if (_cooldownTimer > 0) { _mateWaitTimer=0;_cooldownTimer -= dt; return; }

            if (_controller.IsGestating)
            {
                _mateWaitTimer=0;
                // ★ 适配：满额后完成的孕育等待空位，不伪造出生或重复消耗孕育营养。
                if(_gestationTimer<=0&&!Ecology.ColonyContinuityPriority.CanSpawn(_controller))return;
                _gestationTimer -= dt;
                // ★ 适配：孕育额外营养开销与生态收支共用配置，避免长孕育必然饿死。
                stats.Modify("nutrient", -Ecology.EcologyBalanceSettings.Current.gestationFoodPerSecond * dt*DarwinPark.Core.GameplayTuning.Current.baseMetabolismMultiplier);

                if (_gestationTimer <= 0f)
                {
                    if(!MultipleBirthPolicy.Deliver(ref _litter,SpawnOffspring,()=>stats.Modify("nutrient",-Ecology.EcologyBalanceSettings.Current.birthFoodCost)))return;
                    _controller.IsGestating = false;
                    _controller.Recalculate();
                    _cooldownTimer = Ecology.EcologyReproductionTiming.CooldownSeconds(_controller);
                    _litter=null;
                    _partnerGenome = null;
                    _parthenogenetic=false;
                }
                return;
            }

            // 检查成年
            // ★ 核心修改：直接判断实体自带状态
            if (_controller.CurrentAge != AgeState.Adult)
            {
                _maintenanceWait=0;
                _mateWaitTimer=0;
                // ★ 适配：条件不足合并诊断，避免幼体数量和模拟倍率放大逐实体日志。
                Ecology.EcologyDiagnostics.Blocked(Ecology.EcologyDiagnostics.ReproductionBlock.Age);
                return;
            }

            // ★ 核心修改：根据体型计算所需营养和时间
            float size = 1f;
            // ★ 适配：实际扣款与提前储粮采用同一体型下限，微型实体不会准备不足的繁殖预算。
            if (stats.TryGet("size", out var s)) size = Mathf.Max(Ecology.EcologyBalanceSettings.Current.minimumReproductionSize, s);
            float requiredNutrient = MultipleBirthPolicy.RequiredReserve(_controller, _settings.nutrientCost, size);
            // ★ 适配：超过维持目标只延长新孕育的间隔；已有孕育在上面的独立分支正常完成。
            bool maintenanceReady=PopulationMaintenance.BreedingReady(_controller,ref _maintenanceWait,dt);

            // ★ 适配：繁殖按同一份表达状态行动，不在每个模拟子步重复定位状态。
            var repState = _controller.GetEvaluation(_gene)?.State;
            bool sexual=repState?.StateId=="sexual";
            // ★ 修复（防Bug回归）：只有孤雌分支需要在缺粮时继续观察配偶；普通繁殖保留先查储备，避免成百实体无效物理扫描。
            if(!ParthenogenesisCondition.Enabled(_controller)&&(!stats.TryGet("storedNutrient",out float available)||available<requiredNutrient))
            {Ecology.EcologyDiagnostics.Blocked(Ecology.EcologyDiagnostics.ReproductionBlock.Reserve);return;}
            var mate=sexual?FindMate(requiredNutrient):null;
            // ★ 新增：成年无配偶等待独立于储粮积累；超时只免除配偶要求，不能绕过营养预算和数量上限。
            bool parthenogenetic=ParthenogenesisCondition.Advance(_controller,ref _mateWaitTimer,!sexual||mate!=null,dt);
            if(sexual&&mate==null&&!parthenogenetic){Ecology.EcologyDiagnostics.Blocked(Ecology.EcologyDiagnostics.ReproductionBlock.Mate);return;}
            if(!Ecology.ColonyContinuityPriority.CanSpawn(_controller))return;

            // ★ 检查条件：必须是储存营养 (storedNutrient) 足够
            if (!stats.TryGet("storedNutrient", out var stored) || stored < requiredNutrient)
            {
                // ★ 适配：条件不足合并诊断，避免幼体数量和模拟倍率放大逐实体日志。
                Ecology.EcologyDiagnostics.Blocked(Ecology.EcologyDiagnostics.ReproductionBlock.Reserve);
                return;
            }
            if(!maintenanceReady)return;

            if (repState != null && (repState.StateId == "asexual"||parthenogenetic))
            {
                // ★ 修复（防Bug回归）：无性孕育同样消耗储存营养，不能零成本无限生产。
                stats.Modify("storedNutrient",-requiredNutrient);
                StartGestation(stats, null, size,parthenogenetic);
            }
            else if (repState != null && repState.StateId == "sexual")
            {
                if (mate != null)
                {
                    var mateStats = mate.GetComponent<EntityStats>();
                    if (mateStats != null && mateStats.TryGet("storedNutrient", out var mateStored) && mateStored >= requiredNutrient)
                    {
                        // 双方扣除储存营养
                        stats.Modify("storedNutrient", -requiredNutrient);
                        mateStats.Modify("storedNutrient", -requiredNutrient);

                        var mateGeneCtrl = mate.GetComponent<EntityGeneController>();
                        StartGestation(stats, mateGeneCtrl.Genome, size);
                    }
                }
            }
        }

        private void StartGestation(EntityStats stats, Genome partnerGenome, float size,bool parthenogenetic=false)
        {
            _litter=MultipleBirthPolicy.Begin(_controller);
            _partnerGenome = partnerGenome;
            _parthenogenetic=parthenogenetic;_mateWaitTimer=0;
            _maintenanceWait=0;
            _controller.IsGestating = true;

            _gestationTimer=CalculateGestationTime(size);

            // ★ 修复（防Bug回归）：提示使用与孕育计时相同的配置日长，避免调整日长后显示失真。
            string mode = parthenogenetic?"孤雌生殖":partnerGenome == null ? "无性繁殖" : "有性繁殖";
            Debug.Log($"[繁殖] {_controller.gameObject.name} 开始{mode}！预计需要 {(_gestationTimer / _settings.secondsPerDay):F0} 天。");
            _controller.Recalculate();
        }

        // ★ 修复（防Bug回归）：体型/生长影响仍保留，但总孕育时长必须处于配置的15～360个游戏日。
        public float CalculateGestationTime(float size)
        {
            // ★ 修复（防Bug回归）：原计时读取遗传原值，基因抑制对繁殖周期没有效果。
            return Ecology.EcologyReproductionTiming.GestationSeconds(_controller,size);
        }

        private DarwinEntity FindMate(float requiredNutrient)
        {
            // ★ 适配：配偶改用全局坐标，不依赖物理碰撞体和每小步OverlapSphere分配。
            return _mateCoordinates.Find(_controller,_settings.mateSearchRadius,requiredNutrient);
        }

        private bool SpawnOffspring()
        {
            bool hasAction=_controller.Evaluation.Genes.Exists(g=>g.IsActive && _controller.GetLogic(g.Gene) is LocomotionGeneLogic);
            if(DarwinEntity.Spawner==null || !OffspringPlacement.TryGetPosition(_controller.transform.position,_partnerGenome==null && !hasAction,out var spawnPos))return false;
            // ★ 新增：遗传形体、离散模式和部位参数随基因一起复制，不能出生时退回默认身体。
            var childGenome=Genome.Copy(_controller.Genome);
            for(int i=0;i<childGenome.Genes.Count;i++)
            {
                var copy=childGenome.Genes[i];var original=_controller.Genome.Genes[i];copy.OriginInstanceId=original.InstanceId;
                var partnerGene=_partnerGenome?.Genes.Find(g=>g.DefinitionId==original.DefinitionId);
                if(partnerGene!=null){copy.Value=(original.Value+partnerGene.Value)*.5f;copy.StateId=Random.value<.5f?original.StateId:partnerGene.StateId;}
            }

            if (DarwinEntity.Spawner != null)
            {
                // ★ 适配：卵生后代的孵化状态独立于母体孕育，不会把卵直接当作已出生幼体。
                if(!Ecology.EcologyReproduction.Spawn(_controller,childGenome,spawnPos))return false;
            }
            Debug.Log($"[繁殖] {_controller.gameObject.name} 孕育完成，产下了后代！");

            // ★ 修复（防Bug回归）：生产不属于生命阶段切换，不再偷偷改写亲本生长基因。
            return true;
        }

        public string GetTooltipInfo()
        {
            if (_cooldownTimer > 0) return $"产后休养中: {(_cooldownTimer / _settings.secondsPerDay):F0} 天";
            if (_controller.IsGestating)
            {
                string mode = _parthenogenetic?"孤雌孕育":_partnerGenome == null ? "无性孕育" : "有性孕育";
                return $"<color=#FF6B6B>{mode}中（待出生 {_litter?.remaining??1} / {_litter?.planned??1}）: {(_gestationTimer / _settings.secondsPerDay):F0} 天</color>";
            }
            if(ParthenogenesisCondition.Enabled(_controller)&&_controller.CurrentAge==AgeState.Adult)return $"等待合适配偶: {_mateWaitTimer/_settings.secondsPerDay:F1} / {ParthenogenesisCondition.WaitSeconds/_settings.secondsPerDay:F1} 天（超时尝试孤雌孕育，需储粮）";
            return "状态: 待机（需成年且储存营养充足）";
        }
    }
    // ★ 新增：普通实体与王后使用同一连续等待规则，副本/暂停不推进，找到配偶或离开成年阶段清零。
    public static class ParthenogenesisCondition
    {
        public static float WaitSeconds=>DarwinPark.Core.GameplayTuning.Current.parthenogenesisWaitDays*LifeCycleSettings.Load().secondsPerDay;
        public static bool Enabled(EntityGeneController genes)=>Ecology.EcologyQueries.Active(genes,ParthenogenesisEvolutionUpgrade.GeneId)!=null;
        public static bool Advance(EntityGeneController genes,ref float elapsed,bool hasMate,float dt)
        {
            if(genes.CurrentAge!=AgeState.Adult||genes.IsGestating||!Enabled(genes)||hasMate){elapsed=0;return false;}
            if(dt<=0||DarwinPark.Core.WorldAuthority.IsReplica)return false;
            elapsed=Mathf.Min(WaitSeconds,elapsed+dt);return elapsed>=WaitSeconds;
        }
    }
}
