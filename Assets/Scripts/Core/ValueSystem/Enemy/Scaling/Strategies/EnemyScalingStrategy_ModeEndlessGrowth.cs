using UnityEngine;

/// <summary>
/// 敌人缩放策略
/// 倍率 = ModeConfig 基础倍率 ×（无尽模式增长）
/// 最终值 = EnemyConfig 基础值 × 倍率（在Enemy脚本中计算）
/// 
/// 所有数学公式都在策略SO内部，业务脚本禁止出现这些公式。
/// </summary>
[CreateAssetMenu(fileName = "EnemyScalingStrategy_ModeEndlessGrowth", menuName = "Eureka/ValueStrategy/Enemy/Scaling/ModeEndlessGrowth")]
public class EnemyScalingStrategy_ModeEndlessGrowth : EnemyScalingStrategy
{
    public override EnemyScaledStats Evaluate(in EnemyScalingContext ctx)
    {
        ModeConfig mode = ctx.modeConfig;

        float healthMul = mode != null ? mode.enemyHealthMultiplier : 1f;
        float damageMul = mode != null ? mode.enemyDamageMultiplier : 1f;
        float speedMul = mode != null ? mode.enemySpeedMultiplier : 1f;
        float cooldownMul = mode != null ? mode.enemyAttackCooldownMultiplier : 1f;

        if (mode != null && ctx.isEndless && ctx.wave > ctx.totalWaves)
        {
            int endlessWave = ctx.wave - ctx.totalWaves;
            int k = Mathf.Max(0, endlessWave - 1);

            healthMul *= Mathf.Pow(mode.endlessHealthMultiplierGrowth, k);
            damageMul *= Mathf.Pow(mode.endlessDamageMultiplierGrowth, k);
            speedMul *= Mathf.Pow(mode.endlessSpeedMultiplierGrowth, k);
            cooldownMul *= Mathf.Pow(mode.endlessAttackCooldownMultiplierGrowth, k);
        }

        return new EnemyScaledStats(healthMul, damageMul, speedMul, cooldownMul);
    }
}
