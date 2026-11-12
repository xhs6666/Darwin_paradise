# 游戏性能采样

entities=998 resources=6887 resolution=2560x1385 graphics=Direct3D12 limit=0 foodCacheHits=36921 foodSearches=27568 groundDirect=50219 groundCrossReuse=106836 resourceIndexRebuilds=0 corpseSlotWrites=2764 corpseInstances=12534 corpseBatchesPreviousFrame=100

脚本/方法排行只统计已覆盖的主线程计时范围。自身耗时扣除已计时子调用；包含耗时不能相加。未覆盖脚本、引擎、等待及采样本身仍计入实际帧耗时。

GPU Frame Time：平均 9.914 ms，P95 11.973 ms，最大 15.900 ms。
Frame elapsed：平均 71.864 ms，P95 83.302 ms，最大 96.058 ms。
Main Thread：平均 71.116 ms，P95 83.283 ms，最大 96.035 ms。

## 脚本自身开销排行

| 脚本 | 平均ms/帧 | P95 | 最大帧 | 帧占比 | 已计时调用 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Assets/DarwinPark/Entities/PlanetGroundPhysics.cs | 6.494 | 7.697 | 8.822 | 9.037% | 401363 |
| Assets/DarwinPark/Entities/EntityGeneController.cs | 6.032 | 11.479 | 26.901 | 8.394% | 109334 |
| Assets/DarwinPark/Entities/Ecology/CorpseInstanceRenderer.cs | 4.979 | 5.277 | 5.391 | 6.928% | 224 |
| Assets/DarwinPark/Entities/Vitals/EntityVitals.cs | 4.567 | 6.057 | 6.981 | 6.355% | 111148 |
| Assets/DarwinPark/Entities/Logic/LocomotionGeneLogic.cs | 4.199 | 5.173 | 5.598 | 5.843% | 41650 |
| Assets/DarwinPark/Entities/Ecology/AntColonyMember.cs | 4.081 | 9.587 | 12.094 | 5.679% | 38453 |
| Assets/DarwinPark/Entities/Simulation/EntityWorkScheduler.cs | 3.747 | 4.285 | 7.068 | 5.215% | 10630 |
| Assets/DarwinPark/Entities/Ecology/ResourceCoordinates.cs | 3.639 | 6.376 | 8.047 | 5.064% | 58011 |
| Assets/DarwinPark/Entities/Vitals/EntityEnvironmentHazard.cs | 3.018 | 3.761 | 4.415 | 4.200% | 109618 |
| Assets/DarwinPark/Entities/EntityGroundProbe.cs | 2.053 | 2.394 | 2.769 | 2.856% | 314008 |
| Assets/DarwinPark/Planet/Terrain/LandPathScheduler.cs | 2.023 | 2.245 | 2.888 | 2.815% | 67105 |
| Assets/DarwinPark/Entities/Ecology/EntityNeighborhood.cs | 1.900 | 3.310 | 4.370 | 2.643% | 134 |
| Assets/DarwinPark/Entities/Ecology/ColonyFeeding.cs | 1.811 | 2.841 | 3.634 | 2.520% | 69461 |
| Assets/DarwinPark/Entities/EntitySurfaceAnchor.cs | 1.497 | 1.745 | 1.961 | 2.083% | 401363 |
| Assets/DarwinPark/Entities/Ecology/PopulationBehavior.cs | 1.236 | 1.312 | 1.360 | 1.720% | 38956 |
| Assets/DarwinPark/Entities/Ecology/ColonyFoodCandidates.cs | 1.059 | 1.692 | 2.169 | 1.473% | 27568 |
| Assets/DarwinPark/Entities/Logic/NutritionGeneLogic.cs | 1.008 | 1.105 | 1.225 | 1.403% | 68770 |
| Assets/DarwinPark/Entities/Ecology/ColonyFeedDecision.cs | 0.917 | 1.060 | 1.166 | 1.276% | 105681 |
| Assets/DarwinPark/Entities/Logic/ReproductionGeneLogic.cs | 0.840 | 0.961 | 1.487 | 1.169% | 83805 |
| Assets/DarwinPark/Entities/Ecology/ColonyFormation.cs | 0.735 | 0.814 | 0.841 | 1.023% | 27694 |
| Assets/DarwinPark/Entities/Ecology/EntityCoordinateGrid.cs | 0.280 | 0.586 | 0.744 | 0.390% | 1099 |
| Assets/DarwinPark/Entities/Ecology/PopulationArchive.cs | 0.239 | 1.863 | 2.088 | 0.333% | 15 |
| Assets/DarwinPark/Entities/Ecology/EntityStateController.cs | 0.163 | 0.176 | 0.258 | 0.227% | 12220 |
| Assets/DarwinPark/Entities/Ecology/EntityPerceptionHub.cs | 0.061 | 0.129 | 0.192 | 0.084% | 339 |
| Assets/DarwinPark/Entities/Ecology/EnergyBlockGradeVisual.cs | 0.049 | 0.212 | 0.234 | 0.069% | 17507 |
| Assets/DarwinPark/Entities/Ecology/EcologyReproduction.cs | 0.043 | 0.303 | 0.401 | 0.060% | 24 |
| Assets/DarwinPark/Entities/Simulation/SingletonSettlement.cs | 0.029 | 0.054 | 0.607 | 0.041% | 350 |
| Assets/DarwinPark/Entities/Ecology/MateCoordinates.cs | 0.025 | 0.201 | 0.220 | 0.035% | 336 |
| Assets/DarwinPark/UI/SimulationControlPanel.cs | 0.023 | 0.026 | 0.032 | 0.031% | 112 |
| Assets/DarwinPark/Entities/PopulationMaintenance.cs | 0.006 | 0.007 | 0.009 | 0.008% | 15554 |
| Assets/DarwinPark/Entities/OffspringPlacement.cs | 0.001 | 0.009 | 0.013 | 0.002% | 30 |
| Assets/DarwinPark/Entities/Simulation/MutationRoundController.cs | 0.001 | 0.001 | 0.002 | 0.001% | 112 |
| Assets/DarwinPark/Entities/Simulation/CorpseCleanupController.cs | 0.000 | 0.001 | 0.001 | 0.000% | 112 |
| Assets/DarwinPark/Entities/EntitySpawnCapture.cs | 0.000 | 0.000 | 0.000 | 0.000% | 24 |

