namespace DarwinPark.Entities.Ecology
{
    // ★ 修复（防Bug回归）：当前移动修饰仅乘法/归零/限幅；基础为零时不必为比例统计反复查询群体、光环和搬运状态。
    public static class EcologyMobility
    {
        public static bool CanMove(EntityGeneController genes)
        {
            string attribute=EcologyBalanceSettings.Current.movementAttribute;
            return genes!=null&&genes.Evaluation!=null&&genes.Evaluation.Attributes.TryGetValue(attribute,out var value)&&value>0&&genes.GetAttribute(attribute)>0;
        }
    }
}
