using System;
using UnityEngine;

/// <summary>
/// 玩家事件中心：道具与系统订阅的统一入口。
/// </summary>
public class PlayerEvents : MonoBehaviour
{
    /// <summary>
    /// 击杀敌人（参数：敌人 GameObject）。
    /// </summary>
    public event Action<GameObject> OnEnemyKilled;

    /// <summary>
    /// 受到伤害（伤害值，来源）。
    /// </summary>
    public event Action<float, GameObject> OnDamaged;

    /// <summary>
    /// 已实际扣血（护甲、闪避结算之后；用于画面/音效反馈）。
    /// </summary>
    public event Action<float, GameObject> OnDamageTaken;

    /// <summary>
    /// 造成伤害（伤害值，目标，是否暴击）。
    /// </summary>
    public event Action<float, GameObject, bool> OnDealDamage;

    /// <summary>
    /// 闪避成功。
    /// </summary>
    public event Action OnDodgeSuccess;

    /// <summary>
    /// 生命值变化（当前，最大）。
    /// </summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>
    /// 死亡。
    /// </summary>
    public event Action OnDeath;

    /// <summary>
    /// 生命回满。
    /// </summary>
    public event Action OnHealthFull;

    /// <summary>
    /// 获得经验。
    /// </summary>
    public event Action<float> OnExpGained;

    /// <summary>
    /// 升级（新等级）。
    /// </summary>
    public event Action<int> OnLevelUp;

    /// <summary>
    /// 拾取道具。
    /// </summary>
    public event Action<ItemConfig> OnItemPickedUp;

    /// <summary>
    /// 使用道具。
    /// </summary>
    public event Action<ItemConfig> OnItemUsed;

    /// <summary>
    /// 金币数量变化（当前金币）。
    /// </summary>
    public event Action<int> OnCoinsChanged;

    /// <summary>
    /// 移动（方向）。
    /// </summary>
    public event Action<Vector2> OnMove;

    /// <summary>
    /// 停止移动。
    /// </summary>
    public event Action OnStopMove;

    /// <summary>
    /// 触发击杀敌人事件。
    /// </summary>
    public void TriggerEnemyKilled(GameObject enemy)
    {
        OnEnemyKilled?.Invoke(enemy);
    }

    /// <summary>
    /// 触发受伤事件。
    /// </summary>
    public void TriggerDamaged(float damage, GameObject source)
    {
        OnDamaged?.Invoke(damage, source);
    }

    /// <summary>
    /// 触发「已实际扣血」事件（仅 actualDamage 大于 0 时由 PlayerHealth 调用）。
    /// </summary>
    public void TriggerDamageTaken(float actualDamage, GameObject source)
    {
        OnDamageTaken?.Invoke(actualDamage, source);
    }

    /// <summary>
    /// 触发造成伤害事件。
    /// </summary>
    public void TriggerDealDamage(float damage, GameObject target, bool isCrit = false)
    {
        OnDealDamage?.Invoke(damage, target, isCrit);
    }

    /// <summary>
    /// 触发闪避成功事件。
    /// </summary>
    public void TriggerDodgeSuccess()
    {
        OnDodgeSuccess?.Invoke();
    }

    /// <summary>
    /// 触发生命值变化事件。
    /// </summary>
    public void TriggerHealthChanged(int currentHealth, int maxHealth)
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// 触发死亡事件。
    /// </summary>
    public void TriggerDeath()
    {
        OnDeath?.Invoke();
    }

    /// <summary>
    /// 触发生命回满事件。
    /// </summary>
    public void TriggerHealthFull()
    {
        OnHealthFull?.Invoke();
    }

    /// <summary>
    /// 触发获得经验事件。
    /// </summary>
    public void TriggerExpGained(float exp)
    {
        OnExpGained?.Invoke(exp);
    }

    /// <summary>
    /// 触发升级事件。
    /// </summary>
    public void TriggerLevelUp(int newLevel)
    {
        OnLevelUp?.Invoke(newLevel);
    }

    /// <summary>
    /// 触发拾取道具事件。
    /// </summary>
    public void TriggerItemPickedUp(ItemConfig item)
    {
        OnItemPickedUp?.Invoke(item);
    }

    /// <summary>
    /// 触发使用道具事件。
    /// </summary>
    public void TriggerItemUsed(ItemConfig item)
    {
        OnItemUsed?.Invoke(item);
    }

    /// <summary>
    /// 触发移动事件。
    /// </summary>
    public void TriggerMove(Vector2 direction)
    {
        OnMove?.Invoke(direction);
    }

    /// <summary>
    /// 触发停止移动事件。
    /// </summary>
    public void TriggerStopMove()
    {
        OnStopMove?.Invoke();
    }

    /// <summary>
    /// 触发金币变化事件。
    /// </summary>
    public void TriggerCoinsChanged(int currentCoins)
    {
        OnCoinsChanged?.Invoke(currentCoins);
    }
}
