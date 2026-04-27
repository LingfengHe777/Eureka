/// <summary>
/// 敌人缩放策略
/// 输出倍率，让Enemy脚本保持纯净，不出现乘法/幂等公式
/// 策略返回倍率，Enemy脚本只需将倍率乘以EnemyConfig的基础值即可。
/// </summary>
public abstract class EnemyScalingStrategy : ValueStrategy
{
    /// <summary>
    /// 评估缩放倍率
    /// 不依赖EnemyConfig，只根据wave/mode/endless计算倍率
    /// </summary>
    public abstract EnemyScaledStats Evaluate(in EnemyScalingContext ctx);

    /// <summary>
    /// 直接获取倍率（用于DifficultySystem，不需要 EnemyConfig）
    /// </summary>
    public EnemyScaledStats ScaleEnemyStats(in EnemyScalingContext ctx)
    {
        return Evaluate(ctx);
    }
}
