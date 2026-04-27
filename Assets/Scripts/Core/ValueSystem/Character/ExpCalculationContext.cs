/// <summary>
/// 经验值计算上下文（强类型、无分配）。
/// 业务层负责组装上下文，具体公式由 ExpCalculationStrategy 实现。
/// </summary>
public readonly struct ExpCalculationContext
{
    public readonly int currentLevel;
    public readonly float baseExp;
    public readonly float growthRate;
    public readonly int maxLevel;

    /// <summary>
    /// 构造经验计算上下文；等级与上限会钳制到至少为 1。
    /// </summary>
    public ExpCalculationContext(int currentLevel, float baseExp, float growthRate, int maxLevel)
    {
        this.currentLevel = currentLevel < 1 ? 1 : currentLevel;
        this.baseExp = baseExp;
        this.growthRate = growthRate;
        this.maxLevel = maxLevel < 1 ? 1 : maxLevel;
    }
}
