using UnityEngine;

/// <summary>
/// 生成数量倍率策略（默认实现）：
/// - 倍率 = 无尽模式增长（仅在进入无尽后生效）
/// - 最终数量 = 基础数量 × 倍率（在 WaveManager 脚本中计算）
/// 
/// 注意：所有数学公式都在策略 SO 内部，业务脚本禁止出现这些公式。
/// </summary>
[CreateAssetMenu(fileName = "SpawnCountMultiplierStrategy_EndlessGrowth", menuName = "Eureka/ValueStrategy/Enemy/SpawnCountMultiplier/EndlessGrowth")]
public class SpawnCountMultiplierStrategy_EndlessGrowth : SpawnCountMultiplierStrategy
{
    public override float Evaluate(in SpawnCountMultiplierContext ctx)
    {
        if (!ctx.isEndless || ctx.wave <= ctx.totalWaves) return 1f;

        if (ctx.modeConfig == null) return 1f;

        int endlessWave = ctx.wave - ctx.totalWaves;
        int k = Mathf.Max(0, endlessWave - 1);

        return Mathf.Pow(ctx.modeConfig.endlessSpawnCountMultiplierGrowth, k);
    }
}
