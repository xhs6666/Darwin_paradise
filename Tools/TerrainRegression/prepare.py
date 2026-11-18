"""构建托管地形回归适配器：仅跳过需要 Unity 原生 Slerp 的导航构建。"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
output = root / 'Artifacts/TerrainRegression'
output.mkdir(parents=True, exist_ok=True)
terrain = root / 'Assets/DarwinPark/Planet/Terrain'
source = (terrain / 'PlanetTerrainGenerator.cs').read_text(encoding='utf-8')
source, count = re.subn(r'data\.Navigation=new LandNavigationGraph\([^;]+;', '// Native navigation is covered by CoordinateRegression.', source)
assert count == 1, 'Native boundary changed; review regression adapter'
(output / 'ManagedTerrainGenerator.cs').write_text(source.replace('PlanetTerrainGenerator', 'ManagedTerrainGenerator'), encoding='utf-8')
height = terrain / 'PlanetHeightField.cs'
(output / 'ManagedHeightField.cs').write_text(height.read_text(encoding='utf-8').replace('PlanetTerrainGenerator', 'ManagedTerrainGenerator') if height.exists() else '', encoding='utf-8')