## 方法与算法排行

| 方法 | 算法范围 | 自身ms/帧 | 包含ms/帧 | P95自身 | 最大单次 | 调用 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Assets/DarwinPark/Entities/PlanetGroundPhysics.cs::Advance | Gravity | 6.494 | 8.069 | 7.697 | 0.318 | 401363 |
| Assets/DarwinPark/Entities/EntityGeneController.cs::AdvanceSimulation | Genes | 5.385 | 16.919 | 5.778 | 6.368 | 109206 |
| Assets/DarwinPark/Entities/Vitals/EntityVitals.cs::AdvanceVitals | Vitals | 4.567 | 4.567 | 6.057 | 0.757 | 111148 |
| Assets/DarwinPark/Entities/Ecology/CorpseInstanceRenderer.cs::LateUpdate | CorpseBatches | 4.235 | 4.235 | 4.512 | 4.558 | 112 |
| Assets/DarwinPark/Entities/Logic/LocomotionGeneLogic.cs::OnUpdate | Locomotion | 4.199 | 9.027 | 5.173 | 1.652 | 41650 |
| Assets/DarwinPark/Entities/Ecology/AntColonyMember.cs::Advance | ColonyTasks | 4.081 | 11.712 | 9.587 | 23.826 | 38453 |
| Assets/DarwinPark/Entities/Ecology/ResourceCoordinates.cs::Query | ResourceSearch | 3.614 | 3.614 | 6.339 | 0.316 | 57987 |
| Assets/DarwinPark/Entities/Vitals/EntityEnvironmentHazard.cs::AdvanceShared | Environment | 3.018 | 3.129 | 3.761 | 5.919 | 109618 |
| Assets/DarwinPark/Entities/Simulation/EntityWorkScheduler.cs::Advance | Scheduler | 2.998 | 20.740 | 3.449 | 29.225 | 112 |
| Assets/DarwinPark/Entities/EntityGroundProbe.cs::Read | GroundQuery | 2.053 | 2.053 | 2.394 | 0.107 | 314008 |
| Assets/DarwinPark/Entities/Ecology/EntityNeighborhood.cs::Ensure | Neighborhood | 1.900 | 1.900 | 3.310 | 2.000 | 134 |
| Assets/DarwinPark/Entities/Ecology/ColonyFeeding.cs::Find | Feeding | 1.811 | 6.312 | 2.841 | 0.818 | 69461 |
| Assets/DarwinPark/Entities/EntitySurfaceAnchor.cs::FixedUpdate | GroundAnchor | 1.497 | 1.976 | 1.745 | 0.084 | 401363 |
| Assets/DarwinPark/Planet/Terrain/LandPathScheduler.cs::Resolve | LandRouteFollow | 1.321 | 1.321 | 1.497 | 0.117 | 39299 |
| Assets/DarwinPark/Entities/Ecology/PopulationBehavior.cs::Advance | Population | 1.236 | 12.948 | 1.312 | 23.831 | 38956 |
| Assets/DarwinPark/Entities/Ecology/ColonyFoodCandidates.cs::Fill | FoodCandidates | 1.059 | 3.864 | 1.692 | 0.812 | 27568 |
| Assets/DarwinPark/Entities/Logic/NutritionGeneLogic.cs::OnUpdate | Nutrition | 1.008 | 1.171 | 1.105 | 1.843 | 68770 |
| Assets/DarwinPark/Entities/Ecology/ColonyFeedDecision.cs::Read | FeedDecision | 0.917 | 0.917 | 1.060 | 0.089 | 105681 |
| Assets/DarwinPark/Entities/Logic/ReproductionGeneLogic.cs::OnUpdate | Reproduction | 0.840 | 0.980 | 0.961 | 6.350 | 83805 |
| Assets/DarwinPark/Entities/Simulation/EntityWorkScheduler.cs::Signature | GeneSignature | 0.749 | 0.749 | 1.068 | 3.104 | 10518 |
| Assets/DarwinPark/Entities/Ecology/CorpseInstanceRenderer.cs::Draw | CorpseBatches | 0.744 | 0.744 | 1.006 | 1.120 | 112 |
| Assets/DarwinPark/Entities/Ecology/ColonyFormation.cs::Avoid | Formation | 0.735 | 0.750 | 0.814 | 1.626 | 27694 |
| Assets/DarwinPark/Planet/Terrain/LandPathScheduler.cs::SafeVelocity | ShoreGuard | 0.668 | 0.668 | 0.720 | 0.137 | 27694 |
| Assets/DarwinPark/Entities/EntityGeneController.cs::Recalculate | GeneRecalculation | 0.648 | 0.648 | 6.010 | 11.448 | 128 |
| Assets/DarwinPark/Entities/Ecology/EntityCoordinateGrid.cs::Query | CoordinateGrid | 0.280 | 1.977 | 0.586 | 2.141 | 1099 |
| Assets/DarwinPark/Entities/Ecology/PopulationArchive.cs::Observe | PopulationArchive | 0.239 | 0.239 | 1.863 | 2.088 | 15 |
| Assets/DarwinPark/Entities/Ecology/EntityStateController.cs::Advance | StatePerception | 0.163 | 0.163 | 0.176 | 0.094 | 12220 |
| Assets/DarwinPark/Entities/Ecology/EntityPerceptionHub.cs::Read | Perception | 0.061 | 0.957 | 0.129 | 2.159 | 339 |
| Assets/DarwinPark/Entities/Ecology/EnergyBlockGradeVisual.cs::Refresh | ResourceVisuals | 0.049 | 0.049 | 0.212 | 0.022 | 17507 |
| Assets/DarwinPark/Entities/Ecology/EcologyReproduction.cs::Spawn | BirthFactory | 0.043 | 0.500 | 0.303 | 11.720 | 24 |
| Assets/DarwinPark/Planet/Terrain/LandPathScheduler.cs::LateUpdate | LandPathSearch | 0.034 | 0.034 | 0.072 | 0.176 | 112 |
| Assets/DarwinPark/Entities/Simulation/SingletonSettlement.cs::Advance | SingletonSettlement | 0.029 | 0.071 | 0.054 | 0.596 | 350 |
| Assets/DarwinPark/Entities/Ecology/MateCoordinates.cs::Find | MateSearch | 0.025 | 0.029 | 0.201 | 0.264 | 336 |
| Assets/DarwinPark/Entities/Ecology/ResourceCoordinates.cs::Food | ResourceSearch | 0.025 | 0.025 | 0.135 | 0.152 | 24 |
| Assets/DarwinPark/UI/SimulationControlPanel.cs::Update | BottomUI | 0.023 | 0.023 | 0.026 | 0.032 | 112 |
| Assets/DarwinPark/Entities/PopulationMaintenance.cs::BreedingScale | BreedingMaintenance | 0.006 | 0.006 | 0.007 | 0.003 | 15554 |
| Assets/DarwinPark/Entities/OffspringPlacement.cs::TryGetPosition | BirthPlacement | 0.001 | 0.001 | 0.009 | 0.007 | 30 |
| Assets/DarwinPark/Entities/Simulation/MutationRoundController.cs::Advance | MutationRounds | 0.001 | 0.001 | 0.001 | 0.002 | 112 |
| Assets/DarwinPark/Entities/Simulation/CorpseCleanupController.cs::Advance | CorpseCleanup | 0.000 | 0.000 | 0.001 | 0.001 | 112 |
| Assets/DarwinPark/Entities/EntitySpawnCapture.cs::Create | BirthIdentity | 0.000 | 0.000 | 0.000 | 0.000 | 24 |

