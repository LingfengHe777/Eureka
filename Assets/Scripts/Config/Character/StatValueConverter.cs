/// <summary>
/// 属性值在配置、显示与内部计算之间的转换
/// </summary>
public static class StatValueConverter
{
    /// <summary>
    /// 判断该属性在配置中是否按百分数表示。
    /// </summary>
    public static bool IsPercentStat(StatType statType)
    {
        return statType == StatType.HealthSteal
            || statType == StatType.Dodge
            || statType == StatType.Damage
            || statType == StatType.AttackSpeed
            || statType == StatType.CritChance;
    }

    /// <summary>
    /// 将配置中的数值转为计算用数值（百分数属性会除以100）
    /// </summary>
    public static float ToCalculationValue(StatType statType, float configuredValue)
    {
        return IsPercentStat(statType) ? configuredValue * 0.01f : configuredValue;
    }

    /// <summary>
    /// 将计算结果转为展示用数值（百分数属性会乘以100）
    /// </summary>
    public static float ToDisplayValue(StatType statType, float calculatedValue)
    {
        return IsPercentStat(statType) ? calculatedValue * 100f : calculatedValue;
    }

    /// <summary>
    /// 将配置中的百分数转为比例（如 10 → 0.1）
    /// </summary>
    public static float PercentModifierToRatio(float configuredPercent)
    {
        return configuredPercent * 0.01f;
    }
}
