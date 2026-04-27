using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using System.Collections.Generic;

/// <summary>
/// 武器类型：近战或远程。
/// </summary>
public enum WeaponType
{
    [InspectorName("近战")]
    Melee,
    [InspectorName("远程")]
    Ranged
}

/// <summary>
/// 近战武器等级数据：命中范围由预制体上 Collider2D 决定，此处仅数值与效果。
/// </summary>
[System.Serializable]
public class MeleeData
{
    [Range(0, 10f)]
    [Tooltip("近战属性对伤害的缩放，1 表示 100%")]
    public float meleeScaling;

    [FormerlySerializedAs("knockback")]
    [Min(0f)]
    [Tooltip("击退位移距离，单位世界坐标")]
    public float knockbackDistance;

    [Min(0f)]
    [Tooltip("击退位移时长，秒；0 为单帧完成")]
    public float knockbackDuration = 0.12f;
}

/// <summary>
/// 远程武器特有数据：仅包含远程相关字段
/// </summary>
[System.Serializable]
public class RangedData
{
    [Range(0, 10f)]
    [Tooltip("远程属性对伤害的缩放，1 表示 100%")]
    public float rangedScaling;

    [Header("弹道")]
    [Tooltip("子弹预制体")]
    public GameObject bulletPrefab;

    [Tooltip("子弹飞行速度")]
    public float bulletSpeed;

    [Tooltip("可穿透的敌人数量")]
    public int pierceCount;
}

/// <summary>
/// 单个等级档位的战斗数值、价格与近战/远程差异化数据。
/// </summary>
[System.Serializable]
public class WeaponTierData
{
    [Range(1, 4)]
    [Tooltip("本条数据对应的品阶 1～4")]
    public int level = 1;

    [Header("战斗")]
    [Tooltip("基础伤害")]
    public float baseDamage;

    [Tooltip("两次攻击间隔，单位秒")]
    public float attackCooldown;

    [Tooltip("攻击距离或判定范围，依武器类型而定")]
    public float attackRange;

    [Header("价格")]
    [Tooltip("商店购入基础价，最终价由模式策略计算")]
    public int buyPrice;

    [Tooltip("出售给商店的基础价")]
    public int sellPrice;

    [Header("元素")]
    [Tooltip("是否附加元素伤害")]
    public bool isElemental;

    [Tooltip("元素部分的基础值")]
    public float elementalBaseDamage;

    [Header("类型扩展")]
    [Tooltip("近战扩展数据，仅近战武器生效")]
    public MeleeData meleeData;

    [Tooltip("远程扩展数据，仅远程武器生效")]
    public RangedData rangedData;

    [Header("修正器")]
    [Tooltip("可选武器特性，留空则仅用上方数值")]
    public List<AssetReferenceT<WeaponModifier>> modifiers = new();
}

[CreateAssetMenu(fileName = "WeaponConfig", menuName = "Eureka/Weapon/WeaponConfig")]
public class WeaponConfig : ScriptableObject
{
    [Header("展示")]
    [Tooltip("唯一 ID，用于合成等逻辑")]
    public string weaponId;

    [Tooltip("显示名称")]
    public string weaponName;

    [Tooltip("图标，用于商店与背包等界面")]
    public Sprite weaponIcon;

    [Tooltip("远程武器枪口显示贴图；近战外观在近战预制体上")]
    public Sprite weaponSprite;

    [TextArea(3, 10)]
    [Tooltip("说明文案")]
    public string weaponDescription;

    [Tooltip("近战或远程")]
    public WeaponType type;

    [Header("品阶")]
    [Range(1, 4)]
    [Tooltip("当前品阶 1～4，对应白蓝紫红")]
    public int weaponLevel = 1;

    [Header("各品阶数值")]
    [Tooltip("各品阶一条数据，至少一条")]
    public List<WeaponTierData> tierDataList = new();

    [Header("音效")]
    [Tooltip("攻击音效可寻址键，近战首次命中或远程开火时播")]
    public string attackSfxKey;

    [Header("近战专用 · 预制体")]
    [Tooltip("近战预制体，含 Animator 与命中碰撞；远程勿填，检视器会隐藏")]
    public GameObject meleeWeaponPrefab;

    //近战 prefab 内 Animator 的攻击 Trigger 参数名（与 Controller 一致）
    public const string MeleeAttackTriggerParameterName = "isAttack";

    //MeleeWeaponHitbox 上供 Animation Event 调用的函数名（与剪辑中 Function 一致）
    public const string MeleeEventAttackBegin = "AttackBegin";
    //攻击伤害判定结束（关闭碰撞体）
    public const string MeleeEventAttackEnd = "AttackEnd";
    //整段攻击动画结束，恢复 gunPos 自动瞄准
    public const string MeleeEventAnimEnd = "AnimEnd";

