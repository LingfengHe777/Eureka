using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

/// <summary>
/// 角色属性：基础值 + 固定/百分比修正，统一公式与Clamp。
/// </summary>
public class StatHandler : MonoBehaviour
{
    [Header("配置引用")]
    //全局默认属性
    public GlobalStatConfig globalConfig;

    //角色差异化初始值
    [HideInInspector]
    public SpecialStatConfig characterConfig;

    //上下限（由GameMgr注入）
    public StatClampConfig statClampConfig;

    //固定值修正字典
    private Dictionary<StatType, float> flatModifiers = new();

    //百分比修正字典
    private Dictionary<StatType, float> percentageModifiers = new();

    private void Awake()
    {
        foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
        {
            flatModifiers[statType] = 0f;
            percentageModifiers[statType] = 0f;
        }
    }

    /// <summary>
    /// 最终属性公式：(基础 + flat) × (1 + percent)，再按 StatClampConfig 夹取。
    /// </summary>
    public float GetStat(StatType statType)
    {
        //获取全局属性默认值
        float baseValue = GetBaseValue(statType);
        //获取固定值修正
        float flatMod = flatModifiers.ContainsKey(statType) ? flatModifiers[statType] : 0f;
        //获取百分比修正
        float percentageMod = percentageModifiers.ContainsKey(statType) ? percentageModifiers[statType] : 0f;
        //转换为计算用数值
        baseValue = StatValueConverter.ToCalculationValue(statType, baseValue);
        flatMod = StatValueConverter.ToCalculationValue(statType, flatMod);
        //计算最终值
        float finalValue = (baseValue + flatMod) * (1f + percentageMod);
        //按配置夹取单属性最终值
        return ClampStatValue(statType, finalValue);
    }

    /// <summary>
    /// 按配置夹取单属性最终值
    /// </summary>
    private float ClampStatValue(StatType statType, float value)
    {
        if (statClampConfig == null)
        {
            throw new InvalidOperationException($"StatHandler: statClampConfig为空，无法计算{statType}的上下限");
        }

        if (!statClampConfig.TryGetClamp(statType, out float minValue, out float maxValue))
        {
            throw new InvalidOperationException($"StatHandler: StatClampConfig 缺少{statType}的上下限配置");
        }

        minValue = StatValueConverter.ToCalculationValue(statType, minValue);
        maxValue = StatValueConverter.ToCalculationValue(statType, maxValue);

        return Mathf.Clamp(value, minValue, maxValue);
    }

    /// <summary>
    /// 基础值转显示/对比用小数
    /// </summary>
    public float GetBaseStat(StatType statType)
    {
        float baseValue = GetBaseValue(statType);
        return StatValueConverter.ToCalculationValue(statType, baseValue);
    }

    /// <summary>
    /// 全局+角色初始修正之和
    /// </summary>
    private float GetBaseValue(StatType statType)
    {
        float baseValue = 0f;

        if (globalConfig != null)
        {
            baseValue = globalConfig.GetDefaultValue(statType);
        }

        if (characterConfig != null && characterConfig.initialStats != null)
        {
            StatModifier characterModifier = characterConfig.initialStats.FirstOrDefault(m => m.type == statType);
            if (characterModifier.type == statType)
            {
                baseValue += characterModifier.value;
            }
        }
        return baseValue;
    }

    /// <summary>
    /// 增加固定修正
    /// </summary>
    public void AddFlatModifier(StatType statType, float value)
    {
        if (flatModifiers.ContainsKey(statType))
        {
            flatModifiers[statType] += value;
        }
        else
        {
            flatModifiers[statType] = value;
        }
    }

    /// <summary>
    /// 减少固定修正
    /// </summary>
    public void RemoveFlatModifier(StatType statType, float value)
    {
        if (flatModifiers.ContainsKey(statType))
        {
            flatModifiers[statType] -= value;
        }
    }

    /// <summary>
    /// 增加百分比修正
    /// </summary>
    public void AddPercentageModifier(StatType statType, float value)
    {
        float ratioValue = StatValueConverter.PercentModifierToRatio(value);
        if (percentageModifiers.ContainsKey(statType))
        {
            percentageModifiers[statType] += ratioValue;
        }
        else
        {
            percentageModifiers[statType] = ratioValue;
        }
    }

    /// <summary>
    /// 减少百分比修正
    /// </summary>
    public void RemovePercentageModifier(StatType statType, float value)
    {
        float ratioValue = StatValueConverter.PercentModifierToRatio(value);
        if (percentageModifiers.ContainsKey(statType))
        {
            percentageModifiers[statType] -= ratioValue;
        }
    }
}
