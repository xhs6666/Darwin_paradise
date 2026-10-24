namespace DarwinPark.Core
{
    public static class ClimateDataHub
    {
        // ★ 核心修复：提供合理的默认值，防止未初始化时全变冰
        public static float BaseTemperature = 20f;
        public static float BaseHumidity = 44f;

        // ★ 新增：季节偏移。EnvironmentSystem 每帧写入，Planet 只依赖 Core，避免程序集循环引用。
        public static float SeasonalTemperatureOffset = 0f;
        public static float SeasonalHumidityOffset = 0f;
    }
}