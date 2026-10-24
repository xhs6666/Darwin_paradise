namespace DarwinPark.AI
{
    public interface IGeneAIModule
    {
        string ModuleId { get; }
        void Initialize(EntityAIContext context);
        void Tick(float deltaTime);
        void OnDisable();
    }
}