## 引擎分项（包含关系，不能直接相加）

Gfx.WaitForRenderThread：0.000 ms，最大 0.000
Gfx.WaitForPresentOnGfxThread：0.002 ms，最大 0.007
GC.Collect：0.205 ms，最大 4.660
Main Thread：71.116 ms，最大 96.035
PlayerLoop：71.114 ms，最大 96.033
WaitForTargetFPS：0.000 ms，最大 0.000
Draw Calls Count：3366.850 Count，最大 7202.000
Triangles Count：12080266.558 Count，最大 26977296.000
SetPass Calls Count：1154.469 Count，最大 1614.000
Render Thread：本平台未提供有效数据
BehaviourUpdate：本平台未提供有效数据
FixedBehaviourUpdate：本平台未提供有效数据
Physics.Simulate：本平台未提供有效数据
Physics.Processing：本平台未提供有效数据
Physics.FetchResults：本平台未提供有效数据
GC Allocated In Frame：本平台未提供有效数据
Gfx.PresentFrame：本平台未提供有效数据
Gfx.ProcessCommands：本平台未提供有效数据
Camera.Render：本平台未提供有效数据
RenderPipelineManager.DoRenderLoop_Internal：本平台未提供有效数据
RenderLoop.Draw：本平台未提供有效数据
Canvas.BuildBatch：本平台未提供有效数据
Canvas.SendWillRenderCanvases：本平台未提供有效数据
GUI.Repaint：本平台未提供有效数据
EditorLoop：本平台未提供有效数据
SceneView.Repaint：本平台未提供有效数据
