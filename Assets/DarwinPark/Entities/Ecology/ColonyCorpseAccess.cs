namespace DarwinPark.Entities.Ecology
{
    // ★ 新增：自然死亡尸体可由工匠领取；已有存活种群的专属尸体仍受保护，独占领取防止重复转化。
    public static class ColonyCorpseAccess
    {
        public static bool Available(ColonyResource resource,AntColonyMember worker)=>resource!=null&&resource.State.kind==ColonyResourceKind.Corpse&&
            (string.IsNullOrEmpty(resource.State.group)||resource.State.group==worker.GroupId||!ColonyResourceOwnership.HasOwner(resource.State.group));
        public static bool HasLiveClaim(ColonyResource resource)
        {
            var worker=EcologyIdentity.Find(resource.State.claim)?.GetComponent<AntColonyMember>();
            return worker!=null&&worker.Active&&worker.Role==AntRole.Worker&&(worker.State.resourceTarget==resource.State.id||worker.State.carriedResource==resource.State.id);
        }
    }
}
