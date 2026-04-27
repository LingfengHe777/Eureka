using UnityEngine;

/// <summary>
/// 效果基类：由 StatEffectSO 与 LogicEffectSO 继承，通过 Initialize 与 Remove 管理生命周期。
/// </summary>
public abstract class EffectSO : ScriptableObject
{
    [Header("标识")]
    [Tooltip("效果名称，调试或界面展示")]
    public string effectName;

    /// <summary>
    /// 效果被应用时调用（target 通常为玩家）。
    /// </summary>
    public abstract void Initialize(GameObject target);

    /// <summary>
    /// 效果被移除时调用，用于清理资源（target 目标对象）。
    /// </summary>
    public abstract void Remove(GameObject target);
}
