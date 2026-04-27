using UnityEngine;

/// <summary>
/// 持有期间每秒流血的逻辑效果配置。
/// </summary>
[CreateAssetMenu(fileName = "LogicEffect_BloodDrain", menuName = "Eureka/Effect/LogicEffect/BloodDrain")]
public class LogicEffect_BloodDrainSO : LogicEffectSO
{
    [Header("流血参数")]
    [Min(0.01f)] public float tickIntervalSeconds = 1f;
    [Min(1f)] public float damagePerTick = 1f;

    /// <summary>
    /// 添加并初始化运行时流血组件。
    /// </summary>
    public override void Initialize(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        LogicEffect_BloodDrain component = target.AddComponent<LogicEffect_BloodDrain>();
        RegisterComponent(target, component);
        component.ApplyConfig(this);
    }

    /// <summary>
    /// 反注册并销毁运行时流血组件。
    /// </summary>
    public override void Remove(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Component component = UnregisterComponent(target);
        if (component == null)
        {
            component = target.GetComponent<LogicEffect_BloodDrain>();
        }

        CleanupAndDestroy(component);
    }
}
