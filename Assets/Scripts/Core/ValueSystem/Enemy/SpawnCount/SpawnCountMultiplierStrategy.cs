/// <summary>
/// 生成数量倍率策略（输出"倍率"，让 WaveManager 脚本保持纯净，不出现乘法/幂等公式）。
/// 策略返回倍率，WaveManager 脚本只需将倍率乘以基础数量即可。
/// </summary>
public abstract class SpawnCountMultiplierStrategy : ValueStrategy
{
    /// <summary>
    /// 评估生成数量倍率（根据 wave/mode/endless 计算倍率）
    /// </summary>
    public abstract float Evaluate(in SpawnCountMultiplierContext ctx);

    /// <summary>
    /// 便捷方法：直接获取倍率
    /// </summary>
    public float GetSpawnCountMultiplier(in SpawnCountMultiplierContext ctx)
    {
        return Evaluate(ctx);
    }
}
