/// <summary>
/// 经验值计算策略。
/// 业务脚本禁止出现经验值公式：经验值计算交由 ValueStrategy（SO）完成。
/// </summary>
public abstract class ExpCalculationStrategy : ValueStrategy
{
    /// <summary>
    /// 计算从指定等级升到下一级所需的经验值
    /// </summary>
    public abstract float Evaluate(in ExpCalculationContext ctx);

    /// <summary>
    /// 便捷方法：直接获取经验值
    /// </summary>
    public float GetExpForLevel(in ExpCalculationContext ctx)
    {
        return Evaluate(ctx);
    }
}
