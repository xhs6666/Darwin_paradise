namespace DarwinPark.AI
{
    // ★ 新增：纯兼容模块没有每帧行为；仅显式声明的模块才可跳过Tick，扩展模块默认继续更新。
    public interface IPassiveGeneAIModule : IGeneAIModule { }

    public interface IGeneAIModule
    {
        string ModuleId { get; }
        void Initialize(EntityAIContext context);
        void Tick(float deltaTime);
        void OnDisable();
    }
}