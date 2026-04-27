using UnityEngine;

/// <summary>
/// 商店“刷新价格”策略。
///
/// 说明：
/// - 业务层只允许构造 ShopRefreshPriceContext 并调用策略求值。
/// - 具体数学公式必须写在派生类中（例如 Growth 等）。
/// </summary>
public abstract class ShopRefreshPriceStrategy : ValueStrategy
{
    /// <summary>
    /// 计算当前刷新价格（返回 int，便于 UI 展示与扣费）。
    /// </summary>
    public abstract int Evaluate(in ShopRefreshPriceContext ctx);

    /// <summary>
    /// 生成“可隐式转换为 int”的数值包装体，用于让业务层保持极简调用。
    /// </summary>
    public ShopRefreshPriceValue MakeValue(int wave, int refreshCountThisStore, ModeConfig modeConfig)
    {
        return new ShopRefreshPriceValue(this, new ShopRefreshPriceContext(wave, refreshCountThisStore, modeConfig));
    }

    /// <summary>
    /// 刷新价格数值包装体（Implicit Operator）。
    /// </summary>
    public readonly struct ShopRefreshPriceValue
    {
        //策略引用
        private readonly ShopRefreshPriceStrategy strategy;
        //求值上下文
        private readonly ShopRefreshPriceContext ctx;

        /// <summary>
        /// 构造刷新价格数值包装体。
        /// </summary>
        public ShopRefreshPriceValue(ShopRefreshPriceStrategy strategy, ShopRefreshPriceContext ctx)
        {
            this.strategy = strategy;
            this.ctx = ctx;
        }

        /// <summary>
        /// 隐式转换为 int（无策略时按模式基础价或默认值兜底）。
        /// </summary>
        public static implicit operator int(ShopRefreshPriceValue v)
        {
            if (v.strategy == null)
            {
                if (v.ctx.modeConfig != null) return Mathf.Max(1, v.ctx.modeConfig.baseRefreshPrice);
                return 100;
            }
            return v.strategy.Evaluate(v.ctx);
        }
    }
}
