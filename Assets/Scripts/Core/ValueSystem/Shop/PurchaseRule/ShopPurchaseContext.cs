using UnityEngine;

/// <summary>
/// 商店“购买规则”上下文（强类型、无分配）。
///
/// 说明：
/// - 业务层负责提供当前商品、动态价格、以及必要的系统引用（Coin/Weapon/Inventory）。
/// - 购买限制/合成可用性/失败原因等全部由 ShopPurchaseRuleStrategy 负责。
/// </summary>
public readonly struct ShopPurchaseContext
{
    //当前商品
    public readonly ScriptableObject product;
    //商品价格（至少为1）
    public readonly int productPrice;

    //金币管理器
    public readonly CoinManager coinManager;
    //背包管理器
    public readonly InventoryManager inventoryManager;
    //武器管理器
    public readonly WeaponManager weaponManager;

    //波次（至少为1）
    public readonly int wave;
    //模式配置
    public readonly ModeConfig modeConfig;

    /// <summary>
    /// 构造购买规则上下文。
    /// </summary>
    public ShopPurchaseContext(
        ScriptableObject product,
        int productPrice,
        CoinManager coinManager,
        InventoryManager inventoryManager,
        WeaponManager weaponManager,
        int wave,
        ModeConfig modeConfig)
    {
        this.product = product;
        this.productPrice = productPrice < 1 ? 1 : productPrice;
        this.coinManager = coinManager;
        this.inventoryManager = inventoryManager;
        this.weaponManager = weaponManager;
        this.wave = wave < 1 ? 1 : wave;
        this.modeConfig = modeConfig;
    }
}
