using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店“抽卡/出货”上下文（强类型、无分配）。
///
/// 说明：
/// - 业务层负责提供候选池与排除集合（例如锁定商品）。
/// - 抽取权重/类型偏好/等级分布等全部由 ShopProductRollStrategy 负责。
/// </summary>
public readonly struct ShopProductRollContext
{
    //道具候选池
    public readonly IReadOnlyList<ItemConfig> itemPool;
    //武器候选池
    public readonly IReadOnlyList<WeaponConfig> weaponPool;
    //排除商品集合
    public readonly HashSet<ScriptableObject> excludedProducts;
    //波次
    public readonly int wave;
    //模式配置
    public readonly ModeConfig modeConfig;

    /// <summary>
    /// 构造抽卡上下文。
    /// </summary>
    public ShopProductRollContext(
        IReadOnlyList<ItemConfig> itemPool,
        IReadOnlyList<WeaponConfig> weaponPool,
        HashSet<ScriptableObject> excludedProducts,
        int wave,
        ModeConfig modeConfig)
    {
        this.itemPool = itemPool;
        this.weaponPool = weaponPool;
        this.excludedProducts = excludedProducts;
        this.wave = wave < 1 ? 1 : wave;
        this.modeConfig = modeConfig;
    }
}
