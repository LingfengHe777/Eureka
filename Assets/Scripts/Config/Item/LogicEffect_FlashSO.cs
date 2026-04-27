using UnityEngine;

/// <summary>
/// 闪现逻辑效果配置，继承LogicEffectSO
/// 定义闪现参数，负责挂载/卸载LogicEffect_Flash运行时组件
/// </summary>
[CreateAssetMenu(fileName = "LogicEffect_Flash", menuName = "Eureka/Effect/LogicEffect/Flash")]
public class LogicEffect_FlashSO : LogicEffectSO
{
    [Header("闪现参数")]
    [Min(0f)] public float cooldownSeconds = 1.5f;
    [Min(0.01f)] public float blinkDistance = 4f;
    [Min(0f)] public float blinkDuration = 0.08f;
    [Tooltip("触发按键")]
    public KeyCode activationKey = KeyCode.Space;

    [Header("碰撞参数")]
    [Min(0f)] public float stopBuffer = 0.03f;
    [Min(4)] public int castBufferSize = 16;
    [Tooltip("墙体层掩码（直接在 Inspector 选择 Wall 层）")]
    public LayerMask wallLayerMask;

    public override void Initialize(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        LogicEffect_Flash component = target.AddComponent<LogicEffect_Flash>();
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
            component = target.GetComponent<LogicEffect_Flash>();
        }

        CleanupAndDestroy(component);
    }
}