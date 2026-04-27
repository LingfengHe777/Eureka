using UnityEngine;

/// <summary>
/// 商店商品价格策略：基础价 × 难度倍数 ×（线性波次增长 + 二次波次增长）。
///
/// 注意：这里允许出现数学公式；业务脚本禁止出现公式。
/// </summary>
[CreateAssetMenu(fileName = "ShopProductPriceStrategy_WaveGrowth", menuName = "Eureka/ValueStrategy/Shop/ProductPrice/WaveGrowth")]
public class ShopProductPriceStrategy_WaveGrowth : ShopProductPriceStrategy
{
    /// <summary>
    /// 按波次与模式配置计算最终商品价格。
    /// </summary>
    public override int Evaluate(in ShopProductPriceContext ctx)
    {
        int basePrice = Mathf.Max(1, ctx.basePrice);
        if (ctx.modeConfig == null) return basePrice;

        int w = Mathf.Max(0, ctx.wave - 1);

        float waveFactor = 1f
                           + ctx.modeConfig.shopPriceWaveLinearGrowth * w
                           + ctx.modeConfig.shopPriceWaveQuadraticGrowth * w * w;

        float priceF = basePrice * ctx.modeConfig.shopPriceMultiplier * waveFactor;
        int price = Mathf.CeilToInt(priceF);
        return Mathf.Max(1, price);
    }
}
