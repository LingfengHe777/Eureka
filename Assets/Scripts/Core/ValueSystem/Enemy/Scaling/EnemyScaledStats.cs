/// <summary>
/// 敌人缩放倍率（纯数据结构体）
/// 用于与EnemyConfig的基础值相乘得到最终属性
/// </summary>
public readonly struct EnemyScaledStats
{
    public readonly float healthMultiplier;
    public readonly float damageMultiplier;
    public readonly float speedMultiplier;
    public readonly float attackCooldownMultiplier;

    /// <summary>
    /// 构造各属性倍率（与 EnemyConfig 基础值相乘）。
    /// </summary>
    public EnemyScaledStats(float healthMultiplier, float damageMultiplier, float speedMultiplier, float attackCooldownMultiplier)
    {
        this.healthMultiplier = healthMultiplier;
        this.damageMultiplier = damageMultiplier;
        this.speedMultiplier = speedMultiplier;
        this.attackCooldownMultiplier = attackCooldownMultiplier;
    }
}
