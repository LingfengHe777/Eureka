using UnityEngine;

/// <summary>
/// 商店商品价格计算上下文（强类型、无分配）。
/// 业务层负责组装上下文，具体公式由 ShopProductPriceStrategy 实现。
/// </summary>
public readonly struct ShopProductPriceContext
{
    //基础价
    public readonly int basePrice;
    //波次（至少为1）
    public readonly int wave;
    //模式配置
    public readonly ModeConfig modeConfig;

    /// <summary>
    /// 构造商品价格上下文。
    /// </summary>
    public ShopProductPriceContext(int basePrice, int wave, ModeConfig modeConfig)
    {
        this.basePrice = basePrice;
        this.wave = Mathf.Max(1, wave);
        this.modeConfig = modeConfig;
    }
}
