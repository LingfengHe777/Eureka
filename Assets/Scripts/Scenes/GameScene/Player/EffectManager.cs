using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 效果管理：记录 EffectSO 堆叠、Logic 组件注册与移除（道具堆叠逻辑入口）。
/// </summary>
public class EffectManager : MonoBehaviour
{
    //Item 维度：同一 EffectSO 被多少 Item 引用并生效
    private readonly Dictionary<EffectSO, int> effectStacks = new();

    //LogicEffectSO：每个效果在本对象上的组件实例列表（堆叠与移除）
    private Dictionary<LogicEffectSO, List<Component>> logicComponents = new();

    /// <summary>
    /// 应用一层效果并视 stackMode 初始化。
    /// </summary>
    public void ApplyEffect(EffectSO effect)
    {
        if (effect == null)
        {
            return;
        }

        int currentCount = 0;
        effectStacks.TryGetValue(effect, out currentCount);
        int newCount = currentCount + 1;
        effectStacks[effect] = newCount;

        if (effect is LogicEffectSO logicEffect && logicEffect.stackMode == LogicEffectSO.LogicStackMode.Single)
        {
            if (newCount == 1)
            {
                effect.Initialize(this.gameObject);
            }
            return;
        }

        effect.Initialize(this.gameObject);
    }

    /// <summary>
    /// 移除一层效果并按 stackMode 反初始化。
    /// </summary>
    public void RemoveEffect(EffectSO effect)
    {
        if (effect == null)
        {
            return;
        }

        if (!effectStacks.TryGetValue(effect, out int currentCount) || currentCount <= 0)
        {
            return;
        }

        if (effect is LogicEffectSO logicEffect && logicEffect.stackMode == LogicEffectSO.LogicStackMode.Single)
        {
            int remaining = currentCount - 1;
            if (remaining <= 0)
            {
                effectStacks.Remove(effect);
                effect.Remove(this.gameObject);
            }
            else
            {
                effectStacks[effect] = remaining;
            }
            return;
        }

        effect.Remove(this.gameObject);
        int newCount = currentCount - 1;
        if (newCount <= 0)
        {
            effectStacks.Remove(effect);
        }
        else
        {
            effectStacks[effect] = newCount;
        }
    }

    /// <summary>
    /// LogicEffectSO 初始化时注册组件引用。
    /// </summary>
    public void RegisterLogicComponent(LogicEffectSO effect, Component component)
    {
        if (effect == null || component == null)
        {
            return;
        }

        if (!logicComponents.TryGetValue(effect, out List<Component> list))
        {
            list = new List<Component>();
            logicComponents[effect] = list;
        }

        list.Add(component);
    }

    /// <summary>
    /// LogicEffectSO Remove 时反注册并弹出组件。
    /// </summary>
    public Component UnregisterLogicComponent(LogicEffectSO effect)
    {
        if (effect == null)
        {
            return null;
        }

        if (!logicComponents.TryGetValue(effect, out List<Component> list) || list.Count == 0)
        {
            return null;
        }

        int lastIndex = list.Count - 1;
        Component component = list[lastIndex];
        list.RemoveAt(lastIndex);

        if (list.Count == 0)
        {
            logicComponents.Remove(effect);
        }
        return component;
    }

    /// <summary>
    /// 移除全部效果（快照按层 RemoveEffect）。
    /// </summary>
    public void RemoveAllEffects()
    {
        if (effectStacks.Count == 0)
        {
            logicComponents.Clear();
            return;
        }

        List<KeyValuePair<EffectSO, int>> snapshot = new List<KeyValuePair<EffectSO, int>>(effectStacks);
        foreach (KeyValuePair<EffectSO, int> pair in snapshot)
        {
            EffectSO effect = pair.Key;
            int count = pair.Value;
            for (int i = 0; i < count; i++)
            {
                RemoveEffect(effect);
            }
        }

        effectStacks.Clear();
        logicComponents.Clear();
    }

    /// <summary>
    /// 效果是否已激活（层数大于 0）。
    /// </summary>
    public bool HasEffect(EffectSO effect)
    {
        return effect != null && effectStacks.ContainsKey(effect) && effectStacks[effect] > 0;
    }

    /// <summary>
    /// 当前堆叠层数。
    /// </summary>
    public int GetEffectStackCount(EffectSO effect)
    {
        if (effect == null) return 0;
        return effectStacks.TryGetValue(effect, out int count) ? count : 0;
    }

    private void OnDestroy()
    {
        RemoveAllEffects();
    }
}
