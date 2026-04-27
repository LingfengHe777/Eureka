using UnityEngine;

/// <summary>
/// 逻辑效果基类，继承自EffectSO
/// 继承自EffectSO
/// 提供堆叠策略与组件注册/反注册辅助，具体逻辑效果需继承本类并实现Initialize / Remove
/// </summary>
public abstract class LogicEffectSO : EffectSO
{   
    public enum LogicStackMode
    {
        [InspectorName("单实例")]
        Single,
        [InspectorName("可叠加")]
        Stackable
    }

    [Header("堆叠")]
    [Tooltip("同一效果在目标上为单实例或可多次挂载")]
    public LogicStackMode stackMode = LogicStackMode.Single;

    /// <summary>
    /// 向 EffectManager 注册该逻辑效果对应的运行时组件。
    /// </summary>
    protected void RegisterComponent(GameObject target, Component component)
    {
        if (target == null || component == null)
        {
            return;
        }

        if (target.TryGetComponent<EffectManager>(out EffectManager manager))
        {
            manager.RegisterLogicComponent(this, component);
        }
    }

    /// <summary>
    /// 从 EffectManager 反注册一个该效果对应组件。
    /// </summary>
    protected Component UnregisterComponent(GameObject target)
    {
        if (target != null && target.TryGetComponent<EffectManager>(out EffectManager manager))
        {
            return manager.UnregisterLogicComponent(this);
        }
        return null;
    }

    /// <summary>
    /// 对运行时组件执行清理并销毁。
    /// </summary>
    protected static void CleanupAndDestroy(Component component)
    {
        if (component == null)
        {
            return;
        }

        if (component is IEffectComponent effectComponent)
        {
            effectComponent.CleanupEffect();
        }

        Object.Destroy(component);
    }
}

/// <summary>
/// 逻辑效果运行时组件的清理约定。
/// </summary>
public interface IEffectComponent
{
    /// <summary>
    /// 道具或效果移除时清理。
    /// </summary>
    void CleanupEffect();
}