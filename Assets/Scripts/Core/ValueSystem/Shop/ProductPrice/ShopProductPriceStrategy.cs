using UnityEngine;

/// <summary>
/// 商店“商品价格”策略（商品/武器通用）。
///
/// 说明：
/// - 业务层只允许构造 ShopProductPriceContext 并调用策略求值。
/// - 具体数学公式必须写在派生类中（例如 WaveGrowth 等）。
/// </summary>
public abstract class ShopProductPriceStrategy : ValueStrategy
{
    /// <summary>
    /// 计算最终价格（返回 int，便于 UI 展示与扣费）。
    /// </summary>
    public abstract int Evaluate(in ShopProductPriceContext ctx);

    /// <summary>
    /// 生成“可隐式转换为 int”的数值包装体，用于让业务层保持极简调用。
    /// </summary>
    public ShopProductPriceValue MakeValue(int basePrice, int wave, ModeConfig modeConfig)
    {
        return new ShopProductPriceValue(this, new ShopProductPriceContext(basePrice, wave, modeConfig));
    }

    /// <summary>
    /// 价格数值包装体（Implicit Operator）。
    /// </summary>
    public readonly struct ShopProductPriceValue
    {
        //策略引用
        private readonly ShopProductPriceStrategy strategy;
        //求值上下文
        private readonly ShopProductPriceContext ctx;

        /// <summary>
        /// 构造价格数值包装体。
        /// </summary>
        public ShopProductPriceValue(ShopProductPriceStrategy strategy, ShopProductPriceContext ctx)
        {
            this.strategy = strategy;
            this.ctx = ctx;
        }

        /// <summary>
        /// 隐式转换为 int（无策略时用基础价兜底）。
        /// </summary>
        public static implicit operator int(ShopProductPriceValue v)
        {
            if (v.strategy == null) return Mathf.Max(1, v.ctx.basePrice);
            return v.strategy.Evaluate(v.ctx);
        }
    }
}
