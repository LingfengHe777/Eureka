using UnityEngine;

/// <summary>
/// 商店刷新价格策略：
/// - 基础刷新价随波次增长（线性 + 二次）
/// - 每次刷新倍率也随波次增长
///
/// 注意：这里允许出现数学公式；业务脚本禁止出现公式。
/// </summary>
[CreateAssetMenu(fileName = "ShopRefreshPriceStrategy_Growth", menuName = "Eureka/ValueStrategy/Shop/RefreshPrice/Growth")]
public class ShopRefreshPriceStrategy_Growth : ShopRefreshPriceStrategy
{
    /// <summary>
    /// 按波次、本局刷新次数与模式配置计算刷新价格。
    /// </summary>
    public override int Evaluate(in ShopRefreshPriceContext ctx)
    {
        if (ctx.modeConfig == null) return 100;

        int wave = Mathf.Max(1, ctx.wave);
        int w = Mathf.Max(0, wave - 1);

        float waveFactor = 1f
                           + ctx.modeConfig.basePriceWaveLinearGrowth * w
                           + ctx.modeConfig.basePriceWaveQuadraticGrowth * w * w;

        float perRefreshMultiplier = 1f
                                     + ctx.modeConfig.refreshBaseGrowth
                                     + ctx.modeConfig.refreshGrowthPerWave * w;
        perRefreshMultiplier = Mathf.Max(1.01f, perRefreshMultiplier);

        float priceF = ctx.modeConfig.baseRefreshPrice
                       * waveFactor
                       * Mathf.Pow(perRefreshMultiplier, ctx.refreshCountThisStore);

        int price = Mathf.CeilToInt(priceF);
        return Mathf.Clamp(price, ctx.modeConfig.minRefreshPrice, ctx.modeConfig.maxRefreshPrice);
    }
}
