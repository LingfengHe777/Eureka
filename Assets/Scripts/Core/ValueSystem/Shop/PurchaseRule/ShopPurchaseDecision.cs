/// <summary>
/// 购买失败原因（用于 UI/日志；避免直接拼接字符串导致分配）。
/// </summary>
public enum ShopPurchaseFailReason
{
    None = 0,
    ProductNull,
    InvalidProductPrice,
    CoinManagerMissing,
    InventoryManagerMissing,
    WeaponManagerMissing,
    ModeConfigMissing,
    PurchaseRuleStrategyMissing,
    NotEnoughCoins,
    WeaponFullCannotMerge,
    UnknownProductType
}

/// <summary>
/// 购买规则输出（纯数据）。
/// </summary>
public readonly struct ShopPurchaseDecision
{
    //是否可购买
    public readonly bool canPurchase;
    //应付价格（至少为1）
    public readonly int priceToPay;
    //失败原因
    public readonly ShopPurchaseFailReason failReason;

    /// <summary>
    /// 构造购买决策。
    /// </summary>
    public ShopPurchaseDecision(bool canPurchase, int priceToPay, ShopPurchaseFailReason failReason)
    {
        this.canPurchase = canPurchase;
        this.priceToPay = priceToPay < 1 ? 1 : priceToPay;
        this.failReason = failReason;
    }

    /// <summary>
    /// 允许购买。
    /// </summary>
    public static ShopPurchaseDecision Allow(int priceToPay) =>
        new ShopPurchaseDecision(true, priceToPay, ShopPurchaseFailReason.None);

    /// <summary>
    /// 拒绝购买。
    /// </summary>
    public static ShopPurchaseDecision Deny(int priceToPay, ShopPurchaseFailReason reason) =>
        new ShopPurchaseDecision(false, priceToPay, reason);
}
