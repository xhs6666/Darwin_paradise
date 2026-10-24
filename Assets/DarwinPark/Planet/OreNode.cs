using UnityEngine;

namespace DarwinPark.Planet // ★ 改为 Planet
{
    public class OreNode : MonoBehaviour
    {
        public float maxNutrient = 100f;
        public float currentNutrient = 100f;
        public float nutrientDensity = 1.0f; // 密度倍率

        public float Extract(float amount)
        {
            if (currentNutrient <= 0) return 0f;

            float actualExtract = Mathf.Min(amount, currentNutrient);
            currentNutrient -= actualExtract;

            // 根据剩余量调整模型大小（视觉反馈）
            float scale = Mathf.Max(0.1f, currentNutrient / maxNutrient);
            transform.localScale = Vector3.one * scale;

            if (currentNutrient <= 0.1f)
            {
                Destroy(gameObject); // 被吸干了就销毁
            }
            return actualExtract;
        }
    }
}