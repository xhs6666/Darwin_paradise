using UnityEngine;

namespace DarwinPark.Genetics.Mutation
{
    public static class GeneInteractionSpace
    {
        public static float InteractionProbability(Vector3 a, Vector3 b, float maxDistance)
        {
            if (maxDistance <= 0f) return 0f;
            float d = Vector3.Distance(a, b);
            if (d >= maxDistance) return 0f;
            return 1f - d / maxDistance;
        }
    }
}