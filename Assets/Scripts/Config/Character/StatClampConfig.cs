using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性上下限配置：每个角色属性必须存在范围限制。
/// </summary>
[CreateAssetMenu(fileName = "StatClampConfig", menuName = "Eureka/Character/StatClampConfig")]
public class StatClampConfig : ScriptableObject
{
    /// <summary>
    /// 单条属性的最小值与最大值。
    /// </summary>
    [System.Serializable]
    public class ClampEntry
    {
        [Tooltip("属性种类")]
        public StatType type;

        [Tooltip("该属性允许的最小值")]
        public float minValue;

        [Tooltip("该属性允许的最大值")]
        public float maxValue;
    }

    [Header("属性上下限")]
    [Tooltip("各属性上下限，须覆盖全部属性类型")]
    public List<ClampEntry> entries = new();

    /// <summary>
    /// 查询指定属性类型的上下限（type；输出 minValue/maxValue；返回是否找到条目）。
    /// </summary>
    public bool TryGetClamp(StatType type, out float minValue, out float maxValue)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ClampEntry entry = entries[i];
                if (entry != null && entry.type == type)
                {
                    minValue = entry.minValue;
                    maxValue = entry.maxValue;
                    return true;
                }
            }
        }
        minValue = 0f;
        maxValue = 0f;
        return false;
    }

    /// <summary>
    /// 校验列表非空、无重复类型、min≤max，且覆盖全部 StatType（errorMessage 失败原因；返回是否通过）。
    /// </summary>
    public bool ValidateComplete(out string errorMessage)
    {
        if (entries == null || entries.Count == 0)
        {
            errorMessage = "StatClampConfig: 属性上下限列表为空";
            return false;
        }

        HashSet<StatType> seen = new();
        for (int i = 0; i < entries.Count; i++)
        {
            ClampEntry entry = entries[i];
            if (entry == null)
            {
                errorMessage = $"StatClampConfig: 第 {i} 条为空";
                return false;
            }

            if (!seen.Add(entry.type))
            {
                errorMessage = $"StatClampConfig: 存在重复配置 {entry.type}";
                return false;
            }

            if (entry.minValue > entry.maxValue)
            {
                errorMessage = $"StatClampConfig: {entry.type} 的 minValue({entry.minValue}) > maxValue({entry.maxValue})";
                return false;
            }
        }

        foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
        {
            if (!seen.Contains(statType))
            {
                errorMessage = $"StatClampConfig: 缺少 {statType} 的上下限配置";
                return false;
            }
        }
        errorMessage = null;
        return true;
    }
}
