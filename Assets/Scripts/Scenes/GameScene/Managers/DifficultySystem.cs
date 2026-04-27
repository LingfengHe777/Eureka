using UnityEngine;

/// <summary>
/// 按 ModeConfig 与波次计算敌人倍率、刷怪倍率等；缩放公式在 Strategy SO 内。
/// </summary>
public class DifficultySystem
{
    private static DifficultySystem instance;
    public static DifficultySystem Instance => instance;

    private readonly ModeConfig modeConfig;

    private readonly int totalWaves;
    private int currentWave;
    private bool isEndless;

    /// <summary>
    /// 生命/伤害/速度/攻击冷却倍率。
    /// </summary>
    public struct EnemyMultipliers
    {
        public float health;
        public float damage;
        public float speed;
        public float attackCooldown;
    }

    private DifficultySystem(ModeConfig mode, int totalWaves)
    {
        this.modeConfig = mode;
        this.totalWaves = totalWaves;
        this.currentWave = 0;
        this.isEndless = false;
    }

    /// <summary>
    /// 开局由 WaveManager 调用。
    /// </summary>
    public static void Create(ModeConfig mode, int totalWaves)
    {
        if (mode == null)
        {
            return;
        }

        instance = new DifficultySystem(mode, totalWaves);
    }

    /// <summary>
    /// 局末由 WaveManager 调用。
    /// </summary>
    public static void Shutdown()
    {
        instance = null;
    }

    /// <summary>
    /// 每波开始更新波次与无尽标记。
    /// </summary>
    public void UpdateWaveState(int wave, bool endless)
    {
        currentWave = wave;
        isEndless = endless;
    }

    /// <summary>
    /// 当前波敌人属性倍率，基础 × 无尽等由Strategy决定
    /// </summary>
    public EnemyMultipliers GetEnemyMultipliers()
    {
        if (modeConfig.enemyScalingStrategy == null)
        {
            return new EnemyMultipliers { health = 1f, damage = 1f, speed = 1f, attackCooldown = 1f };
        }

        var context = new EnemyScalingContext(null, modeConfig, currentWave, totalWaves, isEndless);
        var scaledStats = modeConfig.enemyScalingStrategy.ScaleEnemyStats(context);

        return new EnemyMultipliers
        {
            health = scaledStats.healthMultiplier,
            damage = scaledStats.damageMultiplier,
            speed = scaledStats.speedMultiplier,
            attackCooldown = scaledStats.attackCooldownMultiplier
        };
    }

    /// <summary>
    /// 刷怪数量偏移。
    /// </summary>
    public int GetSpawnCountOffset()
    {
        return modeConfig.spawnCountOffset;
    }

    /// <summary>
    /// 刷怪数量倍率，无尽增长等在Strategy内处理
    /// </summary>
    public float GetSpawnCountMultiplier()
    {
        if (modeConfig.spawnCountMultiplierStrategy == null)
        {
            return 1f;
        }

        var context = new SpawnCountMultiplierContext(currentWave, totalWaves, isEndless, modeConfig);
        return modeConfig.spawnCountMultiplierStrategy.GetSpawnCountMultiplier(context);
    }

    public float GetMaterialDropRate() => modeConfig.materialDropRate;
    public float GetShopPriceMultiplier() => modeConfig.shopPriceMultiplier;
    public float GetHealingReduction() => modeConfig.healingReduction;

    /// <summary>
    /// 调试摘要。
    /// </summary>
    public string GetDifficultySummary()
    {
        var m = GetEnemyMultipliers();
        float spawnMul = GetSpawnCountMultiplier();
        return $"难度 - 血量:{m.health:F2}, 伤害:{m.damage:F2}, 速度:{m.speed:F2}, " +
               $"冷却:{m.attackCooldown:F2}, 数量:{spawnMul:F2}";
    }
}
