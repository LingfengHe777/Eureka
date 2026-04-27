using UnityEngine;

/// <summary>
/// 武器特性Modifier基类
/// 不配置Modifier时，武器依然按基础等级属性正常工作
/// </summary>
public abstract class WeaponModifier : ScriptableObject
{
    /// <summary>
    /// 攻击前回调
    /// </summary>
    public virtual void OnBeforeAttack(GameObject owner, WeaponConfig weaponConfig, int mountPointIndex) { }

    /// <summary>
    /// 命中前伤害修正
    /// </summary>
    public virtual float ModifyDamage(float currentDamage, GameObject owner, WeaponConfig weaponConfig, Enemy target)
    {
        return currentDamage;
    }

    /// <summary>
    /// 命中后回调
    /// </summary>
    public virtual void OnAfterHit(GameObject owner, WeaponConfig weaponConfig, Enemy target, float finalDamage) { }

    /// <summary>
    /// 攻击后回调
    /// </summary>
    public virtual void OnAfterAttack(GameObject owner, WeaponConfig weaponConfig, int mountPointIndex) { }
}