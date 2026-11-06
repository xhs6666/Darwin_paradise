namespace DarwinPark.AI.Modules
{
    // ★ 适配：实际飞行与自养由行动/营养基因逻辑承担；这两个旧模块保留类型与ID兼容，但不再空跑每帧。
    public class FlightAIModule : IPassiveGeneAIModule
    {
        public string ModuleId => "ai_flight";
        public void Initialize(EntityAIContext context) { }
        public void Tick(float deltaTime) { }
        public void OnDisable() { }
    }
    public class AutotrophAIModule : IPassiveGeneAIModule
    {
        public string ModuleId => "ai_autotroph";
        public void Initialize(EntityAIContext context) { }
        public void Tick(float deltaTime) { }
        public void OnDisable() { }
    }
}
