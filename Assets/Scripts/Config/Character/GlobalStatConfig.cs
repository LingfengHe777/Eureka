using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全体角色属性配置，用于统一所有角色的基础信息配置。
/// </summary>
[CreateAssetMenu(fileName = "GlobalStatConfig", menuName = "Eureka/Character/GlobalStatConfig")]
public class GlobalStatConfig : ScriptableObject
{
    /// <summary>
    /// 单一属性的默认值配置项
    /// </summary>
    [System.Serializable]
    public class DefaultStat
    {
        [Tooltip("属性种类")]
        public StatType type;

        [Tooltip("未加角色修正时的全局默认值")]
        public float value;
    }

    [Header("全局默认属性")]
    [Tooltip("全角色共用的基础属性列表")]
    public List<DefaultStat> DefaultStatList;

    /// <summary>
    /// 按属性类型查询全局默认值（type；未配置返回 0）。
    /// </summary>
    public float GetDefaultValue(StatType type)
    {
        foreach (DefaultStat status in DefaultStatList)
        {
            if (status.type == type)
                return status.value;
        }
        return 0;
    }
}