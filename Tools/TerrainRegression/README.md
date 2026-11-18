# TerrainRegression

8 组地形基准在本轮重构前记录，覆盖不同种子、细分、全海/全陆、侵蚀关闭以及水系开关。比较六面高度、最终顶点和三角形、水系河床、水位和下游拓扑；输出必须逐位一致。

这些历史基准固定地貌版本 2，不能用新世界默认版本变更覆盖旧档结果。第三版河网和盆地湖泊另由 Unity `DarwinPark.EditorTools.HydrologyRegression.RunBatch` 检查无环、守恒、真实网格、曲线节点与盆地蓄水，并输出新旧水位图。

`prepare.py` 从当前生产源码生成托管测试适配器，只跳过依赖 Unity 原生 `Vector3.Slerp` 的导航建图。导航由已有 `CoordinateRegression` 覆盖；此测试不替代真实 Unity 网格碰撞、物理与游戏 FPS 验证。

使用统一入口 `./Tools/verify_server.ps1`。基准文件 `expected.json` 不应随意重录，只有明确的算法改动并经审核后才更新。
