using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>让星球缓慢自转。</summary>
    public class PlanetRotator : MonoBehaviour
    {
        public float rotationSpeed = 10f;

        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}