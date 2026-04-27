using UnityEngine;

/// <summary>
/// 经验值计算策略（默认实现）：指数增长
/// - 每级所需经验 = baseExp * (1 + growthRate) ^ (level - 1)
/// 
/// 注意：所有数学公式都在策略 SO 内部，业务脚本禁止出现这些公式。
/// </summary>
[CreateAssetMenu(fileName = "ExpCalculationStrategy_Exponential", menuName = "Eureka/ValueStrategy/Character/ExpCalculation/Exponential")]
public class ExpCalculationStrategy_Exponential : ExpCalculationStrategy
{
    public override float Evaluate(in ExpCalculationContext ctx)
    {
        if (ctx.currentLevel < 1) return ctx.baseExp;
        if (ctx.currentLevel >= ctx.maxLevel) return float.MaxValue;

        return ctx.baseExp * Mathf.Pow(1f + ctx.growthRate, ctx.currentLevel - 1);
    }
}
