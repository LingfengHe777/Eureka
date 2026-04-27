/// <summary>
/// 生成数量倍率计算上下文（强类型、无分配）。
/// 业务层负责组装上下文，具体公式由 SpawnCountMultiplierStrategy 实现。
/// </summary>
public readonly struct SpawnCountMultiplierContext
{
    public readonly int wave;
    public readonly int totalWaves;
    public readonly bool isEndless;
    public readonly ModeConfig modeConfig;

    /// <summary>
    /// 构造生成数量倍率上下文；波次与总波次会钳制到至少为 1。
    /// </summary>
    public SpawnCountMultiplierContext(int wave, int totalWaves, bool isEndless, ModeConfig modeConfig)
    {
        this.wave = wave < 1 ? 1 : wave;
        this.totalWaves = totalWaves < 1 ? 1 : totalWaves;
        this.isEndless = isEndless;
        this.modeConfig = modeConfig;
    }
}