    //挂载点下显示武器图与近战 Animator 的子物体名（父 gunPos 负责瞄准）
    public const string WeaponVisualChildName = "WeaponVisual";

    /// <summary>
    /// 获取或创建武器显示子物体。贴图与近战 Animator 应挂在此节点上，挥砍动画只应影响其 localRotation/localPosition，
    /// 以便与父节点 mountPoint 的朝向（瞄准）叠加。
    /// </summary>
    public static Transform GetOrCreateWeaponVisualChild(Transform mountPoint)
    {
        if (mountPoint == null) return null;
        Transform child = mountPoint.Find(WeaponVisualChildName);
        if (child == null)
        {
            var go = new GameObject(WeaponVisualChildName);
            child = go.transform;
            child.SetParent(mountPoint, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
        }
        return child;
    }

    /// <summary>
    /// 远程武器动态创建的 SpriteRenderer 默认 Sorting 为 Default/0，可能与角色分层不一致。
    /// 使用玩家 PlayerVisuals 配置的身体 SpriteRenderer 复制 Layer/Order（Order +1 盖在身体上）。
    /// </summary>
    public static void ApplyRangedWeaponSpriteSorting(SpriteRenderer weaponRenderer, Transform mountPoint)
    {
        if (weaponRenderer == null || mountPoint == null) return;

        if (!PlayerVisuals.TryGetBodySpriteRenderer(mountPoint, out SpriteRenderer reference) || reference == weaponRenderer)
        {
            return;
        }

        weaponRenderer.sortingLayerID = reference.sortingLayerID;
        weaponRenderer.sortingOrder = reference.sortingOrder + 1;
    }

    /// <summary>
    /// 从prefab上获取已配置的MeleeWeaponHitbox组件并绑定
    /// </summary>
    public static void InitializeMeleeWeaponHitbox(GameObject inst, WeaponController wc, WeaponConfig cfg)
    {
        if (inst == null) return;
        MeleeWeaponHitbox hb = inst.GetComponentInChildren<MeleeWeaponHitbox>(true);
        if (hb == null) return;
        if (wc != null && cfg != null)
            hb.Initialize(wc, cfg);
    }

    /// <summary>
    /// 优先返回weaponId，否则返回weaponName
    /// </summary>
    public string WeaponIdOrName => string.IsNullOrWhiteSpace(weaponId) ? weaponName : weaponId;

    /// <summary>
    /// 将weaponLevel限制在1–4
    /// </summary>
    public int GetCurrentLevel() => Mathf.Clamp(weaponLevel, 1, 4);

    /// <summary>
    /// 按当前等级返回主题颜色
    /// </summary>
    public Color GetThemeColor()
    {
        switch (GetCurrentLevel())
        {
            case 1: return Color.white;
            case 2: return Color.cyan;
            case 3: return new Color(0.5f, 0f, 0.5f, 1f);
            case 4: return Color.red;
            default: return Color.white;
        }
    }

    /// <summary>
    /// 由等级推导的主题颜色
    /// </summary>
    public Color themeColor => GetThemeColor();

    /// <summary>
    /// 按等级查找对应档位配置
    /// </summary>
    public WeaponTierData GetTierData(int level)
    {
        int targetLevel = Mathf.Clamp(level, 1, 4);
        if (tierDataList != null)
        {
            for (int i = 0; i < tierDataList.Count; i++)
            {
                WeaponTierData tier = tierDataList[i];
                if (tier != null && tier.level == targetLevel)
                {
                    return tier;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 当前等级对应的档位配置
    /// </summary>
    public WeaponTierData GetCurrentTierData() => GetTierData(GetCurrentLevel());

    /// <summary>
    /// 当前等级的基础购买价
    /// </summary>
    public int GetCurrentBuyBasePrice()
    {
        WeaponTierData tier = GetCurrentTierData();
        return tier != null ? tier.buyPrice : 0;
    }

    /// <summary>
    /// 当前等级的基础出售价
    /// </summary>
    public int GetCurrentSellBasePrice()
    {
        WeaponTierData tier = GetCurrentTierData();
        return tier != null ? tier.sellPrice : 0;
    }

    /// <summary>
    /// 当前等级的攻击冷却（秒）
    /// </summary>
    public float GetCurrentAttackCooldown()
    {
        WeaponTierData tier = GetCurrentTierData();
        return tier != null ? tier.attackCooldown : 0f;
    }

    /// <summary>
    /// 当前等级的攻击范围
    /// </summary>
    public float GetCurrentAttackRange()
    {
        WeaponTierData tier = GetCurrentTierData();
        return tier != null ? tier.attackRange : 0f;
    }

    /// <summary>
    /// 近战武器时尝试取得当前档位的MeleeData
    /// </summary>
    public bool TryGetCurrentMeleeData(out MeleeData meleeData)
    {
        meleeData = null;
        if (type != WeaponType.Melee) return false;
        WeaponTierData tier = GetCurrentTierData();
        if (tier == null || tier.meleeData == null) return false;
        meleeData = tier.meleeData;
        return true;
    }

    /// <summary>
    /// 远程武器时尝试取得当前档位的RangedData
    /// </summary>
    public bool TryGetCurrentRangedData(out RangedData rangedData)
    {
        rangedData = null;
        if (type != WeaponType.Ranged) return false;
        WeaponTierData tier = GetCurrentTierData();
        if (tier == null || tier.rangedData == null) return false;
        rangedData = tier.rangedData;
        return true;
    }

    /// <summary>
    /// 当前等级配置的Modifier引用列表
    /// </summary>
    public List<AssetReferenceT<WeaponModifier>> GetCurrentModifierRefs()
    {
        WeaponTierData tier = GetCurrentTierData();
        if (tier == null || tier.modifiers == null) return null;
        return tier.modifiers;
    }

    /// <summary>
    /// 计算当前武器在角色属性加持下的总伤害（playerMelee/playerRanged/playerElemental 为角色近战/远程/元素；返回面板总伤害）。
    /// </summary>
    public float GetTotalDamage(float playerMelee, float playerRanged, float playerElemental)
    {
        WeaponTierData tier = GetCurrentTierData();
        if (tier == null)
        {
            return 0f;
        }

        float scalingBonus = 0;

        if (type == WeaponType.Melee && tier.meleeData != null)
        {
            scalingBonus = playerMelee * tier.meleeData.meleeScaling;
        }
        else if (type == WeaponType.Ranged && tier.rangedData != null)
        {
            scalingBonus = playerRanged * tier.rangedData.rangedScaling;
        }

        float totalElemental = tier.isElemental ? (tier.elementalBaseDamage + playerElemental) : 0;

        return tier.baseDamage + scalingBonus + totalElemental;
    }

    /// <summary>
    /// 仅用于远程：在WeaponVisual上清除Animator。近战Animator一律在meleeWeaponPrefab内，不在此注入
    /// </summary>
    public void ApplyMeleeAnimatorToMount(Transform mountPoint)
    {
        if (mountPoint == null) return;
        if (type == WeaponType.Melee)
            return;

        Transform visual = GetOrCreateWeaponVisualChild(mountPoint);
        Animator anim = visual.GetComponent<Animator>();
        if (anim != null)
            anim.runtimeAnimatorController = null;

        Animator mountAnimator = mountPoint.GetComponent<Animator>();
        if (mountAnimator != null && mountAnimator.runtimeAnimatorController != null)
            mountAnimator.runtimeAnimatorController = null;
    }

    /// <summary>
    /// 卸下武器时清除WeaponVisual与挂载点上的AnimatorController
    /// </summary>
    public static void ClearMeleeAnimatorFromMount(Transform mountPoint)
    {
        if (mountPoint == null) return;
        Transform visual = mountPoint.Find(WeaponVisualChildName);
        if (visual != null)
        {
            Animator anim = visual.GetComponent<Animator>();
            if (anim != null)
                anim.runtimeAnimatorController = null;
        }
        Animator mountAnimator = mountPoint.GetComponent<Animator>();
        if (mountAnimator != null)
            mountAnimator.runtimeAnimatorController = null;
    }

    /// <summary>
    /// 触发近战攻击动画：从WeaponVisual实例上获取prefab内已配置的Animator，使用MeleeAttackTriggerParameterName
    /// </summary>
    public void TriggerMeleeAttackAnimation(Transform mountPoint)
    {
        if (type != WeaponType.Melee) return;
        if (mountPoint == null) return;

        Animator anim = null;

        // 优先从近战命中盒实例反查 Animator，避免与远程 WeaponVisual 同名节点冲突。
        MeleeWeaponHitbox hitbox = mountPoint.GetComponentInChildren<MeleeWeaponHitbox>(true);
        if (hitbox != null)
        {
            anim = hitbox.GetComponentInChildren<Animator>(true);
            if (anim == null)
            {
                anim = hitbox.GetComponentInParent<Animator>();
            }
        }

        if (anim == null || anim.runtimeAnimatorController == null)
        {
            Transform visual = mountPoint.Find(WeaponVisualChildName);
            if (visual != null)
            {
                anim = visual.GetComponentInChildren<Animator>(true);
            }
        }

        if (anim == null || anim.runtimeAnimatorController == null)
            anim = mountPoint.GetComponent<Animator>();
        if (anim == null || anim.runtimeAnimatorController == null) return;

        anim.SetTrigger(MeleeAttackTriggerParameterName);
    }

    /// <summary>
    /// 克隆配置并将等级设为targetLevel，用于运行时独立实例
    /// </summary>
    public static WeaponConfig CreateRuntimeCopy(WeaponConfig source, int targetLevel)
    {
        if (source == null) return null;

        WeaponConfig copy = CreateInstance<WeaponConfig>();
        copy.weaponId = source.weaponId;
        copy.weaponName = source.weaponName;
        copy.weaponIcon = source.weaponIcon;
        copy.weaponSprite = source.weaponSprite;
        copy.weaponDescription = source.weaponDescription;
        copy.type = source.type;
        copy.weaponLevel = Mathf.Clamp(targetLevel, 1, 4);
        copy.attackSfxKey = source.attackSfxKey;
        copy.meleeWeaponPrefab = source.meleeWeaponPrefab;

        copy.tierDataList = new List<WeaponTierData>();
        if (source.tierDataList != null)
        {
            for (int i = 0; i < source.tierDataList.Count; i++)
            {
                WeaponTierData srcTier = source.tierDataList[i];
                if (srcTier == null) continue;

                WeaponTierData dstTier = new WeaponTierData
                {
                    level = srcTier.level,
                    baseDamage = srcTier.baseDamage,
                    attackCooldown = srcTier.attackCooldown,
                    attackRange = srcTier.attackRange,
                    buyPrice = srcTier.buyPrice,
                    sellPrice = srcTier.sellPrice,
                    isElemental = srcTier.isElemental,
                    elementalBaseDamage = srcTier.elementalBaseDamage,
                    meleeData = srcTier.meleeData == null ? null : new MeleeData
                    {
                        meleeScaling = srcTier.meleeData.meleeScaling,
                        knockbackDistance = srcTier.meleeData.knockbackDistance,
                        knockbackDuration = srcTier.meleeData.knockbackDuration
                    },
                    rangedData = srcTier.rangedData == null ? null : new RangedData
                    {
                        rangedScaling = srcTier.rangedData.rangedScaling,
                        bulletPrefab = srcTier.rangedData.bulletPrefab,
                        bulletSpeed = srcTier.rangedData.bulletSpeed,
                        pierceCount = srcTier.rangedData.pierceCount
                    },
                    modifiers = new List<AssetReferenceT<WeaponModifier>>()
                };

                if (srcTier.modifiers != null)
                {
                    for (int j = 0; j < srcTier.modifiers.Count; j++)
                    {
                        AssetReferenceT<WeaponModifier> m = srcTier.modifiers[j];
                        if (m != null)
                        {
                            dstTier.modifiers.Add(m);
                        }
                    }
                }
                copy.tierDataList.Add(dstTier);
            }
        }

        return copy;
    }

    /// <summary>
    /// 编辑器中校验并修正字段：补全 ID、约束等级与类型专属数据
    /// </summary>
    private void OnValidate()
    {
        weaponLevel = Mathf.Clamp(weaponLevel, 1, 4);

        if (tierDataList == null)
        {
            tierDataList = new List<WeaponTierData>();
        }

        if (string.IsNullOrWhiteSpace(weaponId))
        {
            weaponId = weaponName;
        }

        HashSet<int> uniqueLevels = new HashSet<int>();
        for (int i = 0; i < tierDataList.Count; i++)
        {
            WeaponTierData tier = tierDataList[i];
            if (tier == null) continue;

            tier.level = Mathf.Clamp(tier.level, 1, 4);
            uniqueLevels.Add(tier.level);

            if (type == WeaponType.Melee)
            {
                if (tier.meleeData != null)
                {
                    tier.meleeData.knockbackDistance = Mathf.Max(0f, tier.meleeData.knockbackDistance);
                    tier.meleeData.knockbackDuration = Mathf.Max(0f, tier.meleeData.knockbackDuration);
                }

                if (tier.rangedData != null)
                {
                    tier.rangedData = null;
                }
            }
            else if (type == WeaponType.Ranged)
            {
                if (tier.meleeData != null)
                {
                    tier.meleeData = null;
                }
            }
        }
    }
}