using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>让星球缓慢自转。</summary>
    public class PlanetRotator : MonoBehaviour
    {
        public float rotationSpeed = 10f;

        private void Update()
        {
            // ★ 修复（防Bug回归）：暂停与加载期间星球不推进；菜单背景另用预录视频。
            float dt=DarwinPark.Core.ServiceLocator.TryGet<DarwinPark.Core.SimulationClock>(out var clock)?clock.GameDeltaTime:Time.deltaTime;
            transform.Rotate(Vector3.up, rotationSpeed * dt);
        }
    }
}