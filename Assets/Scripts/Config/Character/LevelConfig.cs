using UnityEngine;

/// <summary>
/// 升级配置：定义升级所需经验值曲线与计算策略。
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Eureka/Character/LevelConfig")]
public class LevelConfig : ScriptableObject
{
    [Header("升级曲线")]
    [Tooltip("1 级升 2 级所需经验底数")]
    public float baseExp = 100f;

    [Range(0.1f, 2f)]
    [Tooltip("升级经验增长强度，具体公式见下方策略")]
    public float growthRate = 0.15f;

    [Tooltip("角色可达到的最高等级")]
    public int maxLevel = 100;

    [Header("数值策略 · 经验")]
    [Tooltip("升级经验计算公式，仅在此资源中配置")]
    public ExpCalculationStrategy expCalculationStrategy;

    /// <summary>
    /// 计算从 1 级升到指定等级所需的累计总经验（level 目标等级；返回总经验）。
    /// </summary>
    public float GetTotalExpForLevel(int level)
    {
        if (level <= 1) return 0f;

        float totalExp = 0f;
        for (int i = 1; i < level; i++)
        {
            totalExp += GetExpForLevel(i);
        }
        return totalExp;
    }

    /// <summary>
    /// 计算从当前等级升到下一级所需经验；公式由 expCalculationStrategy 完成（currentLevel 当前等级；已满级返回 float.MaxValue）。
    /// </summary>
    public float GetExpForLevel(int currentLevel)
    {
        if (currentLevel < 1) return baseExp;
        if (currentLevel >= maxLevel) return float.MaxValue;

        if (expCalculationStrategy != null)
        {
            var context = new ExpCalculationContext(currentLevel, baseExp, growthRate, maxLevel);
            return expCalculationStrategy.GetExpForLevel(context);
        }

        return baseExp;
    }
}
