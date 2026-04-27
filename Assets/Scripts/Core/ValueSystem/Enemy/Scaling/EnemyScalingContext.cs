/// <summary>
/// 敌人缩放上下文
/// 
/// 说明：
/// 1.业务层（Enemy/DifficultySystem）只负责提供EnemyConfig、波次、总波次、是否无尽、ModeConfig等输入
/// 2.具体缩放曲线与公式必须封装在EnemyScalingStrategy（SO）中
/// </summary>
public readonly struct EnemyScalingContext
{
    public readonly EnemyConfig enemyConfig;
    public readonly ModeConfig modeConfig;
    public readonly int wave;
    public readonly int totalWaves;
    public readonly bool isEndless;

    /// <summary>
    /// 构造缩放上下文；波次与总波次会钳制到至少为 1。
    /// </summary>
    public EnemyScalingContext(EnemyConfig enemyConfig, ModeConfig modeConfig, int wave, int totalWaves, bool isEndless)
    {
        this.enemyConfig = enemyConfig;
        this.modeConfig = modeConfig;
        this.wave = wave < 1 ? 1 : wave;
        this.totalWaves = totalWaves < 1 ? 1 : totalWaves;
        this.isEndless = isEndless;
    }
}
