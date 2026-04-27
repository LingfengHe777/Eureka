using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包管理员MonoBehaviour
/// 管理道具列表，并触发 Effect 的初始化
/// 加载新场景时，它是从GameSession拿数据
/// </summary>
public class InventoryManager : MonoBehaviour
{
    //当前背包中的道具列表
    private List<ItemConfig> inventory = new();

    //效果管理器
    private EffectManager effectManager;

    private void Awake()
    {
        effectManager = GetComponent<EffectManager>();
    }

    /// <summary>
    /// 初始化背包
    /// </summary>
    public void InitializeFromSession(GameSession session)
    {
        ClearInventory();
    }

    /// <summary>
    /// 添加道具到背包
    /// </summary>
    public bool AddItem(ItemConfig item)
    {
        if (item == null)
        {
            return false;
        }

        inventory.Add(item);

        if (item.effects != null)
        {
            foreach (EffectSO effect in item.effects)
            {
                if (effect != null && effectManager != null)
                {
                    effectManager.ApplyEffect(effect);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 清空背包
    /// </summary>
    public void ClearInventory()
    {
        foreach (var item in inventory)
        {
            if (item != null && item.effects != null)
            {
                foreach (EffectSO effect in item.effects)
                {
                    if (effect != null && effectManager != null)
                    {
                        effectManager.RemoveEffect(effect);
                    }
                }
            }
        }
        inventory.Clear();
    }

    /// <summary>
    /// 获取背包中的所有道具
    /// </summary>
    public List<ItemConfig> GetInventory()
    {
        return new List<ItemConfig>(inventory);
    }
}