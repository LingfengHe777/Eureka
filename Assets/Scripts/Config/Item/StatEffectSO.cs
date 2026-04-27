using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性效果：通过 StatModifier 对 StatHandler 施加固定值或百分比修正。
/// </summary>
[CreateAssetMenu(fileName = "StatEffect", menuName = "Eureka/Effect/StatEffect")]
public class StatEffectSO : EffectSO
{
    [Header("属性修正")]
    [Tooltip("随效果应用或移除同步到角色的修正条目")]
    public List<StatModifier> statModifiers = new();

    /// <summary>
    /// 将列表中的修正应用到目标的 StatHandler。
    /// </summary>
    public override void Initialize(GameObject target)
    {
        if (target.TryGetComponent<StatHandler>(out StatHandler statHandler))
        {
            foreach (StatModifier modifier in statModifiers)
            {
                if (modifier.isPercent)
                {
                    statHandler.AddPercentageModifier(modifier.type, modifier.value);
                }
                else
                {
                    statHandler.AddFlatModifier(modifier.type, modifier.value);
                }
            }
        }
    }

    /// <summary>
    /// 从目标的 StatHandler 上撤销本效果对应的修正。
    /// </summary>
    public override void Remove(GameObject target)
    {
        if (target.TryGetComponent<StatHandler>(out StatHandler statHandler))
        {
            foreach (StatModifier modifier in statModifiers)
            {
                if (modifier.isPercent)
                {
                    statHandler.RemovePercentageModifier(modifier.type, modifier.value);
                }
                else
                {
                    statHandler.RemoveFlatModifier(modifier.type, modifier.value);
                }
            }
        }
    }
}
