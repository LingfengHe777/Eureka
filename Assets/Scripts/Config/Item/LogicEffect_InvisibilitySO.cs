using UnityEngine;

/// <summary>
/// 隐身逻辑效果配置：
/// - 持续时间内免疫伤害
/// - 将玩家预设体可见 Sprite 的 Alpha 调低
/// </summary>
[CreateAssetMenu(fileName = "LogicEffect_Invisibility", menuName = "Eureka/Effect/LogicEffect/Invisibility")]
public class LogicEffect_InvisibilitySO : LogicEffectSO
{
    [Header("隐身参数")]
    [Min(0f)] public float cooldownSeconds = 6f;
    [Min(0.01f)] public float durationSeconds = 2f;
    [Range(0f, 1f)] public float invisibleAlpha = 0.35f;
    [Tooltip("触发按键")]
    public KeyCode activationKey = KeyCode.Space;

    public override void Initialize(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        LogicEffect_Invisibility component = target.AddComponent<LogicEffect_Invisibility>();
        RegisterComponent(target, component);
        component.ApplyConfig(this);
    }

    public override void Remove(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Component component = UnregisterComponent(target);
        if (component == null)
        {
            component = target.GetComponent<LogicEffect_Invisibility>();
        }

        CleanupAndDestroy(component);
    }
}

