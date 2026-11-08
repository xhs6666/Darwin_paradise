namespace DarwinPark.Entities.Simulation
{
    // ★ 适配：全局入口转入独立轮次控制器，出生和生命阶段不再请求单独的随机许可。
    public static class GlobalMutationManager
    {
        public static void Advance(float simulationSeconds)=>MutationRoundController.Advance(simulationSeconds);
    }
}
