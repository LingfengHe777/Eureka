using System;
using UnityEngine;

/// <summary>
/// 角色属性的修正结构体。
/// </summary>
[Serializable]
public struct StatModifier
{
    [Tooltip("要修正的属性")]
    public StatType type;

    [Tooltip("修正量，固定值直接加减，百分比见下方开关")]
    public float value;

    [Tooltip("勾选为百分比修正，数值为百分数；不勾选为固定值")]
    public bool isPercent;

    /// <summary>
    /// 使用类型、数值与百分比标记构造修正。
    /// </summary>
    public StatModifier(StatType type, float value, bool isPercent = false)
    {
        this.type = type;
        this.value = value;
        this.isPercent = isPercent;
    }
}
